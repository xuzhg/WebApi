#if !NETSTANDARD2_0
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.OData.Edm;

namespace Microsoft.AspNetCore.OData.Routing
{
    /// <summary>
    /// 
    /// </summary>
    public class EntityEndpointConvention : IODataControllerActionConvention
    {
        /// <summary>
        /// 
        /// </summary>
        public int Order => 300;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <param name="controller"></param>
        /// <returns></returns>
        public virtual bool AppliesToController(ODataControllerContext context, ControllerModel controller)
        {
            return context?.EntitySet != null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <param name="action"></param>
        public virtual bool AppliesToAction(ODataControllerContext context, ActionModel action)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (context.EntitySet == null || action.Parameters.Count < 1)
            {
                // At lease one parameter for the key.
                return false;
            }

            IEdmEntitySet entitySet = context.EntitySet;
            var entityType = entitySet.EntityType();
            var entityTypeName = entitySet.EntityType().Name;
            var keys = entitySet.EntityType().Key().ToArray();

            string actionName = action.ActionMethod.Name;
            if ((actionName == "Get" ||
                actionName == $"Get{entityTypeName}" ||
                actionName == "Put" ||
                actionName == $"Put{entityTypeName}" ||
                actionName == "Patch" ||
                actionName == $"Patch{entityTypeName}" ||
                actionName == "Delete" ||
                actionName == $"Delete{entityTypeName}") &&
                keys.Length == action.Parameters.Count)
            {
                IList<MyODataSegment> segments = new List<MyODataSegment>
                {
                        new MyNavigationSourceSegment(entitySet),
                        new MyKeyTemplate(entityType, entitySet)
                };
                ODataTemplate template = new ODataTemplate(segments);

                // support key in parenthesis
                action.AddSelector(context.Prefix, context.Model, template);

                // support key as segment
                ODataTemplate newTemplate = template.Clone();
                newTemplate.KeyAsSegment = true;
                action.AddSelector(context.Prefix, context.Model, newTemplate);
                return true;
            }

            return false;
        }
    }
}
#endif
