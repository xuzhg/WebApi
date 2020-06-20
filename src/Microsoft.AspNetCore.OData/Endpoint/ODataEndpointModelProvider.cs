#if !NETSTANDARD2_0
using Microsoft.AspNet.OData;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.OData.Routing
{
    internal class ODataEndpointModelProvider : IApplicationModelProvider
    {
        private ConcurrentDictionary<string, IServiceProvider> _perRouteContainers = new ConcurrentDictionary<string, IServiceProvider>();
        private IServiceProvider _serviceProvider;
        private readonly IOptions<ODataRoutingOptions> _options;

        public ODataEndpointModelProvider(
            IServiceProvider serviceProvider,
            IOptions<ODataRoutingOptions> options)
        {
            _serviceProvider = serviceProvider;
            _options = options;
            Initialize();
        }

        public int Order => 100;

        public void OnProvidersExecuted(ApplicationModelProviderContext context)
        {
        //    var conventions = _options.Value.Conventions.OrderBy(c => c.Order);
            var routes = _options.Value.Models;

            // Can apply on controller
            // for all conventions, 
            foreach (var route in routes)
            {
                IEdmModel model = route.Value;
                if (model == null || model.EntityContainer == null)
                {
                    continue;
                }

                IEnumerable<IODataControllerActionConvention> conventions = GetConventions(route.Key);

                foreach (var controller in context.Result.Controllers)
                {
                    // apply to ODataModelAttribute
                    if (!CanApply(route.Key, controller))
                    {
                        continue;
                    }

                    // Add here
                    //

                    // Get conventions for all this controller
                    // Get conventions for all this controller
                    ODataControllerContext odataContext = BuildContext(route.Key, model, controller);

                    IODataControllerActionConvention[] newConventions =
                        conventions.Where(c => c.AppliesToController(odataContext, controller)).ToArray();

                    if (newConventions.Length > 0)
                    {
                        foreach (var action in controller.Actions)
                        {
                            foreach (var con in newConventions)
                            {
                                if (con.AppliesToAction(odataContext, action))
                                {
                                    break;
                                }
                            }
                        }
                    }

                    //foreach (var convention in conventions)
                    //{
                    //    if (convention.AppliesToController(route.Key, route.Value, controller))
                    //    {
                    //        foreach (var action in controller.Actions)
                    //        {
                    //            if (convention.AppliesToAction(route.Key, route.Value, action))
                    //            {
                    //                ;
                    //            }
                    //        }
                    //    }
                    //}
                }
            }

            Console.WriteLine("OnProvidersExecuted");
        }

        public void OnProvidersExecuting(ApplicationModelProviderContext context)
        {
            Console.WriteLine("OnProvidersExecuting");
        }

        private static ODataControllerContext BuildContext(string prefix, IEdmModel model, ControllerModel controller)
        {
            // The reason why it's better to create a context is that:
            // We don't need to call te FindEntitySet or FindSingleton in every convention
            string controllerName = controller.ControllerName;

            IEdmEntitySet entitySet = model.EntityContainer.FindEntitySet(controllerName);
            if (entitySet == null)
            {
                return new ODataControllerContext(prefix, model, entitySet);
            }

            IEdmSingleton singleton = model.EntityContainer.FindSingleton(controllerName);
            if (singleton != null)
            {
                return new ODataControllerContext(prefix, model, singleton);
            }

            return new ODataControllerContext(prefix, model);
        }

        private static bool CanApply(string prefix, ControllerModel controller)
        {
            ODataModelAttribute odataModel = controller.GetAttribute<ODataModelAttribute>();
            if (odataModel == null)
            {
                return true; // apply to all model
            }
            else if (prefix == odataModel.Model)
            {
                return true;
            }

            return false;
        }

        private static bool CanApply(IEdmModel model, ControllerModel controllerModel)
        {
            if (model == null || model.EntityContainer == null)
            {
                return false;
            }

            string controllerName = controllerModel.ControllerName;

            if (controllerName == "ODataOperationImport")
            {
                // Convention for the actionimport/function import
                return true;
            }

            IEdmEntitySet entitySet = model.EntityContainer.FindEntitySet(controllerName);
            if (entitySet != null)
            {
                return true;
            }

            IEdmSingleton singleton = model.EntityContainer.FindSingleton(controllerName);
            if (singleton != null)
            {
                return true;
            }

            return false;
        }

        internal IEnumerable<IODataControllerActionConvention> GetConventions(string prefix)
        {
            IServiceProvider sp = _perRouteContainers[prefix];
            return sp.GetServices<IODataControllerActionConvention>();
        }

        internal void Initialize()
        {
            var models = _options.Value.Models;
            var perRoutes = _options.Value.PreRoutePrividers;

            foreach (var config in perRoutes)
            {
                IContainerBuilder odataContainerBuilder = null;
                if (_serviceProvider != null)
                {
                    odataContainerBuilder = _serviceProvider.GetService<IContainerBuilder>();
                }
                if (odataContainerBuilder == null)
                {
                    odataContainerBuilder = new DefaultContainerBuilder();
                }

                odataContainerBuilder.AddDefaultODataServices();

                RegisterDefaultConventions(odataContainerBuilder);

                config.Value?.Invoke(odataContainerBuilder);

                // Set Uri resolver to by default enabling unqualified functions/actions and case insensitive match.
                odataContainerBuilder.AddService(
                   Microsoft.OData.ServiceLifetime.Singleton,
                    typeof(ODataUriResolver),
                    sp => new UnqualifiedODataUriResolver { EnableCaseInsensitive = true });

                IEdmModel model = models[config.Key];

                odataContainerBuilder.AddService(Microsoft.OData.ServiceLifetime.Singleton, sp => model);

                _perRouteContainers[config.Key] = odataContainerBuilder.BuildContainer();
            }
        }

        internal void RegisterDefaultConventions(IContainerBuilder builder)
        {
            IList<IODataControllerActionConvention> conventions = new List<IODataControllerActionConvention>
            {
                new MetadataEndpointConvention(),
                new EntitySetEndpointConvention(),
                new SingletonEndpointConvention(),
                new EntityEndpointConvention(),
                new OperationImportEndpointConvention(),
                new OperationEndpointConvention(),
                new PropertyEndpointConvention()
            };

            builder.AddService(Microsoft.OData.ServiceLifetime.Singleton, sp => conventions.AsEnumerable());
        }
    }
}
#endif