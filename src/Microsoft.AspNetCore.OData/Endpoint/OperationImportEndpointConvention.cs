#if !NETSTANDARD2_0
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Routing;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;

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
        public int Order => 100;

        /// <summary>
        /// used for cache
        /// </summary>
        internal IEdmOperationImport OperationImport { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <param name="controller"></param>
        /// <returns></returns>
        public bool AppliesToController(ODataControllerContext context, ControllerModel controller)
        {
            return controller?.ControllerName == "ODataOperationImport";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <param name="action"></param>
        public bool AppliesToAction(ODataControllerContext context, ActionModel action)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (action.Controller.ControllerName != "ODataOperationImport")
            {
                return false;
            }

            IEdmModel model = context.Model;

            // By convention, we use the operation name as the action name in the controller
            string actionMethodName = action.ActionMethod.Name;
            var edmOperationImports = model.EntityContainer.FindOperationImports(actionMethodName);

            foreach (var edmOperationImport in edmOperationImports)
            {
                IEdmEntitySetBase targetSet = null;
                edmOperationImport.TryGetStaticEntitySet(model, out targetSet);

                if (edmOperationImport.IsActionImport())
                {
                    ODataTemplate template = new ODataTemplate(new MyActionImportSegment((IEdmActionImport)edmOperationImport));
                    action.AddSelector(context.Prefix, context.Model, template);
                }
                else
                {
                    IEdmFunctionImport functionImport = (IEdmFunctionImport)edmOperationImport;
                    ODataTemplate template = new ODataTemplate(new MyFunctionImportSegment(functionImport));
                    action.AddSelector(context.Prefix, context.Model, template);

                    //if (functionImport.Name == "CalcByOrder")
                    //{
                    //    IDictionary<string, string> parameterMappings = ConstructFunctionParameters(functionImport);
                    //    string parameterTemplate = string.Join(",", parameterMappings.Select(p => $"{p.Key}={p.Value}"));
                    //    string template = string.IsNullOrEmpty(prefix) ? $"{functionImport.Name}({parameterTemplate})" : $"{prefix}/{functionImport.Name}({parameterTemplate})";

                    //    SelectorModel selectorModel = action.Selectors.FirstOrDefault(s => s.AttributeRouteModel == null);
                    //    if (selectorModel == null)
                    //    {
                    //        selectorModel = new SelectorModel();
                    //        action.Selectors.Add(selectorModel);
                    //    }

                    //    selectorModel.AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(template) { Name = template });
                    //    selectorModel.EndpointMetadata.Add(new ODataEndpointMetadata(prefix, model, null,
                    //        (_, __) => new ODataPath(new OperationImportSegment(edmOperationImport, targetSet))));
                    //}
                }
            }

            // in OData operationImport routing convention, all action are processed by default
            // even it's not a really edm operation import call.
            return true;
        }
    }
}
#endif