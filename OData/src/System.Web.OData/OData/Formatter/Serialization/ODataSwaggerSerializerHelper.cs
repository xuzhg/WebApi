// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.OData.Edm;
using Newtonsoft.Json.Linq;

namespace System.Web.OData.Formatter.Serialization
{
    internal class ODataSwaggerSerializerHelper
    {
        private const string Version = "0.1.0";

        private IEdmModel _model;
        private JObject _swaggerDoc;
        private JObject _swaggerPaths;
        private JObject _swaggeDefinitions;

        public JObject SwaggerDoc
        {
            get { return _swaggerDoc; }
        }

        public ODataSwaggerSerializerHelper(IEdmModel model, string metadataUri, string host, string basePath)
        {
            Contract.Assert(model != null);

            _model = model;

            InitializeDocument(metadataUri, host, basePath);
            InitializeContainer();
            InitializeTypeDefinitions();
            InitializeOpertions();
            InitializeEnd();
        }

        private void InitializeDocument(string metadataUri, string host, string basePath)
        {
            _swaggerDoc = new JObject()
            {
                { "swagger", "2.0" },
                { "info", new JObject()
                {
                    { "title", "OData Service" },
                    { "description", "The OData Service at " + metadataUri },
                    { "version", Version },
                    { "x-odata-version", "4.0" }
                }
                },
                { "host", host },
                { "schemes", new JArray("http") },
                { "basePath", basePath },
                { "consumes", new JArray("application/json") },
                { "produces", new JArray("application/json") },
            };
        }

        private void InitializeContainer()
        {
            _swaggerPaths = new JObject();
            _swaggerDoc.Add("paths", _swaggerPaths);

            if (_model.EntityContainer == null)
            {
                return;
            }

            foreach (var entitySet in _model.EntityContainer.EntitySets())
            {
                _swaggerPaths.Add("/" + entitySet.Name, CreateSwaggerPathForEntitySet(entitySet));

                _swaggerPaths.Add(GetPathForEntity(entitySet), CreateSwaggerPathForEntity(entitySet));
            }

            foreach (var operationImport in _model.EntityContainer.OperationImports())
            {
                _swaggerPaths.Add(GetPathForOperationImport(operationImport), CreateSwaggerPathForOperationImport(operationImport));
            }
        }

        private void InitializeTypeDefinitions()
        {
            _swaggeDefinitions = new JObject();
            _swaggerDoc.Add("definitions", _swaggeDefinitions);

            foreach (var type in _model.SchemaElements.OfType<IEdmStructuredType>())
            {
                _swaggeDefinitions.Add(type.FullTypeName(), CreateSwaggerDefinitionForStructureType(type));
            }
        }

        private void InitializeOpertions()
        {
            foreach (var operation in _model.SchemaElements.OfType<IEdmOperation>())
            {
                // skip unbound operation
                if (!operation.IsBound)
                {
                    continue;
                }

                var boundParameter = operation.Parameters.First();
                var boundType = boundParameter.Type.Definition;

                // skip operation bound to non entity (or entity collection)
                if (boundType.TypeKind == EdmTypeKind.Entity)
                {
                    IEdmEntityType entityType = (IEdmEntityType)boundType;
                    foreach (
                        var entitySet in
                            _model.EntityContainer.EntitySets().Where(es => es.EntityType().Equals(entityType)))
                    {
                        _swaggerPaths.Add(GetPathForOperationOfEntity(operation, entitySet),
                            CreateSwaggerPathForOperationOfEntity(operation, entitySet));
                    }
                }
                else if (boundType.TypeKind == EdmTypeKind.Collection)
                {
                    IEdmCollectionType collectionType = boundType as IEdmCollectionType;

                    if (collectionType != null && collectionType.ElementType.Definition.TypeKind == EdmTypeKind.Entity)
                    {
                        IEdmEntityType entityType = (IEdmEntityType)collectionType.ElementType.Definition;
                        foreach (
                            var entitySet in
                                _model.EntityContainer.EntitySets().Where(es => es.EntityType().Equals(entityType)))
                        {
                            _swaggerPaths.Add(GetPathForOperationOfEntitySet(operation, entitySet),
                                CreateSwaggerPathForOperationOfEntitySet(operation, entitySet));
                        }
                    }
                }
            }
        }

        private void InitializeEnd()
        {
            _swaggeDefinitions.Add("_Error", new JObject()
            {
                {
                    "properties", new JObject()
                    {
                        { "error", new JObject()
                        {
                            { "$ref", "#/definitions/_InError" }
                        }
                        }
                    }
                }
            });

            _swaggeDefinitions.Add("_InError", new JObject()
            {
                {
                    "properties", new JObject()
                    {
                        { "code", new JObject()
                        {
                            { "type", "string" }
                        }
                        },
                        { "message", new JObject()
                        {
                            { "type", "string" }
                        }
                        }
                    }
                }
            });
        }

        private static JObject CreateSwaggerPathForEntitySet(IEdmEntitySet entitySet)
        {
            Contract.Assert(entitySet != null);

            return new JObject()
            {
                {
                    "get", new JObject()
                        .Summary("Get EntitySet " + entitySet.Name)
                        .Description("Returns the EntitySet " + entitySet.Name)
                        .Tags(entitySet.Name)
                        .Parameters(new JArray()
                            .Parameter("$expand", "query", "Expand navigation property", "string")
                            .Parameter("$select", "query", "select structural property", "string")
                            .Parameter("$orderby", "query", "order by some property", "string")
                            .Parameter("$top", "query", "top elements", "integer")
                            .Parameter("$skip", "query", "skip elements", "integer")
                            .Parameter("$count", "query", "include count in response", "boolean"))
                        .Responses(new JObject()
                            .Response("200", "EntitySet " + entitySet.Name, entitySet.EntityType())
                            .DefaultErrorResponse())
                },
                {
                    "post", new JObject()
                        .Summary("Post a new entity to EntitySet " + entitySet.Name)
                        .Description("Post a new entity to EntitySet " + entitySet.Name)
                        .Tags(entitySet.Name)
                        .Parameters(new JArray()
                            .Parameter(entitySet.EntityType().Name, "body", "The entity to post",
                                entitySet.EntityType()))
                        .Responses(new JObject()
                            .Response("200", "EntitySet " + entitySet.Name, entitySet.EntityType())
                            .DefaultErrorResponse())
                }
            };
        }

        private static JObject CreateSwaggerPathForEntity(IEdmEntitySet entitySet)
        {
            Contract.Assert(entitySet != null);

            var keyParameters = new JArray();
            foreach (var key in entitySet.EntityType().Key())
            {
                string format;
                string type = GetPrimitiveTypeAndFormat(key.Type.Definition as IEdmPrimitiveType, out format);
                keyParameters.Parameter(key.Name, "path", "key: " + key.Name, type, format);
            }

            return new JObject()
            {
                {
                    "get", new JObject()
                        .Summary("Get entity from " + entitySet.Name + " by key.")
                        .Description("Returns the entity with the key from " + entitySet.Name)
                        .Tags(entitySet.Name)
                        .Parameters((keyParameters.DeepClone() as JArray)
                            .Parameter("$select", "query", "description", "string"))
                        .Responses(new JObject()
                            .Response("200", "EntitySet " + entitySet.Name, entitySet.EntityType())
                            .DefaultErrorResponse())
                },
                {
                    "patch", new JObject()
                        .Summary("Update entity in EntitySet " + entitySet.Name)
                        .Description("Update entity in EntitySet " + entitySet.Name)
                        .Tags(entitySet.Name)
                        .Parameters((keyParameters.DeepClone() as JArray)
                            .Parameter(entitySet.EntityType().Name, "body", "The entity to patch",
                                entitySet.EntityType()))
                        .Responses(new JObject()
                            .Response("204", "Empty response")
                            .DefaultErrorResponse())
                },
                {
                    "delete", new JObject()
                        .Summary("Delete entity in EntitySet " + entitySet.Name)
                        .Description("Delete entity in EntitySet " + entitySet.Name)
                        .Tags(entitySet.Name)
                        .Parameters((keyParameters.DeepClone() as JArray)
                            .Parameter("If-Match", "header", "If-Match header", "string"))
                        .Responses(new JObject()
                            .Response("204", "Empty response")
                            .DefaultErrorResponse())
                }
            };
        }

        private static JObject CreateSwaggerDefinitionForStructureType(IEdmStructuredType edmType)
        {
            JObject swaggerProperties = new JObject();
            foreach (var property in edmType.StructuralProperties())
            {
                JObject swaggerProperty = new JObject().Description(property.Name);
                SetSwaggerType(swaggerProperty, property.Type.Definition);
                swaggerProperties.Add(property.Name, swaggerProperty);
            }
            return new JObject()
            {
                { "properties", swaggerProperties }
            };
        }

        private static string GetPathForEntity(IEdmEntitySet entitySet)
        {
            string singleEntityPath = "/" + entitySet.Name + "(";
            foreach (var key in entitySet.EntityType().Key())
            {
                if (key.Type.Definition.TypeKind == EdmTypeKind.Primitive &&
                    (key.Type.Definition as IEdmPrimitiveType).PrimitiveKind == EdmPrimitiveTypeKind.String)
                {
                    singleEntityPath += "'{" + key.Name + "}', ";
                }
                else
                {
                    singleEntityPath += "{" + key.Name + "}, ";
                }
            }
            singleEntityPath = singleEntityPath.Substring(0, singleEntityPath.Length - 2);
            singleEntityPath += ")";

            return singleEntityPath;
        }

        private static JObject CreateSwaggerPathForOperationImport(IEdmOperationImport operationImport)
        {
            bool isFunctionImport = operationImport is IEdmFunctionImport;
            JArray swaggerParameters = new JArray();
            foreach (var parameter in operationImport.Operation.Parameters)
            {
                swaggerParameters.Parameter(parameter.Name, isFunctionImport ? "path" : "body",
                    "parameter: " + parameter.Name, parameter.Type.Definition);
            }

            JObject swaggerResponses = new JObject();
            if (operationImport.Operation.ReturnType == null)
            {
                swaggerResponses.Response("204", "Empty response");
            }
            else
            {
                swaggerResponses.Response("200", "Response from " + operationImport.Name,
                    operationImport.Operation.ReturnType.Definition);
            }

            JObject swaggerOperationImport = new JObject()
                .Summary("Call operation import  " + operationImport.Name)
                .Description("Call operation import  " + operationImport.Name)
                .Tags(isFunctionImport ? "Function Import" : "Action Import");

            if (swaggerParameters.Count > 0)
            {
                swaggerOperationImport.Parameters(swaggerParameters);
            }
            swaggerOperationImport.Responses(swaggerResponses.DefaultErrorResponse());

            return new JObject()
            {
                { isFunctionImport ? "get" : "post", swaggerOperationImport }
            };
        }

        private static JObject CreateSwaggerPathForOperationOfEntitySet(IEdmOperation operation, IEdmEntitySet entitySet)
        {
            bool isFunction = operation is IEdmFunction;
            JArray swaggerParameters = new JArray();
            foreach (var parameter in operation.Parameters.Skip(1))
            {
                swaggerParameters.Parameter(parameter.Name, isFunction ? "path" : "body",
                    "parameter: " + parameter.Name, parameter.Type.Definition);
            }

            JObject swaggerResponses = new JObject();
            if (operation.ReturnType == null)
            {
                swaggerResponses.Response("204", "Empty response");
            }
            else
            {
                swaggerResponses.Response("200", "Response from " + operation.Name,
                    operation.ReturnType.Definition);
            }

            JObject swaggerOperation = new JObject()
                .Summary("Call operation  " + operation.Name)
                .Description("Call operation  " + operation.Name)
                .Tags(entitySet.Name, isFunction ? "Function" : "Action");

            if (swaggerParameters.Count > 0)
            {
                swaggerOperation.Parameters(swaggerParameters);
            }
            swaggerOperation.Responses(swaggerResponses.DefaultErrorResponse());
            return new JObject()
                {
                    { isFunction ? "get" : "post", swaggerOperation }
                };
        }

        private static JObject CreateSwaggerPathForOperationOfEntity(IEdmOperation operation, IEdmEntitySet entitySet)
        {
            bool isFunction = operation is IEdmFunction;
            JArray swaggerParameters = new JArray();

            foreach (var key in entitySet.EntityType().Key())
            {
                string format;
                string type = GetPrimitiveTypeAndFormat(key.Type.Definition as IEdmPrimitiveType, out format);
                swaggerParameters.Parameter(key.Name, "path", "key: " + key.Name, type, format);
            }

            foreach (var parameter in operation.Parameters.Skip(1))
            {
                swaggerParameters.Parameter(parameter.Name, isFunction ? "path" : "body",
                    "parameter: " + parameter.Name, parameter.Type.Definition);
            }

            JObject swaggerResponses = new JObject();
            if (operation.ReturnType == null)
            {
                swaggerResponses.Response("204", "Empty response");
            }
            else
            {
                swaggerResponses.Response("200", "Response from " + operation.Name,
                    operation.ReturnType.Definition);
            }

            JObject swaggerOperation = new JObject()
                .Summary("Call operation  " + operation.Name)
                .Description("Call operation  " + operation.Name)
                .Tags(entitySet.Name, isFunction ? "Function" : "Action");

            if (swaggerParameters.Count > 0)
            {
                swaggerOperation.Parameters(swaggerParameters);
            }
            swaggerOperation.Responses(swaggerResponses.DefaultErrorResponse());
            return new JObject()
                {
                    { isFunction ? "get" : "post", swaggerOperation }
                };
        }

        private static string GetPathForOperationImport(IEdmOperationImport operationImport)
        {
            string swaggerOperationImportPath = "/" + operationImport.Name + "(";
            if (operationImport.IsFunctionImport())
            {
                foreach (var parameter in operationImport.Operation.Parameters)
                {
                    swaggerOperationImportPath += parameter.Name + "=" + "{" + parameter.Name + "},";
                }
            }
            if (swaggerOperationImportPath.EndsWith(",", StringComparison.Ordinal))
            {
                swaggerOperationImportPath = swaggerOperationImportPath.Substring(0,
                    swaggerOperationImportPath.Length - 1);
            }
            swaggerOperationImportPath += ")";

            return swaggerOperationImportPath;
        }

        private static string GetPathForOperationOfEntitySet(IEdmOperation operation, IEdmEntitySet entitySet)
        {
            string swaggerOperationPath = "/" + entitySet.Name + "/" + operation.FullName() + "(";
            if (operation.IsFunction())
            {
                foreach (var parameter in operation.Parameters.Skip(1))
                {
                    if (parameter.Type.Definition.TypeKind == EdmTypeKind.Primitive &&
                   (parameter.Type.Definition as IEdmPrimitiveType).PrimitiveKind == EdmPrimitiveTypeKind.String)
                    {
                        swaggerOperationPath += parameter.Name + "=" + "'{" + parameter.Name + "}',";
                    }
                    else
                    {
                        swaggerOperationPath += parameter.Name + "=" + "{" + parameter.Name + "},";
                    }
                }
            }
            if (swaggerOperationPath.EndsWith(",", StringComparison.Ordinal))
            {
                swaggerOperationPath = swaggerOperationPath.Substring(0,
                    swaggerOperationPath.Length - 1);
            }
            swaggerOperationPath += ")";

            return swaggerOperationPath;
        }

        private static string GetPathForOperationOfEntity(IEdmOperation operation, IEdmEntitySet entitySet)
        {
            string swaggerOperationPath = GetPathForEntity(entitySet) + "/" + operation.FullName() + "(";
            if (operation.IsFunction())
            {
                foreach (var parameter in operation.Parameters.Skip(1))
                {
                    if (parameter.Type.Definition.TypeKind == EdmTypeKind.Primitive &&
                   (parameter.Type.Definition as IEdmPrimitiveType).PrimitiveKind == EdmPrimitiveTypeKind.String)
                    {
                        swaggerOperationPath += parameter.Name + "=" + "'{" + parameter.Name + "}',";
                    }
                    else
                    {
                        swaggerOperationPath += parameter.Name + "=" + "{" + parameter.Name + "},";
                    }
                }
            }
            if (swaggerOperationPath.EndsWith(",", StringComparison.Ordinal))
            {
                swaggerOperationPath = swaggerOperationPath.Substring(0,
                    swaggerOperationPath.Length - 1);
            }
            swaggerOperationPath += ")";

            return swaggerOperationPath;
        }

        public static void SetSwaggerType(JObject obj, IEdmType edmType)
        {
            if (edmType.TypeKind == EdmTypeKind.Complex || edmType.TypeKind == EdmTypeKind.Entity)
            {
                obj.Add("$ref", "#/definitions/" + edmType.FullTypeName());
            }
            else if (edmType.TypeKind == EdmTypeKind.Primitive)
            {
                string format;
                string type = GetPrimitiveTypeAndFormat((IEdmPrimitiveType)edmType, out format);
                obj.Add("type", type);
                if (format != null)
                {
                    obj.Add("format", format);
                }
            }
            else if (edmType.TypeKind == EdmTypeKind.Enum)
            {
                obj.Add("type", "string");
            }
            else if (edmType.TypeKind == EdmTypeKind.Collection)
            {
                IEdmType itemEdmType = ((IEdmCollectionType)edmType).ElementType.Definition;
                JObject nestedItem = new JObject();
                SetSwaggerType(nestedItem, itemEdmType);
                obj.Add("type", "array");
                obj.Add("items", nestedItem);
            }
        }

        private static string GetPrimitiveTypeAndFormat(IEdmPrimitiveType primtiveType, out string format)
        {
            format = null;
            switch (primtiveType.PrimitiveKind)
            {
                case EdmPrimitiveTypeKind.String:
                    return "string";
                case EdmPrimitiveTypeKind.Int16:
                case EdmPrimitiveTypeKind.Int32:
                    format = "int32";
                    return "integer";
                case EdmPrimitiveTypeKind.Int64:
                    format = "int64";
                    return "integer";
                case EdmPrimitiveTypeKind.Boolean:
                    return "boolean";
                case EdmPrimitiveTypeKind.Byte:
                    format = "byte";
                    return "string";
                case EdmPrimitiveTypeKind.Date:
                    format = "date";
                    return "string";
                case EdmPrimitiveTypeKind.DateTimeOffset:
                    format = "date-time";
                    return "string";
                case EdmPrimitiveTypeKind.Double:
                    format = "double";
                    return "number";
                case EdmPrimitiveTypeKind.Single:
                    format = "float";
                    return "number";
                default:
                    return "string";
            }
        }
    }
}
