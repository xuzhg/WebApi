using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;

namespace Microsoft.AspNetCore.OData.Routing
{
    /// <summary>
    /// 
    /// </summary>
    internal class ODataEndpointMetadata
    {
        public ODataEndpointMetadata(string prefix, IEdmModel model, ODataTemplate template)
        {
            Prefix = prefix;
            Model = model;
            Template = template;
        }

        public ODataEndpointMetadata(string prefix, IEdmModel model, IDictionary<string, string> parameterMappings,
            Func<RouteValueDictionary, IDictionary<string, string>, ODataPath> odataPathFactory)
        {
            Prefix = prefix;
            Model = model;
            ParameterMappings = parameterMappings;
            ODataPathFactory = odataPathFactory;
        }

        public string Prefix { get; }

        public IEdmModel Model { get; }

        public IDictionary<string, string> ParameterMappings { get; }

        public Func<RouteValueDictionary, IDictionary<string, string>, ODataPath> ODataPathFactory { get; }

        public ODataTemplate Template { get; }

        // { { "$filter", "IntProp eq @p1" }, { "@p1", "@p2" }, { "@p2", "123" } });
        public ODataPath GenerateODataPath(RouteValueDictionary values, QueryString queryString)
        {
            return null;
        }
    }
}
