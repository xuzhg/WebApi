#if !NETSTANDARD2_0
using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Extensions;
using Microsoft.AspNet.OData.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.OData.Edm;
using Microsoft.OData;
using Microsoft.OData.UriParser;
using ServiceLifetime = Microsoft.OData.ServiceLifetime;

namespace Microsoft.AspNetCore.OData.Routing
{
    /// <summary>
    /// 
    /// </summary>
    internal class ODataEndpointMatcherPolicy : MatcherPolicy, IEndpointSelectorPolicy
    {
        /// <summary>
        /// 
        /// </summary>
        public override int Order => 1000 - 102;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="endpoints"></param>
        /// <returns></returns>
        public bool AppliesToEndpoints(IReadOnlyList<Endpoint> endpoints)
        {
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="httpContext"></param>
        /// <param name="candidates"></param>
        /// <returns></returns>
        public Task ApplyAsync(HttpContext httpContext, CandidateSet candidates)
        {
            // The goal of this method is to perform the final matching:
            // Map between route values matched by the template and the ones we want to expose to the action for binding. 
            // (tweaking the route values is fine here)
            // Invalidating the candidate if the key/function values are not valid/missing.
            // Perform overload resolution for functions by looking at the candidates and their metadata.
            for (var i = 0; i < candidates.Count; i++)
            {
                ref var candidate = ref candidates[i];
                if (!candidates.IsValidCandidate(i))
                {
                    continue;
                }

                var oDataMetadata = candidate.Endpoint.Metadata.OfType<ODataEndpointMetadata>().FirstOrDefault();
                if (oDataMetadata == null)
                {
                    continue;
                }

                var original = candidate.Endpoint.RequestDelegate;
                var name = candidate.Endpoint.DisplayName;

                var newEndpoint = new Endpoint(EndpointWithODataPath, candidate.Endpoint.Metadata, name);
                var originalValues = candidate.Values;
                var newValues = new RouteValueDictionary();
                foreach (var (key, value) in originalValues)
                {
                    //if (key.EndsWith(".Name"))
                    //{
                    //    var keyValue = originalValues[key.Replace(".Name", ".Value")];
                    //    var partName = originalValues[key];
                    //    var parameterName = oDataMetadata.ParameterMappings[oDataMetadata.ParameterMappings.Keys.Single(key => key.Name == (string)partName)];
                    //    newValues.Add(parameterName, keyValue);
                    //}

                    newValues.Add(key, value);
                }

                string oDataPathValue = GetODataRouteInfo(originalValues);
                HttpRequest request = httpContext.Request;

                // We need to call Uri.GetLeftPart(), which returns an encoded Url.
                // The ODL parser does not like raw values.
                Uri requestUri = new Uri(request.GetEncodedUrl());
                string requestLeftPart = requestUri.GetLeftPart(UriPartial.Path);
                string queryString = request.QueryString.HasValue ? request.QueryString.ToString() : null;

                //httpContext.RequestServices.GetRequiredService<ODataModelFactory>().Model = oDataMetadata.Model;
                IServiceProvider sp = GetServiceProvider(oDataMetadata.Model);

                // Call ODL to parse the Request URI.
                AspNet.OData.Routing.ODataPath path = ODataPathRouteConstraint.GetODataPath(oDataPathValue, requestLeftPart, queryString, () => sp);
                if (path != null)
                {
                    var odata = httpContext.Request.ODataFeature();
                    odata.Model = oDataMetadata.Model;
                    odata.IsEndpointRouting = true;
                    odata.RequestContainer = httpContext.RequestServices; // sp;
                    odata.Path = path;

                    candidates.SetValidity(i, true);
                }
                else
                {
                    candidates.ReplaceEndpoint(i, newEndpoint, newValues);
                }

                Task EndpointWithODataPath(HttpContext httpContext)
                {
                    var odataPath = oDataMetadata.ODataPathFactory(httpContext.GetRouteData().Values, oDataMetadata.ParameterMappings);
                    var odata = httpContext.Request.ODataFeature();
                    odata.Model = oDataMetadata.Model;
                    odata.IsEndpointRouting = true;
                    odata.RequestContainer = httpContext.RequestServices;

                    odata.Path = new AspNet.OData.Routing.ODataPath(odataPath)
                    {
                        Path = odataPath
                    };

                    odata.RouteName = name;
                    var prc = httpContext.RequestServices.GetRequiredService<IPerRouteContainer>();
                    if (!prc.HasODataRootContainer(name))
                    {
                        prc.AddRoute(odata.RouteName, "");
                    }

                    return original(httpContext);
                }
            }

            return Task.CompletedTask;
        }

        public IServiceProvider GetServiceProvider(IEdmModel model)
        {
            var builder = new DefaultContainerBuilder();

            builder.AddDefaultODataServices();

            // Set Uri resolver to by default enabling unqualified functions/actions and case insensitive match.
            builder.AddService(
                ServiceLifetime.Singleton,
                typeof(ODataUriResolver),
                sp => new UnqualifiedODataUriResolver { EnableCaseInsensitive = true });

            builder.AddService(ServiceLifetime.Singleton, typeof(IEdmModel), sp => model);

            builder.AddService<IODataPathHandler, DefaultODataPathHandler>(ServiceLifetime.Singleton);

            return builder.BuildContainer();
        }

        public static string GetODataRouteInfo(RouteValueDictionary values)
        {
            values.TryGetValue("functionImport", out object value);
            return value as string;
        }
    }
}
#endif