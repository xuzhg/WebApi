#if !NETSTANDARD2_0
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.AspNetCore.OData.Routing
{
    /// <summary>
    /// 
    /// </summary>
    public class EntityEndpointConvention : NavigationSourceEndpointConvention
    {
        /// <summary>
        /// 
        /// </summary>
        public override int Order => -1000 + 100;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="model"></param>
        /// <param name="action"></param>
        public override bool AppliesToAction(string prefix, IEdmModel model, ActionModel action)
        {
            if (model.EntityContainer == null)
            {
                return false;
            }

            IEdmEntitySet entitySet = NavigationSource as IEdmEntitySet;
            if (entitySet == null)
            {
                return false;
            }

            if (action.Parameters.Count < 1)
            {
                return false;
            }

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
                        new MyNavigationSourceSegment(NavigationSource),
                        new MyKeyTemplate(entityType, NavigationSource)
                };
                ODataTemplate template = new ODataTemplate(segments);

                // support key in parenthesis
                AddSelector(prefix, model, action, template);

                // support key as segment
                ODataTemplate newTemplate = template.Clone();
                newTemplate.KeyAsSegment = true;
                AddSelector(prefix, model, action, newTemplate);
                return true;
            }

            return false;
        }

        static void AddSelector(string prefix, IEdmModel model, ActionModel action, ODataTemplate template)
        {
            SelectorModel selectorModel = action.Selectors.FirstOrDefault(s => s.AttributeRouteModel == null);
            if (selectorModel == null)
            {
                selectorModel = new SelectorModel();
                action.Selectors.Add(selectorModel);
            }

            string templateStr = string.IsNullOrEmpty(prefix) ? template.Template : $"{prefix}/{template.Template}";

            selectorModel.AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(templateStr) { Name = templateStr });
            selectorModel.EndpointMetadata.Add(new ODataEndpointMetadata(prefix, model, template));
        }
    }
}
#endif
