#if !NETSTANDARD2_0
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.OData.Edm;
using System;

namespace Microsoft.AspNetCore.OData.Routing
{
    /// <summary>
    /// 
    /// </summary>
    public class EntitySetEndpointConvention : IODataControllerActionConvention
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

            if (context.EntitySet == null)
            {
                return false;
            }

            IEdmEntitySet entitySet = context.EntitySet;

            if (action.Parameters.Count != 0)
            {
                // TODO: improve here to accept other parameters, for example ODataQueryOptions<T>
                return false;
            }

            string actionName = action.ActionMethod.Name;

            if (actionName == "Get" ||
                actionName == $"Get{entitySet.Name}")
            {
                ODataTemplate template = new ODataTemplate(new MyEntitySetSegment(entitySet));
                action.AddSelector(context.Prefix, context.Model, template);

                // $count
                template = new ODataTemplate(new MyEntitySetSegment(entitySet), MyCountSegment.Instance);
                action.AddSelector(context.Prefix, context.Model, template);
                return true;
            }
            else if (actionName == "Post" ||
                actionName == $"Post{entitySet.EntityType().Name}")
            {
                ODataTemplate template = new ODataTemplate(new MyEntitySetSegment(entitySet));
                action.AddSelector(context.Prefix, context.Model, template);
                return true;
            }
            else
            {
                // process the derive type (cast)
                // search all derived types
            }

            return false;
        }
    }
}
#endif
