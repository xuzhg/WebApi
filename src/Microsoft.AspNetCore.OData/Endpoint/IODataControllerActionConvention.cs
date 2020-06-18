
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.OData.Edm;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.AspNetCore.OData.Routing
{
    internal class ODataControllerContext
    {

    }

    internal class ODataActionContext
    {
        public void Test(IList<ControllerModel> controllers, IEnumerable<IODataControllerActionConvention> conventions)
        {
            string prefix = null;
            IEdmModel model = null;
            foreach (var controller in controllers)
            {
                // Add here
                //

                // Get conventions for all this controller

                foreach (var convention in conventions)
                {
                    if (convention.AppliesToController(prefix, model, controller))
                    {
                        foreach (var action in controller.Actions)
                        {
                            //if (convention.AppliesToAction(route.Key, route.Value, action))
                            //{
                            //    ;
                            //}
                        }
                    }
                }
            }
        }
    }

    internal interface IODataEndponitConvention
    {
        ODataControllerContext AppliesToController(string prefix, IEdmModel model, ControllerModel controller);

        bool AppliesToController(ODataActionContext context, ControllerModel controller);

    }


    /// <summary>
    /// 
    /// </summary>
    public interface IODataControllerActionConvention
    {
        /// <summary>
        /// 
        /// </summary>
        int Order { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="model"></param>
        /// <param name="controller"></param>
        /// <returns></returns>
        bool AppliesToController(string prefix, IEdmModel model, ControllerModel controller);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="model"></param>
        /// <param name="action"></param>
        bool AppliesToAction(string prefix, IEdmModel model, ActionModel action);
    }
}
