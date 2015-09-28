// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using Microsoft.OData.Edm;
using Newtonsoft.Json.Linq;

namespace System.Web.OData.Formatter.Serialization
{
    internal static class JObjectExtensionMethods
    {
        public static JObject Responses(this JObject obj, JObject responses)
        {
            obj.Add("responses", responses);
            return obj;
        }

        public static JObject ResponseRef(this JObject responses, string name, string description, string refType)
        {
            responses.Add(name, new JObject()
            {
                { "description", description },
                {
                    "schema", new JObject()
                    {
                        { "$ref", refType }
                    }
                }
            });

            return responses;
        }

        public static JObject Response(this JObject responses, string name, string description, IEdmType type)
        {
            var schema = new JObject();
            ODataSwaggerSerializerHelper.SetSwaggerType(schema, type);

            responses.Add(name, new JObject()
            {
                { "description", description },
                { "schema", schema }
            });

            return responses;
        }

        public static JObject ResponseArrayRef(this JObject responses, string name, string description, string refType)
        {
            responses.Add(name, new JObject()
            {
                { "description", description },
                {
                    "schema", new JObject()
                    {
                        { "type", "array" },
                        {
                            "items", new JObject()
                            {
                                { "$ref", refType }
                            }
                        }
                    }
                }
            });

            return responses;
        }

        public static JObject DefaultErrorResponse(this JObject responses)
        {
            return responses.ResponseRef("default", "Unexpected error", "#/definitions/_Error");
        }

        public static JObject Response(this JObject responses, string name, string description)
        {
            responses.Add(name, new JObject()
            {
                { "description", description },
            });

            return responses;
        }

        public static JObject Parameters(this JObject obj, JArray parameters)
        {
            obj.Add("parameters", parameters);
            return obj;
        }

        public static JArray Parameter(this JArray parameters, string name, string kind, string description, string type, string format = null)
        {
            parameters.Add(new JObject()
            {
                { "name", name },
                { "in", kind },
                { "description", description },
                { "type", type },
            });

            if (!String.IsNullOrEmpty(format))
            {
                (parameters.First as JObject).Add("format", format);
            }

            return parameters;
        }

        public static JArray Parameter(this JArray parameters, string name, string kind, string description, IEdmType type)
        {
            var parameter = new JObject()
            {
                { "name", name },
                { "in", kind },
                { "description", description },
            };

            if (kind != "body")
            {
                ODataSwaggerSerializerHelper.SetSwaggerType(parameter, type);
            }
            else
            {
                var schema = new JObject();
                ODataSwaggerSerializerHelper.SetSwaggerType(schema, type);
                parameter.Add("schema", schema);
            }

            parameters.Add(parameter);
            return parameters;
        }

        public static JArray ParameterRef(this JArray parameters, string name, string kind, string description, string refType)
        {
            parameters.Add(new JObject()
            {
                { "name", name },
                { "in", kind },
                { "description", description },
                {
                    "schema", new JObject()
                    {
                        { "$ref", refType }
                    }
                }
            });

            return parameters;
        }

        public static JObject Tags(this JObject obj, params string[] tags)
        {
            obj.Add("tags", new JArray(tags));
            return obj;
        }

        public static JObject Summary(this JObject obj, string summary)
        {
            obj.Add("summary", summary);
            return obj;
        }

        public static JObject Description(this JObject obj, string description)
        {
            obj.Add("description", description);
            return obj;
        }
    }
}
