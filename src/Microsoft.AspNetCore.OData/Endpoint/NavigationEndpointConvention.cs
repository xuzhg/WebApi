#if !NETSTANDARD2_0
using Microsoft.AspNet.OData;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.AspNetCore.OData.Routing
{
    /// <summary>
    /// 
    /// </summary>
    public class NavigationEndpointConvention : NavigationSourceEndpointConvention
    {
        /// <summary>
        /// 
        /// </summary>
        public override int Order => -1000 + 400;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="model"></param>
        /// <param name="action"></param>
        public override bool AppliesToAction(string prefix, IEdmModel model, ActionModel action)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            Debug.Assert(NavigationSource != null);
            

            string actionName = action.ActionMethod.Name;

            string method = Split(actionName, out string property, out string cast, out string declared);
            if (method == null || string.IsNullOrEmpty(property))
            {
                return false;
            }

            IEdmEntityType entityType = NavigationSource.EntityType();

            IEdmEntityType declaredEntityType = null;
            if (declared != null)
            {
                declaredEntityType = entityType.FindTypeInInheritance(model, declared) as IEdmEntityType;
                if (declaredEntityType == null)
                {
                    return false;
                }

                if (declaredEntityType == entityType)
                {
                    declaredEntityType = null;
                }
            }

            bool hasKeyParameter = HasKeyParameter(entityType, action);
            IEdmSingleton singleton = NavigationSource as IEdmSingleton;
            if (singleton != null && hasKeyParameter)
            {
                // Singleton, don't allow for the keys
                return false;
            }

            IEdmProperty edmProperty = entityType.FindProperty(property);
            if (edmProperty != null && edmProperty.PropertyKind == EdmPropertyKind.Structural)
            {
                // only process structural property
                IEdmStructuredType castComplexType = null;
                if (cast != null)
                {
                    IEdmTypeReference propertyType = edmProperty.Type;
                    if (propertyType.IsCollection())
                    {
                        propertyType = propertyType.AsCollection().ElementType();
                    }
                    if (!propertyType.IsComplex())
                    {
                        return false;
                    }

                    castComplexType = propertyType.ToStructuredType().FindTypeInInheritance(model, cast);
                    if (castComplexType == null)
                    {
                        return false;
                    }
                }

                IList<MyODataSegment> segments = new List<MyODataSegment>
                {
                    new MyNavigationSourceSegment(NavigationSource)
                };
                if (hasKeyParameter)
                {
                    segments.Add(new MyKeyTemplate(entityType, NavigationSource));
                }
                if (declaredEntityType != null && declaredEntityType != entityType)
                {
                    segments.Add(new MyCastSegment(declaredEntityType, NavigationSource));
                }

                segments.Add(new MyPropertySegment((IEdmStructuralProperty)edmProperty));

                ODataTemplate template = new ODataTemplate(segments);

                SelectorModel selectorModel = action.Selectors.FirstOrDefault(s => s.AttributeRouteModel == null);
                if (selectorModel == null)
                {
                    selectorModel = new SelectorModel();
                    action.Selectors.Add(selectorModel);
                }

                string templateStr = string.IsNullOrEmpty(prefix) ? template.Template : $"{prefix}/{template.Template}";
                selectorModel.AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(templateStr) { Name = templateStr });
                selectorModel.EndpointMetadata.Add(new ODataEndpointMetadata(prefix, model, template));
                return true;
            }
            else
            {
                // map to a static action like:  <method>Property(int key, string property)From<...>
                if (property == "Property" && cast == null)
                {
                    if (action.Parameters.Any(p => p.ParameterInfo.Name == "property" && p.ParameterType == typeof(string)))
                    {
                        // we find a static method mapping for all property
                        // we find a action route
                        IList<MyODataSegment> segments = new List<MyODataSegment>
                        {
                            new MyNavigationSourceSegment(NavigationSource)
                        };
                        if (hasKeyParameter)
                        {
                            segments.Add(new MyKeyTemplate(entityType, NavigationSource));
                        }
                        if (declaredEntityType != null)
                        {
                            segments.Add(new MyCastSegment(declaredEntityType, NavigationSource));
                        }

                        segments.Add(new MyPropertyTemplateSegment(entityType));

                        ODataTemplate template = new ODataTemplate(segments);

                        SelectorModel selectorModel = action.Selectors.FirstOrDefault(s => s.AttributeRouteModel == null);
                        if (selectorModel == null)
                        {
                            selectorModel = new SelectorModel();
                            action.Selectors.Add(selectorModel);
                        }

                        string templateStr = string.IsNullOrEmpty(prefix) ? template.Template : $"{prefix}/{template.Template}";
                        selectorModel.AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(templateStr) { Name = templateStr });
                        selectorModel.EndpointMetadata.Add(new ODataEndpointMetadata(prefix, model, template));
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasKeyParameter(IEdmEntityType entityType, ActionModel action)
        {
            var keys = entityType.Key().ToArray();
            if (keys.Length == 1)
            {
                return action.Parameters.Any(p => p.ParameterInfo.Name == "key");
            }
            else
            {
                foreach (var key in keys)
                {
                    string keyName = $"key{key.Name}";
                    if (!action.Parameters.Any(p => p.ParameterInfo.Name == keyName))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        private static string Split(string actionName, out string property, out string cast, out string declared)
        {
            string method = null;
            property = null;
            cast = null;
            declared = null;

            string text;
            // Get{PropertyName}Of<cast>From<declard>
            if (actionName.StartsWith("Get", StringComparison.OrdinalIgnoreCase))
            {
                method = "Get";
                text = actionName.Substring(3);
            }
            else if (actionName.StartsWith("PutTo", StringComparison.OrdinalIgnoreCase))
            {
                method = "PutTo";
                text = actionName.Substring(5);
            }
            else if (actionName.StartsWith("PatchTo", StringComparison.OrdinalIgnoreCase))
            {
                method = "PatchTo";
                text = actionName.Substring(7);
            }
            else if (actionName.StartsWith("DeleteTo", StringComparison.OrdinalIgnoreCase))
            {
                method = "DeleteTo";
                text = actionName.Substring(8);
            }
            else
            {
                return null;
            }

            int index = text.IndexOf("Of", StringComparison.OrdinalIgnoreCase);
            if (index > 0)
            {
                property = text.Substring(0, index);
                text = text.Substring(index + 2);
                cast = Match(text, out declared);
            }
            else
            {
                property = Match(text, out declared);
            }

            return method;
        }

        private static string Match(string text, out string declared)
        {
            declared = null;
            int index = text.IndexOf("From");
            if (index > 0)
            {
                declared = text.Substring(index + 4);
                return text.Substring(0, index);
            }

            return text;
        }
    }
}
#endif