#if !NETSTANDARD2_0
using Microsoft.AspNet.OData;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using System;
using System.Reflection;

namespace Microsoft.AspNetCore.OData.Routing
{
    /// <summary>
    /// 
    /// </summary>
    public class MetadataEndpointConvention : IODataControllerActionConvention
    {
        private static TypeInfo metadataTypeInfo = typeof(MetadataController).GetTypeInfo();

        /// <summary>
        /// 
        /// </summary>
        public virtual int Order => 0;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <param name="controller"></param>
        /// <returns></returns>
        public virtual bool AppliesToController(ODataControllerContext context, ControllerModel controller)
        {
            return controller?.ControllerType == metadataTypeInfo;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <param name="action"></param>
        public virtual bool AppliesToAction(ODataControllerContext context, ActionModel action)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (action.Controller.ControllerType != typeof(MetadataController).GetTypeInfo())
            {
                return false;
            }

            if (action.ActionMethod.Name == "GetMetadata")
            {
                ODataTemplate template = new ODataTemplate(MyMetadataSegment.Instance);
                action.AddSelector(context.Prefix, context.Model, template);

                return true;
            }

            if (action.ActionMethod.Name == "GetServiceDocument")
            {
                ODataTemplate template = new ODataTemplate();
                action.AddSelector(context.Prefix, context.Model, template);

                return true;
            }

            return false;
        }
    }
}
#endif