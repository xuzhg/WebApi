<#
Name: PerfRegression.ps1
Usage: -Workspace [workspace] -TestType Component|Service -Threshold [Percentage] -BuildId [Num] -RunnerParams [XunitRunnerParams]
CopyRight: Copyright (C) Microsoft Corporation. All rights reserved.
#>

Param
(
    [String]$Workspace = $(throw "Workspace path is required."), # The performance working space    
    [Int]$Threshold = $(throw "Threshold is required."), # Percentage, if the percentage ups to this value, it's regression
    [String]$BuildId = $(throw "BuildId is required."),
    [String]$OdlVersion,
    [String]$RunnerParams
)

# In which has the base running bits
$BaseBitsPath = $Workspace + "\BaseBits"
If((Test-Path $BaseBitsPath) -eq $False)
{
   throw "~/BaseBits is required."
}

# In which has the test running bits
$TestBitsPath = $Workspace + "\TestBits" 
If((Test-Path $TestBitsPath) -eq $False)
{
   throw "~/TestBits is required."
}

# In which saves the logs files
$LogPath = $Workspace + "\Logs"
If((Test-Path $LogPath) -eq $False)
{
    New-Item -Path $Workspace -name Logs -ItemType directory
}

# Performance test type
If($TestType -eq "")
{
    $TestType = "Component"
}

<#
Description: Analysis result between test and base
#>
Function AnalysisTestResult
{
    Param(        
        [string]$baseResult,
        [string]$currResult,
        [Int] $threshold,
        [string]$logFile
    )
    # reading the test xml results
    [Xml]$baseXml = Get-Content $baseResult
    [Xml]$currXml = Get-Content $currResult
    $currRun = $currXml.results.run
    $currTests = $currRun.test
       
    $index = 1
    $a =@()
    $exitResult = $True
    foreach($currTest in $currTests)
    {
        $fullName = $currTest.name
        
        $baseTest = FindBaseTest $baseXml $fullName
        If ($baseTest -eq $null)
        {
            throw "Cannot find $fullName in base result."
        }
        
        $info = @{}
        $info.no = $index
        $info.name = $fullName

        $currMean = $currTest.summary.Duration.mean
        $baseMean = $baseTest.summary.Duration.mean
        [decimal]$delta = [decimal]$currMean - [decimal]$baseMean

        If ($delta -lt [decimal]0)
        {
            $delta*=[decimal](-1.0)
            $info.percentage="-"
        }

        $itemResult = "Pass"
        [decimal]$percentage = ( $delta / [decimal]$baseMean ) * 100;
        If ($percentage -gt [decimal]$threshold)
        {
            $exitResult = $False # It's for global
            $itemResult = "Fail"
        }
                
        $info.percentage += [string]([int]$percentage) + "%"
        $info.result = $itemResult
        $a+=$info
        $index+=1
    }

    $a | ConvertTo-json  | Out-File $logFile

    If($exitResult -eq $False)
    {
        # exit 1 # fail
        throw $a | ConvertTo-json 
    }
}

Function Upload([String]$baseFile, [String]$testFile, $testType, [String]$date)
{
    $PROGRAMFILESX = [Environment]::GetFolderPath("ProgramFiles")
    $git = $PROGRAMFILESX + "\Git\bin\git.exe"

    $location = Get-Location

    Set-Location $Workspace
    & $git clone ssh://IdentityDivision@identitydivision.visualstudio.com:22/DefaultCollection/OData/_git/Performance    
    cd Performance
    & $git checkout master

    $historyFile = "OData." + $testType + ".History.md"

    If((Test-Path (".\" + $historyFile)) -eq $True)
    {
        Move-Item -Path $historyFile -Destination .\perftool -Force
    }
    
    #################
    cd perftool
            
    # & .\DrawPerformance.exe $baseFile $testFile $OdlVersion $Threshold "Regression"
    & .\PerfTool.exe -b $baseFile -t $testFile -v $OdlVersion -a $Threshold "-mean"

    $imageMaxFile = ".\OData." + $testType
    Move-Item -Path ($imageMaxFile + ".max.png") -Destination ..\images -Force
    Move-Item -Path ($imageMaxFile + ".mean.png") -Destination ..\images -Force
    Move-Item -Path ($imageMaxFile + ".min.png") -Destination ..\images -Force   
    Move-Item -Path (".\" + $historyFile) -Destination ..\ -Force
    Move-Item -Path .\*.latest.md -Destination ..\ -Force
    Move-Item -Path .\*.md -Destination ..\Logs -Force
    
    cd ..
    $commitstring = "Update " + $OdlVersion + " Performance results at " + $date

    #################
    & $git add .
    & $git commit -m $commitstring
    & $git push origin master

    Set-Location $location
}

<#
Description: search the test in base result.
#>
Function FindBaseTest([Xml]$baseXml, [string]$testName)
{
    $tests = $baseXml.results.run.test

    foreach($test in $tests)
    {
        $fullName = $test.name

        If($fullName -eq $testName)
        {
            return $test
        }
    }

    return $null
}

<#
Description: Run the performance test cases
#>
Function Execute([string]$testFolder, [string]$runid)
{
    $location = Get-Location
    Set-Location ($testFolder + "\bin")

    $testDllName = "WebApiPerformance.Test.dll"
    $result = $runid + ".xml"
    $analysisResult = $runid + ".analysisResult.xml"
    .\xunit.performance.run.exe $testDllName -runner .\xunit.console.exe -runnerargs "-parallel none $RunnerParams" -runid $runid
    .\xunit.performance.analysis.exe $result -xml $analysisResult

    Set-Location $location
}

# Step 1. Get the running date & test result name
$RunDate = Get-Date -Format yyyyMMdd
$TestRunId = "WebApiOData." + $RunDate + "." + $BuildId

# Step 2. Run the current tests
Execute $TestBitsPath $TestRunId
$TestResult = $TestBitsPath + "\bin\" + $TestRunId + ".analysisResult.xml"
Move-Item -Path $TestResult -Destination ($TestBitsPath + "/test.xml") -Force

# Step 3. Run the base tests
Execute $BaseBitsPath $TestRunId
$BaseResult = $BaseBitsPath + "\bin\" + $TestRunId + ".analysisResult.xml"
Move-Item -Path $BaseResult -Destination ($BaseBitsPath + "/base.xml") -Force

<#
$old_ErrorActionPreference = $ErrorActionPreference
$ErrorActionPreference = 'SilentlyContinue'
Upload $BaseResult $TestResult $TestType $RunDate

$ErrorActionPreference = $old_ErrorActionPreference
# Step 4. Analysis the between test results and base results
$LogFileName = $TestRunId + ".log"
$LogFilePath = $LogPath + "\" + $LogFileName
If((Test-Path $LogFilePath ) -eq $False)
{
    New-Item -Path $LogPath -name $LogFileName -ItemType File
}

AnalysisTestResult $BaseResult $TestResult $Threshold $LogFilePath
#>
