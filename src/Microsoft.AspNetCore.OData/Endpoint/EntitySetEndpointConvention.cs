#if !NETSTANDARD2_0
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System.Linq;

namespace Microsoft.AspNetCore.OData.Routing
{
    /// <summary>
    /// 
    /// </summary>
    public class EntitySetRoutingConventionProvider : IODataControllerActionConvention
    {
        /// <summary>
        /// 
        /// </summary>
        public int Order => -1000 + 100;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="model"></param>
        /// <param name="controller"></param>
        public bool AppliesToController(string prefix, IEdmModel model, ControllerModel controller)
        {
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="model"></param>
        /// <param name="action"></param>
        public bool AppliesToAction(string prefix, IEdmModel model, ActionModel action)
        {
            if (model.EntityContainer == null)
            {
                return false;
            }

            string controllerName = action.Controller.ControllerName;
            IEdmEntitySet entitySet = model.EntityContainer.FindEntitySet(controllerName);
            if (entitySet == null)
            {
                return false;
            }

            if (action.Parameters.Count != 0)
            {
                return false;
            }

            string actionName = action.ActionMethod.Name;

            if (actionName == "Get" ||
                actionName == $"Get{entitySet.Name}")
            {
                var template = string.IsNullOrEmpty(prefix) ? entitySet.Name : $"{prefix}/{entitySet.Name}";

                SelectorModel selectorModel = action.Selectors.FirstOrDefault(s => s.AttributeRouteModel == null);
                if (selectorModel == null)
                {
                    selectorModel = new SelectorModel();
                    action.Selectors.Add(selectorModel);
                }

                selectorModel.AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(template) { Name = template });
                selectorModel.EndpointMetadata.Add(new ODataEndpointMetadata(prefix, model, null, (_, __) => new ODataPath(new EntitySetSegment(entitySet))));

                //// $count
                template = string.IsNullOrEmpty(prefix) ? $"{entitySet.Name}/$count" : $"{prefix}/{entitySet.Name}/$count";
                selectorModel = action.Selectors.FirstOrDefault(s => s.AttributeRouteModel == null);
                if (selectorModel == null)
                {
                    selectorModel = new SelectorModel();
                    action.Selectors.Add(selectorModel);
                }

                selectorModel.AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(template) { Name = template });
                selectorModel.EndpointMetadata.Add(new ODataEndpointMetadata(prefix, model, null, (_, __) => new ODataPath(new EntitySetSegment(entitySet), CountSegment.Instance)));
                return true;
            }
            else if (actionName == "Post" ||
                actionName == $"Post{entitySet.EntityType().Name}")
            {
                var template = string.IsNullOrEmpty(prefix) ? entitySet.Name : $"{prefix}/{entitySet.Name}";

                SelectorModel selectorModel = action.Selectors.FirstOrDefault(s => s.AttributeRouteModel == null);
                if (selectorModel == null)
                {
                    selectorModel = new SelectorModel();
                    action.Selectors.Add(selectorModel);
                }

                selectorModel.AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(template) { Name = template });
                selectorModel.EndpointMetadata.Add(new ODataEndpointMetadata(prefix, model, null, (_, __) => new ODataPath(new EntitySetSegment(entitySet))));

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
