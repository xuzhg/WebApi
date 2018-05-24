// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNet.OData.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AspNetCoreODataSample.Web.Models;
using System;
using Microsoft.AspNet.OData.Builder;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Microsoft.OData.Edm.Validation;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Csdl;
using Dynasource.Models;
using Dynasource.Models.Constants;

namespace AspNetCoreODataSample.Web
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<MovieContext>(opt => opt.UseInMemoryDatabase("MovieList"));
            services.AddOData();
            services.AddMvc();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            /*
            var model = EdmModelBuilder.GetEdmModel();

            var b = new ODataConventionModelBuilder();
            b.EnableLowerCamelCase();
            var entityType = b.EntitySet<Route>("Routes").EntityType;
            entityType.HasKey(_ => _.Id);
            entityType.Property(_ => _.Id).IsOptional();
            var m = b.GetEdmModel();
            */
            var m2 = ReadEdmModel2();
            app.UseMvc(builder =>
            {
                builder.Select().Expand().Filter().OrderBy().MaxTop(100).Count();
                /*
                builder.MapODataServiceRoute("odata1", "efcore", model);

                builder.MapODataServiceRoute("odata2", "inmem", model);

                builder.MapODataServiceRoute("odata3", "composite", EdmModelBuilder.GetCompositeModel());

                builder.MapODataServiceRoute("odata1", "odata1", m);*/

                builder.MapODataServiceRoute("odata2", "odata", m2);
            });
        }
/*
        private static IEdmModel ReadEdmModel()
        {
            string filePath = @"E:\MyTemp\metadata\metadata.xml";
            string csdl = File.ReadAllText(filePath);
            IEnumerable<EdmError> errors;
            IEdmModel model;
            if (CsdlReader.TryParse(XElement.Parse(csdl).CreateReader(), out model, out errors))
            {
                return model;
            }

            return null;
        }*/

        private static IEdmModel ReadEdmModel2()
        {
            var builder = new ODataConventionModelBuilder();
            builder.EntityType<BaseConstant>();
            builder.EntitySet<company>("companies");
            return builder.GetEdmModel();
        }
    }

    public class Route
    {
        public Guid Id { get; set; }
    }
}
