// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Library;
using Microsoft.TestCommon;

namespace System.Web.OData.Formatter.Serialization
{
    public class ODataSwaggerSerializerHelperTest
    {
        private static string _metadataUri = "http://any";
        private static string _host = "host";
        private static string _basePath = "http://localhost";

        [Fact]
        public void ODataSwaggerSerializerHelperCtor_InitialSwaggerDoc()
        {
            // Arrange
            IEdmModel model = new EdmModel();

            // Act
            ODataSwaggerSerializerHelper swagger = new ODataSwaggerSerializerHelper(model, _metadataUri, _host, _basePath);

            // Assert
            Assert.NotNull(swagger);
            Assert.NotNull(swagger.SwaggerDoc);

            Assert.Contains("\"paths\": {},", swagger.SwaggerDoc.ToString());
        }
    }
}
