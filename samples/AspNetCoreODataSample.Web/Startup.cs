// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNet.OData.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AspNetCoreODataSample.Web.Models;
using Microsoft.OData.Edm;
using Microsoft.AspNet.OData.Builder;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace AspNetCoreODataSample.Web
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public static readonly LoggerFactory MyLoggerFactory
            = new LoggerFactory(new[] { new ConsoleLoggerProvider((_, __) => true, true) });

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            string ConnectionString = @"Server=(localdb)\mssqllocaldb;Database=Demo.UsersSam;Integrated Security=True;ConnectRetryCount=0";
            services.AddDbContext<UsersContext>(opt => opt.UseSqlServer(ConnectionString).UseLoggerFactory(MyLoggerFactory));

            //services.AddDbContext<MovieContext>(opt => opt.UseInMemoryDatabase("MovieList"));
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

           // var model = EdmModelBuilder.GetEdmModel();
            var model = GetEdmModel();
            app.UseMvc(builder =>
            {
                builder.Select().Expand().Filter().OrderBy().MaxTop(100).Count();

                builder.MapODataServiceRoute("odata1", "odata", model);
                /*
                builder.MapODataServiceRoute("odata2", "inmem", model);

                builder.MapODataServiceRoute("odata3", "composite", EdmModelBuilder.GetCompositeModel());*/
            });
        }

        private static IEdmModel GetEdmModel()
        {
            //var builder = new ODataModelBuilder();
            var builder = new ODataConventionModelBuilder();
            builder.EntitySet<User>("Users");
            /*
            builder.EnableLowerCamelCase();
            var type = builder.EntitySet<User>("Users").EntityType;
            type.HasKey(_ => _.Id);
            type.Property(_ => _.Id).IsOptional();*/

            return builder.GetEdmModel();
        }
    }

    public class User
    {
        public int Id { get; set; }

        public string UserName { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public int Age { get; set; }
    }
}
