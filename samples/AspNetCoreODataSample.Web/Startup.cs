// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNet.OData.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AspNetCoreODataSample.Web.Models;
using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNet.OData;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

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

            //var model = EdmModelBuilder.GetEdmModel();
            var builder1 = new ODataConventionModelBuilder();
            builder1.EnableLowerCamelCase();
            builder1.EntitySet<ApplyCustomer>("ApplyCustomers");
            var model = builder1.GetEdmModel();
            app.UseMvc(builder =>
            {
                builder.Select().Expand().Filter().OrderBy().MaxTop(100).Count();
                builder.MapODataServiceRoute("odata", "odata", model);

                /*
                builder.MapODataServiceRoute("odata1", "efcore", model);

                builder.MapODataServiceRoute("odata2", "inmem", model);

                builder.MapODataServiceRoute("odata3", "composite", EdmModelBuilder.GetCompositeModel());*/
            });
        }
    }

    public class ApplyCustomersController : ODataController
    {
        [EnableQuery]
        public IActionResult Get()
        {
            IList<ApplyCustomer> customers = new List<ApplyCustomer>
            {
                new ApplyCustomer
                {
                    Id = 1,
                    Name = "Conan",
                    DimDateMonth = new ApplyDateMonth
                    {
                        YearNumber = 1999
                    }
                },
                new ApplyCustomer
                {
                    Id = 2,
                    Name = "James",
                    DimDateMonth = new ApplyDateMonth
                    {
                        YearNumber = 2009
                    }
                }
            };

            return Ok(customers);
        }
    }

    public class ApplyCustomer
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public ApplyDateMonth DimDateMonth { get; set; }
    }

    public class ApplyDateMonth
    {
        public int YearNumber { get; set; }
    }
}
