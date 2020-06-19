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