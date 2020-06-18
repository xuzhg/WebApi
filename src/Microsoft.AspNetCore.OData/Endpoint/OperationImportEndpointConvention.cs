#if !NETSTANDARD2_0
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Routing;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.OData.Routing
{
    /// <summary>
    /// 
    /// </summary>
    public class OperationImportEndpointConvention : IODataControllerActionConvention
    {
        /// <summary>
        /// 
        /// </summary>
        public int Order => -1000 + 100;

        /// <summary>
        /// used for cache
        /// </summary>
        internal IEdmOperationImport OperationImport { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="model"></param>
        /// <param name="controller"></param>
        /// <returns></returns>
        public bool AppliesToController(string prefix, IEdmModel model, ControllerModel controller)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            if (model.EntityContainer == null)
            {
                return false;
            }

            return controller.ControllerName == "ODataOperationImport";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="model"></param>
        /// <param name="action"></param>
        public bool AppliesToAction(string prefix, IEdmModel model, ActionModel action)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            // By convention, we use the operation name as the action name in the controller
            string actionMethodName = action.ActionMethod.Name;
            var edmOperationImports = model.EntityContainer.FindOperationImports(actionMethodName);

            foreach (var edmOperationImport in edmOperationImports)
            {
                IEdmEntitySetBase targetSet = null;
                edmOperationImport.TryGetStaticEntitySet(model, out targetSet);

                if (edmOperationImport.IsActionImport())
                {
                    IEdmActionImport actionImport = (IEdmActionImport)edmOperationImport;

                    var template = string.IsNullOrEmpty(prefix) ? edmOperationImport.Name : $"{prefix}/{edmOperationImport.Name}";

                    SelectorModel selectorModel = action.Selectors.FirstOrDefault(s => s.AttributeRouteModel == null);
                    if (selectorModel == null)
                    {
                        selectorModel = new SelectorModel();
                        action.Selectors.Add(selectorModel);
                    }

                    selectorModel.AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(template) { Name = template });
                    selectorModel.EndpointMetadata.Add(new ODataEndpointMetadata(prefix, model, null,
                        (_, __) => new ODataPath(new OperationImportSegment(edmOperationImport, targetSet))));
                }
                else
                {
                    IEdmFunctionImport functionImport = (IEdmFunctionImport)edmOperationImport;

                    if (functionImport.Name == "CalcByOrder")
                    {
                        IDictionary<string, string> parameterMappings = ConstructFunctionParameters(functionImport);
                        string parameterTemplate = string.Join(",", parameterMappings.Select(p => $"{p.Key}={p.Value}"));
                        string template = string.IsNullOrEmpty(prefix) ? $"{functionImport.Name}({parameterTemplate})" : $"{prefix}/{functionImport.Name}({parameterTemplate})";

                        SelectorModel selectorModel = action.Selectors.FirstOrDefault(s => s.AttributeRouteModel == null);
                        if (selectorModel == null)
                        {
                            selectorModel = new SelectorModel();
                            action.Selectors.Add(selectorModel);
                        }

                        selectorModel.AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(template) { Name = template });
                        selectorModel.EndpointMetadata.Add(new ODataEndpointMetadata(prefix, model, null,
                            (_, __) => new ODataPath(new OperationImportSegment(edmOperationImport, targetSet))));
                    }

                    else
                    {
                        string template = string.IsNullOrEmpty(prefix) ? "{functionimport}" : $"{prefix}/{{functionimport}}";

                        SelectorModel selectorModel = action.Selectors.FirstOrDefault(s => s.AttributeRouteModel == null);
                        if (selectorModel == null)
                        {
                            selectorModel = new SelectorModel();
                            action.Selectors.Add(selectorModel);
                        }

                        selectorModel.AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(template) { Name = template });
                        selectorModel.EndpointMetadata.Add(new ODataEndpointMetadata(prefix, model, null,
                            (_, __) => new ODataPath(new OperationImportSegment(edmOperationImport, targetSet))));
                    }
                }
            }

            // in OData operationImport routing convention, all action are processed by default
            // even it's not a really edm operation import call.
            return true;
        }

        private static IDictionary<string, string> ConstructFunctionParameters(IEdmFunctionImport functionImport)
        {
            IEdmFunction function = functionImport.Function;

            int skip = 0;
            if (function.IsBound)
            {
                // Function import should not be a bound, here for safety. Need Double confirm with 
                skip = 1;
            }

            IDictionary<string, string> parameterMappings = new Dictionary<string, string>();
            foreach (var parameter in function.Parameters.Skip(skip))
            {
                parameterMappings[parameter.Name] = $"{{{parameter.Name}}}";
            }

            return parameterMappings;
        }
    }
}
#endif