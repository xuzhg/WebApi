using Microsoft.AspNet.OData.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Validation;
using Microsoft.OData.UriParser;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using ODataPath = Microsoft.OData.UriParser.ODataPath;

namespace Microsoft.AspNetCore.OData.Routing
{
    internal class ODataTemplate
    {
        private string _template;
        private readonly ReadOnlyCollection<MyODataSegment> _segments;

        public ODataTemplate(params MyODataSegment[] segments)
            : this(segments as IEnumerable<MyODataSegment>)
        {

        }

        public ODataTemplate(IEnumerable<MyODataSegment> segments)
        {
            var oDataPathSegments = segments as IList<MyODataSegment> ?? segments.ToList();
            _segments = new ReadOnlyCollection<MyODataSegment>(oDataPathSegments);
        }

        public bool KeyAsSegment { get; set; }

        public string Template
        {
            get
            {
                if (_template == null)
                {
                    _template = CalculateTemplate();
                }
                return _template;
            }
        }

        public ODataPath GenerateODataPath(IEdmModel model, RouteValueDictionary routeValue, QueryString queryString)
        {
            // calculate every time
            IList<ODataPathSegment> oSegments = new List<ODataPathSegment>();
            IEdmNavigationSource previousNavigationSource = null;
            foreach (var segment in _segments)
            {
                ODataPathSegment odataSegment = segment.ProcessRouteValue(model, previousNavigationSource, routeValue, queryString);
                if (odataSegment == null)
                {
                    return null;
                }

                oSegments.Add(odataSegment);
                previousNavigationSource = GetTargetNavigationSource(previousNavigationSource, odataSegment);
            }

            return new ODataPath(oSegments);
        }

        private static IEdmNavigationSource GetTargetNavigationSource(IEdmNavigationSource previous, ODataPathSegment segment)
        {
            if (segment == null)
            {
                return null;
            }

            EntitySetSegment entitySet = segment as EntitySetSegment;
            if (entitySet != null)
            {
                return entitySet.EntitySet;
            }

            SingletonSegment singleton = segment as SingletonSegment;
            if (singleton != null)
            {
                return singleton.Singleton;
            }

            TypeSegment cast = segment as TypeSegment;
            if (cast != null)
            {
                return cast.NavigationSource;
            }

            KeySegment key = segment as KeySegment;
            if (key != null)
            {
                return key.NavigationSource;
            }

            OperationSegment opertion = segment as OperationSegment;
            if (opertion != null)
            {
                return opertion.EntitySet;
            }

            OperationImportSegment import = segment as OperationImportSegment;
            if (import != null)
            {
                return import.EntitySet;
            }

            PropertySegment property = segment as PropertySegment;
            if (property != null)
            {
                return previous; // for property, return the previous, or return null????
            }

            throw new Exception("Not supported segment in endpoint routing convention!");
        }

        private string CalculateTemplate()
        {
            int index = 0;
            StringBuilder sb = new StringBuilder();
            foreach(var segment in _segments)
            {
                MyKeyTemplate keySg = segment as MyKeyTemplate;
                if (keySg != null)
                {
                    if (KeyAsSegment)
                    {
                        sb.Append("/");
                        sb.Append(segment.Template);
                    }
                    else
                    {
                        sb.Append("(");
                        sb.Append(segment.Template);
                        sb.Append(")");
                    }
                }
                else
                {
                    if (index != 0)
                    {
                        sb.Append("/");
                    }
                    sb.Append(segment.Template);
                    index++;
                }
            }

            return sb.ToString();
        }
    }

    internal abstract class MyODataSegment
    {
        public abstract string Template { get; }

        public abstract ODataPathSegment ProcessRouteValue(IEdmModel model, IEdmNavigationSource previous, RouteValueDictionary routeValue, QueryString queryString);
    }

    internal class MyNavigationSourceSegment : MyODataSegment
    {
        public override string Template => NavigationSource.Name;

        public MyNavigationSourceSegment(IEdmNavigationSource source)
        {
            NavigationSource = source;
        }

        public IEdmNavigationSource NavigationSource { get; }

        public override ODataPathSegment ProcessRouteValue(IEdmModel model, IEdmNavigationSource previous, RouteValueDictionary routeValue, QueryString queryString)
        {
            IEdmEntitySet entitySet = NavigationSource as IEdmEntitySet;
            if (entitySet != null)
            {
                return new EntitySetSegment(entitySet);
            }
            else
            {
                IEdmSingleton singleton = (IEdmSingleton)NavigationSource;
                return new SingletonSegment(singleton);
            }
        }
    }

    internal class MyEntitySetSegment : MyODataSegment
    {
        public override string Template => EntitySet.Name;

        public MyEntitySetSegment(IEdmEntitySet entitySet)
        {
            EntitySet = entitySet;
        }

        public IEdmEntitySet EntitySet { get; }

        public override ODataPathSegment ProcessRouteValue(IEdmModel model, IEdmNavigationSource previous, RouteValueDictionary routeValue, QueryString queryString)
        {
            return new EntitySetSegment(EntitySet);
        }
    }

    internal class MySingletonSegment : MyODataSegment
    {
        public override string Template => Singleton.Name;

        public MySingletonSegment(IEdmSingleton singleton)
        {
            Singleton = singleton;
        }

        public IEdmSingleton Singleton { get; }

        public override ODataPathSegment ProcessRouteValue(IEdmModel model, IEdmNavigationSource previous, RouteValueDictionary routeValue, QueryString queryString)
        {
            return new SingletonSegment(Singleton);
        }
    }

    internal class MyCastSegment : MyODataSegment
    {
        public override string Template => CastType.FullTypeName();

        public MyCastSegment(IEdmStructuredType castType, IEdmNavigationSource navigationSource)
        {
            CastType = castType;
            NavigationSource = navigationSource;
        }

        public IEdmStructuredType CastType { get; }

        public IEdmNavigationSource NavigationSource { get; }

        public override ODataPathSegment ProcessRouteValue(IEdmModel model, IEdmNavigationSource previous, RouteValueDictionary routeValue, QueryString queryString)
        {
            return new TypeSegment(CastType, previous);
        }
    }

    internal class MyKeyTemplate : MyODataSegment
    {
        public override string Template { get; }

        public MyKeyTemplate(IEdmEntityType entityType, IEdmNavigationSource navigationSource)
        {
            EntityType = entityType;
            NavigationSource = navigationSource;
            var keys = entityType.Key().ToArray();

            if (keys.Length == 1)
            {
                Template = "{key}";
                KeyMappings[keys[0].Name] = ("key", keys[0].Type);
            }
            else
            {
                foreach (var key in keys)
                {
                    KeyMappings[key.Name] = ($"key{key.Name}", key.Type);
                }

                Template = string.Join(",", KeyMappings.Select(a => $"{a.Key}={a.Value.Item1}"));
            }
        }

        public IDictionary<string, (string, IEdmTypeReference)> KeyMappings { get; } = new Dictionary<string, (string, IEdmTypeReference)>();

        public IEdmEntityType EntityType { get; }

        public IEdmNavigationSource NavigationSource { get; }

        public override ODataPathSegment ProcessRouteValue(IEdmModel model, IEdmNavigationSource previous, RouteValueDictionary routeValue, QueryString queryString)
        {
            IDictionary<string, object> keysValues = new Dictionary<string, object>();
            foreach (var key in KeyMappings)
            {
                string keyName = key.Key;
                string templateName = key.Value.Item1;
                IEdmTypeReference edmType = key.Value.Item2;
                if (routeValue.TryGetValue(templateName, out object rawValue))
                {
                    string strValue = rawValue as string;
                    object newValue = ODataUriUtils.ConvertFromUriLiteral(strValue, ODataVersion.V4, model, edmType);

                    // for without FromODataUri, so update it, for example, remove the single quote for string value.
                    routeValue[templateName] = newValue;

                    // For FromODataUri
                    string prefixName = ODataParameterValue.ParameterValuePrefix + templateName;
                    routeValue[prefixName] = new ODataParameterValue(newValue, edmType);

                    keysValues[keyName] = newValue;
                }
            }

            return new KeySegment(keysValues, EntityType, previous);
        }
    }

    internal class MyFunctionSegment : MyODataSegment
    {
        public override string Template { get; }

        public MyFunctionSegment(IEdmFunction function, IEdmNavigationSource navigationSource, bool unQualifiedFunctionCall)
        {
            Function = function;
            NavigationSource = navigationSource;
            int skip = function.IsBound ? 1 : 0;

            IDictionary<string, string> keyMappings = new Dictionary<string, string>();
            foreach (var parameter in function.Parameters.Skip(skip))
            {
                keyMappings[parameter.Name] = $"{{{parameter.Name}}}";
            }

            if (unQualifiedFunctionCall)
            {
                Template = function.Name + "(" + string.Join(",", keyMappings.Select(a => $"{a.Key}={a.Value}")) + ")";
            }
            else
            {
                Template = function.FullName() + "(" + string.Join(",", keyMappings.Select(a => $"{a.Key}={a.Value}")) + ")";
            }
        }

        public IEdmFunction Function { get; }

        public IDictionary<string, (string, IEdmTypeReference)> ParameterMappings { get; } = new Dictionary<string, (string, IEdmTypeReference)>();

        public IEdmNavigationSource NavigationSource { get; }

        public override ODataPathSegment ProcessRouteValue(IEdmModel model, IEdmNavigationSource previous, RouteValueDictionary routeValue, QueryString queryString)
        {
            // TODO: process the parameter alias
            int skip = Function.IsBound ? 1 : 0;

            IList<OperationSegmentParameter> parameters = new List<OperationSegmentParameter>();
            foreach (var parameter in Function.Parameters.Skip(skip))
            {
                if (routeValue.TryGetValue(parameter.Name, out object rawValue))
                {
                    // for resource or collection resource, this method will return "ODataResourceValue, ..." we should support it.
                    if (IsResourceOrCollectionResource(parameter.Type))
                    {
                        // For FromODataUri
                        string prefixName = ODataParameterValue.ParameterValuePrefix + parameter.Name;
                        routeValue[prefixName] = new ODataParameterValue(rawValue, parameter.Type);

                        parameters.Add(new OperationSegmentParameter(parameter.Name, rawValue));
                    }
                    else
                    {
                        string strValue = rawValue as string;
                        object newValue = ODataUriUtils.ConvertFromUriLiteral(strValue, ODataVersion.V4, model, parameter.Type);

                        // for without FromODataUri, so update it, for example, remove the single quote for string value.
                        routeValue[parameter.Name] = newValue;

                        // For FromODataUri
                        string prefixName = ODataParameterValue.ParameterValuePrefix + parameter.Name;
                        routeValue[prefixName] = new ODataParameterValue(newValue, parameter.Type);

                        parameters.Add(new OperationSegmentParameter(parameter.Name, newValue));
                    }
                }
            }

            IEdmNavigationSource targetset = Function.GetTargetEntitySet(previous, model);

            return new OperationSegment(Function, parameters, targetset as IEdmEntitySetBase);
        }

        private static bool IsResourceOrCollectionResource(IEdmTypeReference edmType)
        {
            if (edmType.IsEntity() || edmType.IsComplex())
            {
                return true;
            }

            if (edmType.IsCollection())
            {
                return IsResourceOrCollectionResource(edmType.AsCollection().ElementType());
            }

            return false;
        }
    }

    internal class MyActionSegment : MyODataSegment
    {
        public override string Template { get; }

        public MyActionSegment(IEdmAction action, IEdmNavigationSource navigationSource, bool unQualifiedFunctionCall)
        {
            Action = action;
            NavigationSource = navigationSource;
            if (unQualifiedFunctionCall)
            {
                Template = action.Name;
            }
            else
            {
                Template = action.FullName();
            }
        }

        public IEdmAction Action { get; }

        public IEdmNavigationSource NavigationSource { get; }

        public override ODataPathSegment ProcessRouteValue(IEdmModel model, IEdmNavigationSource previous, RouteValueDictionary routeValue, QueryString queryString)
        {
            IEdmNavigationSource targetset = null;
            if (Action.ReturnType != null)
            {
                targetset = Action.GetTargetEntitySet(previous, model);
            }

            return new OperationSegment(Action, targetset as IEdmEntitySetBase);
        }
    }

    internal class MyPropertySegment : MyODataSegment
    {
        public override string Template => Property.Name;

        public MyPropertySegment(IEdmStructuralProperty property)
        {
            Property = property;
        }

        public IEdmStructuralProperty Property { get; }

        public override ODataPathSegment ProcessRouteValue(IEdmModel model, IEdmNavigationSource previous, RouteValueDictionary routeValue, QueryString queryString)
        {
            return new PropertySegment(Property);
        }
    }

    internal class MyPropertyTemplateSegment : MyODataSegment
    {
        public override string Template => "{property}";

        public MyPropertyTemplateSegment(IEdmStructuredType declaredType)
        {
            StructuredType = declaredType;
        }

        public IEdmStructuredType StructuredType { get; }

        public override ODataPathSegment ProcessRouteValue(IEdmModel model, IEdmNavigationSource previous, RouteValueDictionary routeValue, QueryString queryString)
        {
            if (routeValue.TryGetValue("property", out object value))
            {
                string rawValue = value as string;
                IEdmProperty edmProperty = StructuredType.FindProperty(rawValue);
                if (edmProperty != null && edmProperty.PropertyKind == EdmPropertyKind.Structural)
                {
                    return new PropertySegment((IEdmStructuralProperty)edmProperty);
                }
            }

            return null;
        }
    }

    internal static class OperationExtensions
    {
        internal static IEdmEntitySetBase GetTargetEntitySet(this IEdmOperation operation, IEdmNavigationSource source, IEdmModel model)
        {
            if (source == null)
            {
                return null;
            }

            if (operation.IsBound && operation.Parameters.Any())
            {
                IEdmOperationParameter parameter;
                Dictionary<IEdmNavigationProperty, IEdmPathExpression> path;
                IEdmEntityType lastEntityType;
                IEnumerable<EdmError> errors;

                if (operation.TryGetRelativeEntitySetPath(model, out parameter, out path, out lastEntityType, out errors))
                {
                    IEdmNavigationSource target = source;

                    foreach (var navigation in path)
                    {
                        target = target.FindNavigationTarget(navigation.Key, navigation.Value);
                    }

                    return target as IEdmEntitySetBase;
                }
            }

            return null;
        }
    }
}