
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.OData.Edm;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNet.OData;
using System.Reflection;

namespace Microsoft.AspNetCore.OData.Routing
{
    /// <summary>
    /// 
    /// </summary>
    public class ODataControllerContext
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="model"></param>
        internal ODataControllerContext(string prefix, IEdmModel model)
        {
            Prefix = prefix;
            Model = model;
        }
        internal ODataControllerContext(string prefix, IEdmModel model, IEdmEntitySet entitySet)
            : this(prefix, model)
        {
            EntitySet = entitySet;
        }

        internal ODataControllerContext(string prefix, IEdmModel model, IEdmSingleton singleton)
            : this(prefix, model)
        {
            Singleton = singleton;
        }

        /// <summary>
        /// 
        /// </summary>
        public string Prefix { get; }

        /// <summary>
        /// 
        /// </summary>
        public IEdmModel Model { get; }

        //public bool IsDone { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public IEdmEntitySet EntitySet { get; }

        /// <summary>
        /// 
        /// </summary>
        public IEdmSingleton Singleton { get; }

        // for others extra information
        // public IDictionary<object, object> properties { get; } = new Dictionary<object, object>();
    }

    internal interface IODataEndpointConvention
    {
        bool AppliesToController(ODataControllerContext context, ControllerModel controller);

        bool AppliesToAction(ODataControllerContext context, ActionModel action);
    }

    internal class ODataActionContext
    {
        public void Test(IList<ControllerModel> controllers, IEnumerable<IODataEndpointConvention> conventions)
        {
            string prefix = null;
            IEdmModel model = null;
            foreach (var controller in controllers)
            {
                // Add here
                //

                // Get conventions for all this controller
                ODataControllerContext context = BuildContext(prefix, model, controller);

                IODataEndpointConvention[] newConventions = 
                    conventions.Where(c => c.AppliesToController(context, controller)).ToArray();

                if (newConventions.Length > 0)
                {
                    foreach (var action in controller.Actions)
                    {
                        foreach (var con in newConventions)
                        {
                            if (con.AppliesToAction(context, action))
                            {
                                break;
                            }
                        }
                    }
                }
            }
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
    }

    


    /// <summary>
    /// 
    /// </summary>
    public interface IODataControllerActionConvention
    {
        /// <summary>
        /// 
        /// </summary>
        int Order { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <param name="controller"></param>
        /// <returns></returns>
        bool AppliesToController(ODataControllerContext context, ControllerModel controller);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <param name="action"></param>
        bool AppliesToAction(ODataControllerContext context, ActionModel action);
    }

    internal interface IODataActionConvention
    {
        /// <summary>
        /// 
        /// </summary>
        int Order { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="model"></param>
        /// <param name="action"></param>
        void AppliesToAction(string prefix, IEdmModel model, ActionModel action);
    }

    internal interface IODataEndpointConventionProvider
    {
        /// <summary>
        /// 
        /// </summary>
        int Order { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="model"></param>
        /// <param name="controller"></param>
        /// <returns></returns>
        bool AppliesToController(string prefix, IEdmModel model, ControllerModel controller);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="model"></param>
        /// <param name="action"></param>
        void AppliesToAction(string prefix, IEdmModel model, ActionModel action);
    }

    internal class DefaultODataEndpointConventionProvider : IODataEndpointConventionProvider
    {
        private readonly IODataActionConvention[] _actionConventions;

        public DefaultODataEndpointConventionProvider(
            IEnumerable<IODataActionConvention> conventions)
        {
            _actionConventions = conventions.OrderBy(p => p.Order).ToArray();
        }

        public int Order => 0;

        public bool AppliesToController(string prefix, IEdmModel model, ControllerModel controller)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            if (model.EntityContainer == null)
            {
                return false;
            }

            return false;
        }

        public void AppliesToAction(string prefix, IEdmModel model, ActionModel action)
        {
            throw new System.NotImplementedException();
        }
    }
}
