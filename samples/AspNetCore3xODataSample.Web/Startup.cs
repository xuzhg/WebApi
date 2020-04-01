// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using AspNetCore3xODataSample.Web.Models;
using Castle.Core.Logging;
using Microsoft.AspNet.OData.Extensions;
using Microsoft.AspNet.OData.Query;
using Microsoft.AspNet.OData.Routing.Conventions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OData;
using Microsoft.OData.Edm;
using System.Collections.Generic;
using ILoggerFactory = Microsoft.Extensions.Logging.ILoggerFactory;

namespace AspNetCore3xODataSample.Web
{
    public class Startup
    {
        public static readonly ILoggerFactory MyLoggerFactory
           = LoggerFactory.Create(builder => { builder.AddConsole(); });

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<ProductDepartmentContext>(opt => opt.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")).UseLoggerFactory(MyLoggerFactory));

         //   services.AddDbContext<CustomerOrderContext>(opt => opt.UseLazyLoadingProxies().UseInMemoryDatabase("CustomerOrderList"));
            services.AddOData();
            services.AddMvc(options => options.EnableEndpointRouting = false);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            IEdmModel model = EdmModelBuilder.GetEdmModel();

            app.UseMvc(builder =>
            {
                builder.Select().Expand().Filter().OrderBy().MaxTop(100).Count();

            //   builder.MapODataServiceRoute("odata", "odata", model);

            builder.MapODataServiceRoute("odata", "odata", containerBuilder =>
            containerBuilder.AddService(Microsoft.OData.ServiceLifetime.Singleton, sp => model)
            .AddService(Microsoft.OData.ServiceLifetime.Scoped, sp => new ODataQuerySettings
            {
                HandleNullPropagation = HandleNullPropagationOption.False
            })
                       .AddService<IEnumerable<IODataRoutingConvention>>(Microsoft.OData.ServiceLifetime.Singleton, sp =>
                           ODataRoutingConventions.CreateDefaultWithAttributeRouting("odata", builder)));
            });
        }
    }
}
