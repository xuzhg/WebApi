using Microsoft.OData.Edm;
using System;

namespace Microsoft.AspNetCore.OData.Routing
{
    /// <summary>
    /// 
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ODataModelAttribute : Attribute
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="model"></param>
        public ODataModelAttribute(string model)
        {
            Model = model;
        }

        /// <summary>
        /// 
        /// </summary>
        public string Model { get; }
    }

    /// <summary>
    /// 
    /// </summary>
    public class ODataModelFactory
    {
        /// <summary>
        /// 
        /// </summary>
        public IEdmModel Model { get; set; }
    }
}