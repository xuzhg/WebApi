// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Linq;
using System.Net.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Routing;
using Microsoft.TestCommon;

namespace System.Web.OData.Routing.Conventions
{
    public class SwaggerRoutingConventionTest
    {
        [Fact]
        public void SelectController_ReturnsNull_IfNoSwaggerMetadata()
        {
            // Arrange
            ODataPath odataPath = new ODataPath(new MetadataPathSegment());
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/");

            // Act
            string controller = new SwaggerRoutingConvention().SelectController(odataPath, request);

            // Assert
            Assert.Null(controller);
        }

        [Fact]
        public void SelectController_ReturnsSwaggerController_IfSwaggerMetadata()
        {
            // Arrange
            ODataPath odataPath = new ODataPath(new SwaggerPathSegment());
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/");

            // Act
            string controller = new SwaggerRoutingConvention().SelectController(odataPath, request);

            // Assert
            Assert.Equal("Swagger", controller);
        }

        [Fact]
        public void SelectAction_ReturnsNull_IfNoSwaggerMetadata()
        {
            // Arrange
            ODataPath odataPath = new ODataPath(new MetadataPathSegment());
            ILookup<string, HttpActionDescriptor> emptyActionMap = new HttpActionDescriptor[0].ToLookup(desc => (string)null);
            HttpControllerContext controllerContext = new HttpControllerContext();
            controllerContext.Request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/");
            controllerContext.Request.SetRouteData(new HttpRouteData(new HttpRoute()));

            // Act
            string action = new SwaggerRoutingConvention().SelectAction(odataPath, controllerContext, emptyActionMap);

            // Assert
            Assert.Null(action);
        }

        [Fact]
        public void SelectAction_ReturnsGetSwaggerAction_IfSwaggerMetadata()
        {
            // Arrange
            ODataPath odataPath = new ODataPath(new SwaggerPathSegment());
            ILookup<string, HttpActionDescriptor> emptyActionMap = new HttpActionDescriptor[0].ToLookup(desc => (string)null);
            HttpControllerContext controllerContext = new HttpControllerContext();
            controllerContext.Request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/");
            controllerContext.Request.SetRouteData(new HttpRouteData(new HttpRoute()));

            // Act
            string action = new SwaggerRoutingConvention().SelectAction(odataPath, controllerContext, emptyActionMap);

            // Assert
            Assert.Equal("GetSwagger", action);
        }
    }
}
