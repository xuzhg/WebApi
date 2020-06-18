#if !NETSTANDARD2_0
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.AspNetCore.OData.Routing
{
    /// <summary>
    /// 
    /// </summary>
    public class SingletonEndpointConvention : IODataControllerActionConvention
    {
        /// <summary>
        /// 
        /// </summary>
        public int Order => -1000 + 100;

        /// <summary>
        /// used for cache
        /// </summary>
        internal IEdmSingleton Singleton { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="model"></param>
        /// <param name="controller"></param>
        /// <returns></returns>
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

            string controllerName = controller.ControllerName;
            IEdmSingleton singleton = model.EntityContainer?.FindSingleton(controllerName);

            // Cached the singleton, because we call this method first, then AppliesToAction
            // FindSingleton maybe time consuming.
            Singleton = singleton;
            return singleton != null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="model"></param>
        /// <param name="action"></param>
        public bool AppliesToAction(string prefix, IEdmModel model, ActionModel action)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            // use the cached
            Debug.Assert(Singleton != null);
            string singletonName = Singleton.Name;

            string actionMethodName = action.ActionMethod.Name;
            if (IsSupportedActionName(actionMethodName, singletonName))
            {
                var template = string.IsNullOrEmpty(prefix) ? singletonName : $"{prefix}/{singletonName}";

                SelectorModel selectorModel = action.Selectors.FirstOrDefault(s => s.AttributeRouteModel == null);
                if (selectorModel == null)
                {
                    selectorModel = new SelectorModel();
                    action.Selectors.Add(selectorModel);
                }

                selectorModel.AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(template) { Name = template });
                selectorModel.EndpointMetadata.Add(new ODataEndpointMetadata(prefix, model, null, (_, __) => new ODataPath(new SingletonSegment(Singleton))));

                // processed
                return true;
            }

            // type cast
            // Get{SingletonName}From{EntityTypeName} or GetFrom{EntityTypeName}
            int index = actionMethodName.IndexOf("From", StringComparison.Ordinal);
            if (index == -1)
            {
                return false;
            }

            string actionPrefix = actionMethodName.Substring(0, index);
            if (IsSupportedActionName(actionPrefix, singletonName))
            {
                IEdmEntityType entityType = Singleton.EntityType();
                string castTypeName = actionMethodName.Substring(index + 4);

                // Shall we cast to base type and the type itself? I think yes.
                IEdmEntityType baseType = entityType;
                while (baseType != null)
                {
                    if (baseType.Name == castTypeName)
                    {
                        AddSelector(prefix, model, action, Singleton, baseType);
                        return true;
                    }

                    baseType = baseType.BaseEntityType();
                }

                // shall we cast to derived type
                IEdmEntityType castType = model.FindAllDerivedTypes(entityType).OfType<IEdmEntityType>().FirstOrDefault(c => c.Name == castTypeName);
                if (castType != null)
                {
                    AddSelector(prefix, model, action, Singleton, castType);
                    return true;
                }
            }

            return false;
        }

        private static void AddSelector(string prefix, IEdmModel model, ActionModel action, IEdmSingleton singleton, IEdmEntityType castType)
        {
            SelectorModel selectorModel = action.Selectors.FirstOrDefault(s => s.AttributeRouteModel == null);
            if (selectorModel == null)
            {
                selectorModel = new SelectorModel();
                action.Selectors.Add(selectorModel);
            }

            // Me/Namespace.VipUser
            string template = $"{singleton.Name}/{castType.FullTypeName()}";
            selectorModel.AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(template) { Name = template });
            selectorModel.EndpointMetadata.Add(
                new ODataEndpointMetadata(prefix, model, null, (_, __) => new ODataPath(
                    new SingletonSegment(singleton),
                    new TypeSegment(castType, singleton.EntityType(), singleton))));
        }

        private static bool IsSupportedActionName(string actionName, string singletonName)
        {
            return actionName == "Get" || actionName == $"Get{singletonName}" ||
                actionName == "Put" || actionName == $"Put{singletonName}" ||
                actionName == "Patch" || actionName == $"Patch{singletonName}";
        }
    }
}
#endif