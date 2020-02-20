// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using Microsoft.AspNet.OData.Common;
using Microsoft.AspNet.OData.Formatter;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData;
using Microsoft.OData.Edm;
using System.Text;

namespace Microsoft.AspNet.OData.Query.Validators
{
    /// <summary>
    /// Represents a validator used to validate a <see cref="TopQueryOption"/> based on the <see cref="ODataValidationSettings"/>.
    /// </summary>
    public class TopQueryValidator
    {
        /// <summary>
        /// Validates a <see cref="TopQueryOption" />.
        /// </summary>
        /// <param name="topQueryOption">The $top query.</param>
        /// <param name="validationSettings">The validation settings.</param>
        public virtual void Validate(TopQueryOption topQueryOption, ODataValidationSettings validationSettings)
        {
            if (topQueryOption == null)
            {
                throw Error.ArgumentNull("topQueryOption");
            }

            if (validationSettings == null)
            {
                throw Error.ArgumentNull("validationSettings");
            }

            if (topQueryOption.Value > validationSettings.MaxTop)
            {
                throw new ODataException(Error.Format(SRResources.SkipTopLimitExceeded, validationSettings.MaxTop,
                    AllowedQueryOptions.Top, topQueryOption.Value));
            }

#if NETCORE
         //   Query.Validators.LogFile.Instance.AddLog("Request: " + topQueryOption.Context.Request.ToString());
#endif

            //Query.Validators.LogFile.Instance.AddLog(GeneratePath(topQueryOption.Context.Path));

            int maxTop;
            IEdmProperty property = topQueryOption.Context.TargetProperty;
            IEdmStructuredType structuredType = topQueryOption.Context.TargetStructuredType;

            if (EdmLibHelpers.IsTopLimitExceeded(
                property,
                structuredType,
                topQueryOption.Context.Model,
                topQueryOption.Value, topQueryOption.Context.DefaultQuerySettings,
                out maxTop))
            {
                throw new ODataException(Error.Format(SRResources.SkipTopLimitExceeded, maxTop,
                    AllowedQueryOptions.Top, topQueryOption.Value));
            }
        }

        private static string GeneratePath(Routing.ODataPath path)
        {
            StringBuilder sb = new StringBuilder("~/");
            foreach (var segment in path.Segments)
            {
                sb.Append(segment.Identifier);
                sb.Append("/");
            }

            return sb.ToString();
        }

        internal static TopQueryValidator GetTopQueryValidator(ODataQueryContext context)
        {
            if (context == null || context.RequestContainer == null)
            {
                return new TopQueryValidator();
            }

            return context.RequestContainer.GetRequiredService<TopQueryValidator>();
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class LogFile : System.IDisposable
    {
        /// <summary>
        /// 
        /// </summary>
        public static LogFile Instance = new LogFile(@"c:\odatalog.txt");

        private System.IO.StreamWriter _file;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        public LogFile(string fileName)
        {
            _file = new System.IO.StreamWriter(fileName, true);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="msg"></param>
        public void AddLog(string msg)
        {
            _file.WriteLine(msg);
            _file.Flush();
        }

        /// <summary>
        /// 
        /// </summary>
        public void Dispose()
        {
             if (_file != null)
            {
                _file.Flush();
                _file.Dispose();
            }
        }
    }
}
