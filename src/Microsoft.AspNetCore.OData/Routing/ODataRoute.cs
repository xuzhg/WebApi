// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using Microsoft.OData.Edm;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using System.Threading;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.Extensions.FileProviders;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Controllers;
using System.Linq;
using Microsoft.AspNet.OData.Extensions;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNet.OData.Interfaces;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.OData.UriParser;
#if NETCOREAPP3_0
using System;
    using Microsoft.AspNetCore.Routing.Patterns;
#endif

namespace Microsoft.AspNet.OData.Routing
{
#if NETCOREAPP3_0

    internal class ODatEndpoint : Endpoint
    {
        public ODatEndpoint(RequestDelegate requestDelegate, EndpointMetadataCollection metadata, string displayName)
            : base(requestDelegate, metadata, displayName)
        {
        }
    }

    internal class ODataEndpointRoute
    {
        public RoutePattern Pattern;
        public string RouteName;
        public string RoutePrefix;
        public RouteValueDictionary DataTokens;
        public int Order;
        public IEdmModel Model;
        //   public readonly IReadOnlyList<Action<EndpointBuilder>> Conventions;

        public string GetRoutePattern()
        {
            return String.IsNullOrEmpty(RoutePrefix) ?
                ODataRouteConstants.ODataPathTemplate :
                RoutePrefix + '/' + ODataRouteConstants.ODataPathTemplate;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class ODataEndpointDataSource : EndpointDataSource
    {
        private readonly IActionDescriptorCollectionProvider _actions;

        /// <summary>
        /// 
        /// </summary>
        protected readonly object Lock = new object();

        private List<Endpoint> _endpoints;
        private IChangeToken _changeToken;
        private CancellationTokenSource _cancellationTokenSource;
        /// <summary>
        /// 
        /// </summary>
        public ODataEndpointDataSource(IActionDescriptorCollectionProvider actions)
        {
            _routes = new List<ODataEndpointRoute>();
            _actions = actions;
        }

        /// <summary>
        /// 
        /// </summary>
        public override IReadOnlyList<Endpoint> Endpoints
        {
            get
            {
                Initialize();
                return _endpoints;
            }
        }

        private void Initialize()
        {
            if (_endpoints == null)
            {
                lock (Lock)
                {
                    if (_endpoints == null)
                    {
                        UpdateEndpoints();
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="actions"></param>
        /// <returns></returns>
        protected virtual List<Endpoint> CreateEndpoints(IReadOnlyList<ActionDescriptor> actions)
        {
            var endpoints = new List<Endpoint>();

            foreach (var route in _routes)
            {
                RoutePattern routePattern = RoutePatternFactory.Parse(route.GetRoutePattern());

                RouteEndpointBuilder routeEndpointBuilder = new RouteEndpointBuilder(CreateRequestDelegate(), routePattern, 0);
                endpoints.Add(routeEndpointBuilder.Build());
            }

            return endpoints;
        }

        internal Task ODataRequestDelegate(HttpContext context)
        {

            return Task.CompletedTask;
        }

        private static RequestDelegate CreateRequestDelegate()
        {
            // We don't want to close over the Invoker Factory in ActionEndpointFactory as
            // that creates cycles in DI. Since we're creating this delegate at startup time
            // we don't want to create all of the things we use at runtime until the action
            // actually matches.
            //
            // The request delegate is already a closure here because we close over
            // the action descriptor.
            IActionInvokerFactory invokerFactory = null;

            return (context) =>
            {
                var syncIOFeature = context.Features.Get<IHttpBodyControlFeature>();
                if (syncIOFeature != null)
                {
                    syncIOFeature.AllowSynchronousIO = true;
                }

                var endpoint = context.GetEndpoint();
                var dataTokens = endpoint.Metadata.GetMetadata<IDataTokensMetadata>();

                var routeData = new RouteData();
                routeData.PushState(router: null, context.Request.RouteValues, new RouteValueDictionary(dataTokens?.DataTokens));

                routeData.Values["controller"] = "Customers";
                routeData.Values["action"] = "Get";

                Match(context, "odata", routeData.Values, RouteDirection.IncomingRequest);

                IActionDescriptorCollectionProvider provider = context.RequestServices.GetRequiredService<IActionDescriptorCollectionProvider>();
                IEnumerable<ControllerActionDescriptor> actionDescriptors = provider
                    .ActionDescriptors.Items.OfType<ControllerActionDescriptor>()
                    .Where(c => c.ControllerName == "Customers");

                IODataFeature odataFeature = context.ODataFeature();
                ODataPath path = odataFeature.Path;

                var actions = actionDescriptors.Where(c => String.Equals(c.ActionName, "Get", StringComparison.OrdinalIgnoreCase));
                ActionDescriptor action;
                if (path.Segments.Last() is KeySegment)
                {
                    action = actions.FirstOrDefault(c => c.Parameters.Any(a => a.Name == "key"));
                    KeySegment keySegment = path.Segments.Last() as KeySegment;
                    routeData.Values["key"] = keySegment.Keys.FirstOrDefault().Value;
                }
                else
                {
                    action = actionDescriptors.FirstOrDefault(c => String.Equals(c.ActionName, "Get", StringComparison.OrdinalIgnoreCase));
                }

                // Don't close over the ActionDescriptor, that's not valid for pages.
               // var action = endpoint.Metadata.GetMetadata<ActionDescriptor>();
                var actionContext = new ActionContext(context, routeData, action);

                if (invokerFactory == null)
                {
                    invokerFactory = context.RequestServices.GetRequiredService<IActionInvokerFactory>();
                }

                var invoker = invokerFactory.CreateInvoker(actionContext);
                return invoker.InvokeAsync();
            };
        }

        internal static bool Match(HttpContext httpContext, string routeName, RouteValueDictionary values, RouteDirection routeDirection)
        {
            if (routeDirection == RouteDirection.IncomingRequest)
            {
                ODataPath path = null;
                HttpRequest request = httpContext.Request;

                object oDataPathValue;
                if (values.TryGetValue(ODataRouteConstants.ODataPath, out oDataPathValue))
                {
                    // We need to call Uri.GetLeftPart(), which returns an encoded Url.
                    // The ODL parser does not like raw values.
                    Uri requestUri = new Uri(request.GetEncodedUrl());
                    string requestLeftPart = requestUri.GetLeftPart(UriPartial.Path);
                    string queryString = request.QueryString.HasValue ? request.QueryString.ToString() : null;

                    path = ODataPathRouteConstraint.GetODataPath(oDataPathValue as string, requestLeftPart, queryString, () => request.CreateRequestContainer(routeName));
                }

                if (path != null)
                {
                    // Set all the properties we need for routing, querying, formatting
                    IODataFeature odataFeature = httpContext.ODataFeature();
                    odataFeature.Path = path;
                    odataFeature.RouteName = routeName;

                    return true;
                }

                // The request doesn't match this route so dispose the request container.
                request.DeleteRequestContainer(true);
                return false;
            }
            else
            {
                // This constraint only applies to incoming request.
                return true;
            }
        }

        private void UpdateEndpoints()
        {
            lock (Lock)
            {
                var endpoints = CreateEndpoints(_actions.ActionDescriptors.Items);

                // See comments in DefaultActionDescriptorCollectionProvider. These steps are done
                // in a specific order to ensure callers always see a consistent state.

                // Step 1 - capture old token
                var oldCancellationTokenSource = _cancellationTokenSource;

                // Step 2 - update endpoints
                _endpoints = endpoints;

                // Step 3 - create new change token
                _cancellationTokenSource = new CancellationTokenSource();
                _changeToken = new CancellationChangeToken(_cancellationTokenSource.Token);

                // Step 4 - trigger old token
                oldCancellationTokenSource?.Cancel();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override IChangeToken GetChangeToken() => NullChangeToken.Singleton;

        private readonly List<ODataEndpointRoute> _routes;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="routeName"></param>
        /// <param name="routePrefix"></param>
        /// <param name="model"></param>
        public void AddRoute(string routeName, string routePrefix, IEdmModel model)
        {
            _routes.Add(new ODataEndpointRoute
            {
                RouteName = routeName,
                RoutePrefix = routePrefix,
                Model = model
            }); ;
        }
    }
#else
#endif

    /// <summary>
    /// A route implementation for OData routes. It supports passing in a route prefix for the route as well
    /// as a path constraint that parses the request path as OData.
    /// </summary>
    public partial class ODataRoute : Route
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ODataRoute"/> class.
        /// </summary>
        /// <param name="target">The target router.</param>
        /// <param name="routeName">The route name.</param>
        /// <param name="routePrefix">The route prefix.</param>
        /// <param name="routeConstraint">The OData route constraint.</param>
        /// <param name="resolver">The inline constraint resolver.</param>
        /// <remarks>This signature uses types that are AspNetCore-specific.</remarks>
        public ODataRoute(IRouter target, string routeName, string routePrefix, ODataPathRouteConstraint routeConstraint, IInlineConstraintResolver resolver)
            : this(target, routeName, routePrefix, (IRouteConstraint)routeConstraint, resolver)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ODataRoute"/> class.
        /// </summary>
        /// <param name="target">The target router.</param>
        /// <param name="routeName">The route name.</param>
        /// <param name="routePrefix">The route prefix.</param>
        /// <param name="routeConstraint">The OData route constraint.</param>
        /// <param name="resolver">The inline constraint resolver.</param>
        /// <remarks>This signature uses types that are AspNetCore-specific.</remarks>
        public ODataRoute(IRouter target, string routeName, string routePrefix, IRouteConstraint routeConstraint, IInlineConstraintResolver resolver)
            : base(target, routeName, GetRouteTemplate(routePrefix), defaults: null, constraints: null, dataTokens: null, inlineConstraintResolver: resolver)
        {
            RouteConstraint = routeConstraint;
            Initialize(routePrefix, routeConstraint as ODataPathRouteConstraint);

            if (routeConstraint != null)
            {
                Constraints.Add(ODataRouteConstants.ConstraintName, routeConstraint);
            }

            Constraints.Add(ODataRouteConstants.VersionConstraintName, new ODataVersionConstraint());
        }

        /// <summary>
        /// Gets the <see cref="IRouteConstraint"/> on this route.
        /// </summary>
        /// <remarks>This signature uses types that are AspNetCore-specific.</remarks>
        public IRouteConstraint RouteConstraint { get; private set; }

        /// <inheritdoc />
        /// <remarks>This signature uses types that are AspNetCore-specific.</remarks>
        public override VirtualPathData GetVirtualPath(VirtualPathContext context)
        {
            // Fast path link generation where we recognize an OData route of the form "prefix/{*odataPath}".
            // Link generation using HttpRoute.GetVirtualPath can consume up to 30% of processor time
            object odataPathValue;
            if (context.Values.TryGetValue(ODataRouteConstants.ODataPath, out odataPathValue))
            {
                string odataPath = odataPathValue as string;
                if (odataPath != null)
                {
                    // Try to generate an optimized direct link
                    // Otherwise, fall back to the base implementation
                    return CanGenerateDirectLink
                        ? GenerateLinkDirectly(odataPath)
                        : base.GetVirtualPath(context);
                }
            }

            return null;
        }

        /// <remarks>This signature uses types that are AspNetCore-specific.</remarks>
        internal VirtualPathData GenerateLinkDirectly(string odataPath)
        {
            Contract.Assert(odataPath != null);
            Contract.Assert(CanGenerateDirectLink);

            string link = CombinePathSegments(RoutePrefix, odataPath);
            link = UriEncode(link);
            return new VirtualPathData(this, link);
        }
    }
}