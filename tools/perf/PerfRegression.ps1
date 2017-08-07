<#
Name: PerfRegression.ps1
Usage: -Workspace [workspace] -BuildId [Num]
CopyRight: Copyright (C) Microsoft Corporation. All rights reserved.
#>

Param
(
    [String]$Workspace = $(throw "Workspace path is required."), 
    [String]$BuildId = $(throw "BuildId is required."),
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
