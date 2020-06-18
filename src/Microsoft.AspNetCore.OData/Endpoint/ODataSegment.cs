using Microsoft.AspNet.OData.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ODataPath = Microsoft.OData.UriParser.ODataPath;

namespace Microsoft.AspNetCore.OData.Routing
{
    internal class ODataTemplate
    {
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

        public string Template { get; }

        public ODataPath GenerateODataPath(RouteValueDictionary routeValue, QueryString queryString)
        {
            return null;
        }

    }

    internal abstract class MyODataSegment
    {
        public abstract string Template { get; }

        public virtual void ProcessRouteValue(IEdmModel model, RouteValueDictionary routeValue, QueryString queryString)
        {

        }
    }

    internal class MyNavigationSourceSegment : MyODataSegment
    {
        public override string Template => NavigationSource.Name;

        public MyNavigationSourceSegment(IEdmNavigationSource source)
        {
            NavigationSource = source;
        }

        public IEdmNavigationSource NavigationSource { get; }
    }

    internal class MyEntitySetSegment : MyODataSegment
    {
        public override string Template => EntitySet.Name;

        public MyEntitySetSegment(IEdmEntitySet entitySet)
        {
            EntitySet = entitySet;
        }

        public IEdmEntitySet EntitySet { get; }
    }

    internal class MySingletonSegment : MyODataSegment
    {
        public override string Template => Singleton.Name;

        public MySingletonSegment(IEdmSingleton singleton)
        {
            Singleton = singleton;
        }

        public IEdmSingleton Singleton { get; }
    }

    internal class MyCastSegment : MyODataSegment
    {
        public override string Template => CastType.FullTypeName();

        public MyCastSegment(IEdmStructuredType castType)
        {
            CastType = castType;
        }

        public IEdmStructuredType CastType { get; }
    }

    internal abstract class MyTemplateSegment : MyODataSegment
    {
        public IDictionary<string, IEdmTypeReference> Mappings { get; } = new Dictionary<string, IEdmTypeReference>();

        public override void ProcessRouteValue(IEdmModel model, RouteValueDictionary routeValue, QueryString queryString)
        {
            //
            foreach (var item in Mappings)
            {
                if (routeValue.TryGetValue(item.Key, out object rawValue))
                {
                    string strValue = rawValue as string;
                    object newValue = ODataUriUtils.ConvertFromUriLiteral(strValue, ODataVersion.V4, model, item.Value);

                    // For FromODataUri
                    string prefixName = ODataParameterValue.ParameterValuePrefix + item.Key;
                    routeValue[prefixName] = new ODataParameterValue(newValue, item.Value);

                    //routeValue[key.Key] = newValue;
                }
            }

        }
    }

    internal class MyKeyTemplate : MyTemplateSegment
    {
        public override string Template { get; }

        public MyKeyTemplate(IEdmEntityType entityType)
        {
            EntityType = entityType;
            var keys = entityType.Key().ToArray();

            if (keys.Length == 1)
            {
                Mappings["key"] = keys[0].Type;
                Template = "{key}";
            }
            else
            {
                IDictionary<string, string> keyMappings = new Dictionary<string, string>();
                foreach (var key in keys)
                {
                    keyMappings[key.Name] = $"key{key.Name}";
                    Mappings[$"key{key.Name}"] = key.Type;
                }

                Template = string.Join(",", keyMappings.Select(a => $"{a.Key}={a.Value}"));
            }
        }

        public IEdmEntityType EntityType { get; }

    }

    internal class MyFunctionSegment : MyTemplateSegment
    {
        public override string Template { get; }

        public MyFunctionSegment(IEdmFunction function, bool unQualifiedFunctionCall)
        {
            Function = function;

            IDictionary<string, string> keyMappings = new Dictionary<string, string>();
            foreach (var parameter in function.Parameters)
            {
                keyMappings[parameter.Name] = $"{{{parameter}}}";
                Mappings[parameter.Name] = parameter.Type;
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
    }

    internal class MyActionSegment : MyTemplateSegment
    {
        public override string Template { get; }

        public MyActionSegment(IEdmAction action, bool unQualifiedFunctionCall)
        {
            Action = action;

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
    }
}