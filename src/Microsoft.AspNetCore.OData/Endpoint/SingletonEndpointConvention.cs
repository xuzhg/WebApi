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
        public virtual int Order => 200;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <param name="controller"></param>
        /// <returns></returns>
        public virtual bool AppliesToController(ODataControllerContext context, ControllerModel controller)
        {
            return context?.Singleton != null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <param name="action"></param>
        public bool AppliesToAction(ODataControllerContext context, ActionModel action)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            // use the cached
            Debug.Assert(context.Singleton != null);
            string singletonName = context.Singleton.Name;
            string prefix = context.Prefix;
            IEdmModel model = context.Model;

            string actionMethodName = action.ActionMethod.Name;
            if (IsSupportedActionName(actionMethodName, singletonName))
            {
                ODataTemplate template = new ODataTemplate(new MySingletonSegment(context.Singleton));
                action.AddSelector(context.Prefix, context.Model, template);

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
                IEdmEntityType entityType = context.Singleton.EntityType();
                string castTypeName = actionMethodName.Substring(index + 4);

                // Shall we cast to base type and the type itself? I think yes.
                IEdmEntityType baseType = entityType;
                while (baseType != null)
                {
                    if (baseType.Name == castTypeName)
                    {
                        ODataTemplate template = new ODataTemplate(new MySingletonSegment(context.Singleton),
                            new MyCastSegment(baseType, context.Singleton));
                        action.AddSelector(context.Prefix, context.Model, template);

                        return true;
                    }

                    baseType = baseType.BaseEntityType();
                }

                // shall we cast to derived type
                IEdmEntityType castType = model.FindAllDerivedTypes(entityType).OfType<IEdmEntityType>().FirstOrDefault(c => c.Name == castTypeName);
                if (castType != null)
                {
                    ODataTemplate template = new ODataTemplate(new MySingletonSegment(context.Singleton),
                        new MyCastSegment(castType, context.Singleton));
                    action.AddSelector(context.Prefix, context.Model, template);

                    return true;
                }
            }

            return false;
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