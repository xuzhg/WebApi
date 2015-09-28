// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Web.Http;
using System.Web.OData.Properties;
using Microsoft.OData.Core;
using Microsoft.OData.Edm;

namespace System.Web.OData.Formatter.Serialization
{
    /// <summary>
    /// Represents an <see cref="ODataSerializer"/> for serializing $swagger. 
    /// </summary>
    public class ODataSwaggerSerializer : ODataSerializer
    {
        /// <summary>
        /// Initializes a new instance of <see cref="ODataSwaggerSerializer"/>.
        /// </summary>
        public ODataSwaggerSerializer()
            : base(ODataPayloadKind.MetadataDocument)
        {
        }

        /// <inheritdoc/>
        public override void WriteObject(object graph, Type type, ODataMessageWriter messageWriter,
            ODataSerializerContext writeContext)
        {
            if (writeContext == null)
            {
                throw Error.ArgumentNull("writeContext");
            }

            SwaggerModel model = graph as SwaggerModel;
            if (model == null)
            {
                throw new SerializationException(SRResources.SwaggerModelMissingDuringSerialization);
            }

            WriteSwaggerInline(model, writeContext);
        }

        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "stopgap. will be used later.")]
        private void WriteSwaggerInline(SwaggerModel swaggerModel, ODataSerializerContext writeContext)
        {
            Contract.Assert(writeContext.Request != null);
            Contract.Assert(writeContext.Stream != null);
            Contract.Assert(swaggerModel.EdmModel != null);

            Stream writeStream = writeContext.Stream;
            var requestUri = writeContext.Request.RequestUri;

            const int SwaggerLen = 9; // $swagger

            var metadataUri = Uri.UnescapeDataString(requestUri.AbsoluteUri);
            var host = requestUri.Authority;
            var basePath = requestUri.LocalPath.Substring(0, requestUri.LocalPath.Length - SwaggerLen);
            IEdmModel model = swaggerModel.EdmModel;

            ODataSwaggerSerializerHelper swaggerHelper = new ODataSwaggerSerializerHelper(model, metadataUri, host, basePath);

            string str = swaggerHelper.SwaggerDoc.ToString().Trim();
            byte[] buffer = Encoding.UTF8.GetBytes(str);
            writeStream.WriteAsync(buffer, 0, buffer.Length);
            writeStream.Flush();
        }
    }
}
