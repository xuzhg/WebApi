using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AspNetCore3xODataSample.Web.Models;
using Microsoft.AspNet.OData.Extensions;
using Microsoft.AspNet.OData.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AspNetCore3xODataSample.Web
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
            Test();
            services.AddOData();
            services.AddSingleton<ODataEndpointDataSource>();
            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseMiddleware<MyEndpointRoutingTestMiddle>();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapODataServiceRoute("odata", "odata", EdmModelBuilder.GetEdmModel());
            });
        }

        public static void Test()
        {
            string pattern = "odata/{*odataPath}";
            RoutePattern routePattern = RoutePatternFactory.Parse(pattern);
            Console.WriteLine(routePattern.PathSegments);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class MyEndpointRoutingTestMiddle
    {
  //      private readonly IEndpointRouteBuilder _endpointBuilder;
        private readonly RequestDelegate _next;

        public MyEndpointRoutingTestMiddle(/*IEndpointRouteBuilder endpointRouteBuilder,*/
            RequestDelegate next)
        {
      //      _endpointBuilder = endpointRouteBuilder;
            _next = next;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="httpContext"></param>
        /// <returns></returns>
        public Task Invoke(HttpContext httpContext)
        {
            return _next(httpContext);
        }
    }
}
