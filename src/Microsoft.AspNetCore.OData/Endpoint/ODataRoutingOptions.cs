#if !NETSTANDARD2_0
using System;
using System.Collections.Generic;
using Microsoft.OData.Edm;
using Microsoft.OData;
using System.Collections.Concurrent;
using Microsoft.AspNet.OData;
using Microsoft.OData.UriParser;

namespace Microsoft.AspNetCore.OData.Routing
{
    /// <summary>
    /// 
    /// </summary>
    public class ODataRoutingOptions
    {
        /// <summary>
        /// 
        /// </summary>
        public IDictionary<string, IEdmModel> Models { get; } = new Dictionary<string, IEdmModel>();

        /// <summary>
        /// 
        /// </summary>
        public IDictionary<string, Action<IContainerBuilder>> PreRoutePrividers = new Dictionary<string, Action<IContainerBuilder>>();

        /// <summary>
        /// 
        /// </summary>
        public IList<IODataControllerActionConvention> Conventions { get; } = new List<IODataControllerActionConvention>
        {
            new MetadataEndpointConvention(),
            new SingletonEndpointConvention(),
         //   new EntitySetRoutingConvention(),
            new OperationImportEndpointConvention()
        };

        /// <summary>
        /// 
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public ODataRoutingOptions AddModel(IEdmModel model)
        {
            return AddModel(string.Empty, model);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        public ODataRoutingOptions AddModel(string name, IEdmModel model)
        {
            return AddModel(name, model, null);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="model"></param>
        /// <param name="configureAction"></param>
        /// <returns></returns>
        public ODataRoutingOptions AddModel(string name, IEdmModel model, Action<IContainerBuilder> configureAction)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (Models.ContainsKey(name))
            {
                throw new Exception($"Contains the same name for the model: {name}");
            }

            Models[name] = model;
            PreRoutePrividers[name] = configureAction;
            return this;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="convention"></param>
        /// <returns></returns>
        public ODataRoutingOptions AddConvention(IODataControllerActionConvention convention)
        {
            Conventions.Add(convention);
            return this;
        }
    }

    //public class ODataRoutingConfig
    //{
    //    /// <summary>
    //    /// Routing conventions
    //    /// </summary>
    //    public IList<IODataRoutingConventionProvider> Conventions { get; } = new List<IODataRoutingConventionProvider>();
    //}
}
#endif