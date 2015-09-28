// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Net.Http;
using System.Web.Http;
using System.Web.OData.Extensions;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Library;
using Microsoft.TestCommon;

namespace System.Web.OData.Builder
{
    public class SwaggerControllerTest
    {
        [Fact]
        public void GetSwagger_Throws_IfMissingEdmModelFromRequest()
        {
            // Arrange
            SwaggerController controller = new SwaggerController { Request = new HttpRequestMessage() };

            // Act &  Assert
            Assert.Throws<InvalidOperationException>(
                () => controller.GetSwagger(),
                "The request must have an associated EDM model. Consider using the extension method HttpConfiguration." +
                "Routes.MapODataServiceRoute to register a route that parses the OData URI and attaches the model information.");
        }

        [Fact]
        public void GetSwagger_Returns_SwaggerModelFromRequest()
        {
            // Arrange
            IEdmModel model = new EdmModel();

            SwaggerController controller = new SwaggerController { Request = new HttpRequestMessage() };
            controller.Request.ODataProperties().Model = model;

            // Act
            SwaggerModel responseModel = controller.GetSwagger();

            // Assert
            Assert.NotNull(responseModel);
            Assert.Same(model, responseModel.EdmModel);
        }

        [Fact]
        public void DollarSwaggerMetadata_Works_ForEmptyEdmModel()
        {
            // Arrange
            IEdmModel model = new EdmModel();

            HttpConfiguration config = new[] { typeof(SwaggerController) }.GetHttpConfiguration();
            config.EnableSwaggerMetadata(true);
            HttpServer server = new HttpServer(config);
            config.MapODataServiceRoute("odata", "odata", model);

            HttpClient client = new HttpClient(server);

            // Act
            HttpResponseMessage response = client.GetAsync("http://localhost/odata/$swagger").Result;

            // Assert
            Assert.True(response.IsSuccessStatusCode);
            Assert.Contains("\"swagger\": \"2.0\",", response.Content.ReadAsStringAsync().Result);
        }

        [Fact]
        public void DollarSwaggerMetadata_Works_ForNormalEdmModel()
        {
            // Arrange
            ODataConventionModelBuilder builder = new ODataConventionModelBuilder();
            builder.EntitySet<Customer>("Customers");
            IEdmModel model = builder.GetEdmModel();

            HttpConfiguration config = new[] { typeof(SwaggerController) }.GetHttpConfiguration();
            HttpServer server = new HttpServer(config);
            config.EnableSwaggerMetadata(true);
            config.MapODataServiceRoute("odata", "odata", model);

            HttpClient client = new HttpClient(server);

            // Act
            HttpResponseMessage response = client.GetAsync("http://localhost/odata/$swagger").Result;

            // Assert
            Assert.True(response.IsSuccessStatusCode);

            string payload = response.Content.ReadAsStringAsync().Result;

            Assert.Contains("\"/Customers\": {", payload);
            Assert.Contains("\"System.Web.OData.Builder.Customer\": {", payload);
        }

        private class Customer
        {
            public int Id { get; set; }
        }
    }
}
