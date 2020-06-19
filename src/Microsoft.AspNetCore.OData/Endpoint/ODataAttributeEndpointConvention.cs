#if !NETSTANDARD2_0
using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Routing;
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
    public class ODataAttributeEndpointConvention : IODataControllerActionConvention
    {
        /// <summary>
        /// 
        /// </summary>
        public int Order => -1000 + 100;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="model"></param>
        /// <param name="controller"></param>
        /// <returns></returns>
        public bool AppliesToController(string prefix, IEdmModel model, ControllerModel controller)
        {
            // Apply to all controllers
            return true;
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

            ODataRouteAttribute routeAttr = action.GetAttribute<ODataRouteAttribute>();
            if (routeAttr == null)
            {
                return false;
            }

            string routeTemplate = "";
            ODataRoutePrefixAttribute prefixAttr = action.Controller.GetAttribute<ODataRoutePrefixAttribute>();
            if (prefixAttr != null)
            {
                routeTemplate = prefixAttr.Prefix + "/";
            }
            routeTemplate += routeAttr.PathTemplate;

            SelectorModel selectorModel = action.Selectors.FirstOrDefault(s => s.AttributeRouteModel == null);
            if (selectorModel == null)
            {
                selectorModel = new SelectorModel();
                action.Selectors.Add(selectorModel);
            }

            string templateStr = string.IsNullOrEmpty(prefix) ? routeTemplate : $"{prefix}/{routeTemplate}";

            selectorModel.AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(templateStr) { Name = templateStr });
            selectorModel.EndpointMetadata.Add(new ODataEndpointMetadata(prefix, model, templateStr));

            return true;
        }
    }
}
#endif