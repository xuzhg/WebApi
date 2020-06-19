#if !NETSTANDARD2_0
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.AspNetCore.OData.Routing
{
    /// <summary>
    /// 
    /// </summary>
    internal static class EdmHelpers
    {
        public static T GetAttribute<T>(this ControllerModel controller)
        {
            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            T value = controller.Attributes.OfType<T>().FirstOrDefault();
            return value;
        }

        public static T GetAttribute<T>(this ActionModel action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            T value = action.Attributes.OfType<T>().FirstOrDefault();
            return value;
        }

        public static void AddSelector(this ActionModel action, string prefix, IEdmModel model, ODataTemplate template)
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

        public static IEnumerable<IEdmStructuredType> BaseTypes(
            this IEdmStructuredType structuralType)
        {
            IEdmStructuredType baseType = structuralType.BaseType;
            while (baseType != null)
            {
                yield return baseType;

                baseType = baseType.BaseType;
            }
        }

        public static IEnumerable<IEdmStructuredType> ThisAndBaseTypes(
            this IEdmStructuredType structuralType)
        {
            IEdmStructuredType baseType = structuralType;
            while (baseType != null)
            {
                yield return baseType;

                baseType = baseType.BaseType;
            }
        }

        public static IEnumerable<IEdmStructuredType> DerivedTypes(this IEdmStructuredType structuralType, IEdmModel model)
        {
            return model.FindAllDerivedTypes(structuralType);
        }

        public static IEdmStructuredType FindTypeInInheritance(this IEdmStructuredType structuralType, IEdmModel model, string typeName)
        {
            IEdmStructuredType baseType = structuralType;
            while (baseType != null)
            {
                if (GetName(baseType) == typeName)
                {
                    return baseType;
                }

                baseType = baseType.BaseType;
            }

            return model.FindAllDerivedTypes(structuralType).FirstOrDefault(c => GetName(c) == typeName);
        }

        private static string GetName(IEdmStructuredType type)
        {
            IEdmEntityType entityType = type as IEdmEntityType;
            if (entityType != null)
            {
                return entityType.Name;
            }

            return ((IEdmComplexType)type).Name;
        }
    }
}
#endif