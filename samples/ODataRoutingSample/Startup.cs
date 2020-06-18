using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ODataRoutingSample.Models;
using Microsoft.OData.Edm;
using Microsoft.AspNetCore.OData.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNet.OData.Extensions;
using Microsoft.AspNet.OData.Formatter.Serialization;
using Microsoft.AspNet.OData.Routing;
using Microsoft.OData;
using Microsoft.OData.Json;
using Microsoft.AspNet.OData.Formatter;
using Microsoft.AspNet.OData.Formatter.Deserialization;

namespace ODataRoutingSample
{
    public class Startup
    {
        private IEdmModel model;
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            model = EdmModelBuilder.GetEdmModel();
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddOData();
            services.AddRouting();

            services.AddODataRouting(options => options
                .AddModel(EdmModelBuilder.GetEdmModel())
                .AddModel("v1", EdmModelBuilder.GetEdmModelV1())
                .AddModel("v2{data}", EdmModelBuilder.GetEdmModelV2()));

            RegisterMissingServices(services);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.Use(next => context =>
            {
                var endpoint = context.GetEndpoint();
                if (endpoint == null)
                {
                    return next(context);
                }


                return next(context);
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();//.WithOData(model);
      //          endpoints.MapODataRoute("odata", "odata", model);
            });
        }

        private static void RegisterMissingServices(IServiceCollection services/*, IEdmModel model*/)
        {
            // services.AddSingleton(model);

            services.AddSingleton<ODataSerializerProvider, DefaultODataSerializerProvider>();
            services.AddSingleton<ODataDeserializerProvider, DefaultODataDeserializerProvider>();

            // Deserializers.
            services.AddSingleton<ODataResourceDeserializer>();
            services.AddSingleton<ODataEnumDeserializer>();
            services.AddSingleton<ODataPrimitiveDeserializer>();
            services.AddSingleton<ODataResourceSetDeserializer>();
            services.AddSingleton<ODataCollectionDeserializer>();
            services.AddSingleton<ODataEntityReferenceLinkDeserializer>();
            services.AddSingleton<ODataActionPayloadDeserializer>();

            services.AddSingleton<ODataResourceSetSerializer>();
            services.AddSingleton<ODataResourceSerializer>();
            services.AddSingleton<ODataPrimitiveSerializer>();
            services.AddSingleton<IODataPathHandler, DefaultODataPathHandler>();
            services.AddSingleton<ODataMessageWriterSettings>();
            services.AddSingleton<ODataMediaTypeResolver>();
            services.AddSingleton<ODataMessageInfo>();
            services.AddSingleton<ODataPayloadValueConverter>();
            services.AddSingleton<ODataSimplifiedOptions>();
            services.AddSingleton<IJsonWriterFactory, DefaultJsonWriterFactory>();
            //     services.AddSingleton<IETagHandler, DefaultODataETagHandler>();

            services.AddSingleton(new ODataMessageReaderSettings
            {
                EnableMessageStreamDisposal = false,
                MessageQuotas = new ODataMessageQuotas { MaxReceivedMessageSize = Int64.MaxValue },
            });
        }
    }
}
