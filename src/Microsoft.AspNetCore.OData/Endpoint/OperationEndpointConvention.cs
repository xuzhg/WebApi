#if !NETSTANDARD2_0
using Microsoft.AspNet.OData;
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
    public class OperationEndpointConvention : IODataControllerActionConvention
    {
        /// <summary>
        /// 
        /// </summary>
        public int Order => -1000 + 100;

        /// <summary>
        /// used for cache
        /// </summary>
        internal IEdmNavigationSource NavigationSource { get; private set; }

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

            string controllerName = controller.ControllerName;
            IEdmEntitySet entitySet = model.EntityContainer.FindEntitySet(controllerName);
            if (entitySet != null)
            {
                NavigationSource = entitySet;
                return true;
            }

            IEdmSingleton singleton = model.EntityContainer.FindSingleton(controllerName);
            if (entitySet != null)
            {
                NavigationSource = singleton;
                return true;
            }

            return false;
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

            Debug.Assert(NavigationSource != null);

            IEdmEntityType entityType = NavigationSource.EntityType();
            bool hasKeyParameter = HasKeyParameter(entityType, action);

            // found
            int keyNumber = entityType.Key().Count();
            IEdmType bindType = entityType;
            if (!hasKeyParameter)
            {
                // bond to collection
                bindType = new EdmCollectionType(new EdmEntityTypeReference(entityType, true));
                keyNumber = 0;
            }

            string actionName = action.ActionMethod.Name;
            var operations = model.FindBoundOperations(bindType).Where(p => p.Name == actionName);

            var actions = operations.OfType<IEdmAction>().ToList();
            if (actions.Count == 1) // action overload on binding type, only one action overload on the same binding type
            {
                if (action.Parameters.Any(p => p.ParameterType == typeof(ODataActionParameters)))
                {
                    // we find a action route
                    IList<MyODataSegment> segments = new List<MyODataSegment>
                    {
                        new MyNavigationSourceSegment(NavigationSource)
                    };
                    if (hasKeyParameter)
                    {
                        segments.Add(new MyKeyTemplate(entityType));
                    }
                    segments.Add(new MyActionSegment(actions[0], false));

                    ODataTemplate template = new ODataTemplate(segments);

                    SelectorModel selectorModel = action.Selectors.FirstOrDefault(s => s.AttributeRouteModel == null);
                    if (selectorModel == null)
                    {
                        selectorModel = new SelectorModel();
                        action.Selectors.Add(selectorModel);
                    }

                    string templateStr = template.Template;
                    selectorModel.AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(templateStr) { Name = prefix });
                    selectorModel.EndpointMetadata.Add(new ODataEndpointMetadata(prefix, model, template));

                    return true;
                }
            }

            var functions = operations.OfType<IEdmFunction>().ToList();
            IEdmFunction function = FindMatchFunction(keyNumber, functions, action);
            if (function != null)
            {
                IList<MyODataSegment> segments = new List<MyODataSegment>
                    {
                        new MyNavigationSourceSegment(NavigationSource)
                    };
                if (hasKeyParameter)
                {
                    segments.Add(new MyKeyTemplate(entityType));
                }
                segments.Add(new MyFunctionSegment(function, false));

                ODataTemplate template = new ODataTemplate(segments);

                SelectorModel selectorModel = action.Selectors.FirstOrDefault(s => s.AttributeRouteModel == null);
                if (selectorModel == null)
                {
                    selectorModel = new SelectorModel();
                    action.Selectors.Add(selectorModel);
                }

                string templateStr = template.Template;
                selectorModel.AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(templateStr) { Name = prefix });
                selectorModel.EndpointMetadata.Add(new ODataEndpointMetadata(prefix, model, template));

                return true;
            }

            // in OData operationImport routing convention, all action are processed by default
            // even it's not a really edm operation import call.
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

        private static IEdmFunction FindMatchFunction(int keyNumber, IEnumerable<IEdmFunction> functions, ActionModel action)
        {
            // if it's action
            int actionParameterNumber = action.Parameters.Count - keyNumber;
            foreach (var function in functions)
            {
                if (function.Parameters.Count() != actionParameterNumber)
                {
                    // maybe we can allow other parameters
                    continue;
                }

                bool matched = true;
                foreach (var parameter in function.Parameters.Skip(1)) // skip 1 because bound
                {
                    if (!action.Parameters.Any(p => p.ParameterInfo.Name == parameter.Name))
                    {
                        matched = false;
                        break;
                    }
                }

                if (matched)
                {
                    return function;
                }
            }

            return null;
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