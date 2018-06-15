// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Web.Http;
using AspNetODataSample.Web.Models;
using Microsoft.AspNet.OData.Extensions;

namespace AspNetODataSample.Web
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {/*
            var model = EdmModelBuilder.GetEdmModel();
            config.MapODataServiceRoute("odata", "odata", model);*/

            // Web API routes
            config.MapHttpAttributeRoutes();

            //OData support
            config.AddODataQueryFilter();
            config.EnableDependencyInjection();
            /*
            ODataConventionModelBuilder builder = new ODataConventionModelBuilder();

            builder.EnableLowerCamelCase(NameResolverOptions.ProcessReflectedPropertyNames | NameResolverOptions.ProcessExplicitPropertyNames);

            builder.EntitySet<KISS>("KISS");

            var edmModel = builder.GetEdmModel();*/

            //   config.MapODataServiceRoute("ODataRoute", "/", edmModel);

            config.Routes.MapHttpRoute(
                "DefaultApi",
                "{controller}/{id}",
                new { id = RouteParameter.Optional }
            );

            config.Count().Filter().OrderBy().Select().MaxTop(2000);
        }
    }
}
