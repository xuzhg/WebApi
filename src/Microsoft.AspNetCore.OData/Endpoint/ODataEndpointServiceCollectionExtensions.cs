#if !NETSTANDARD2_0
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;

namespace Microsoft.AspNetCore.OData.Routing
{
    /// <summary>
    /// 
    /// </summary>
    public static class ODataEndpointServiceCollectionExtensions
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="services"></param>
        /// <param name="setupAction"></param>
        /// <returns></returns>
        public static IServiceCollection AddODataRouting(this IServiceCollection services, Action<ODataRoutingOptions> setupAction)
        {
            AddODataRoutingServices(services);
            services.Configure(setupAction);
            return services;
        }

        static void AddODataRoutingServices(IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddSingleton<ODataModelFactory>();

            services.TryAddEnumerable(
                ServiceDescriptor.Transient<IApplicationModelProvider, ODataEndpointModelProvider>());

            services.TryAddEnumerable(ServiceDescriptor.Singleton<MatcherPolicy, ODataEndpointMatcherPolicy>());
        }
    }
}
#endif