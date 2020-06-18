using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Routing;
using ODataRoutingSample.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ODataRoutingSample.Controllers
{
    public class ODataOperationImportController : ControllerBase
    {
        [HttpPost]
        public IEnumerable<Product> ResetData()
        {
            var rng = new Random();
            return Enumerable.Range(1, 5).Select(index => new Product
            {
                Id = index,
                Category = "Category + " + index
            })
            .ToArray();
        }
    }
}

namespace ODataRoutingSample.Controllers.V1
{
    [ODataModel("v1")]
    public class ODataOperationImportController : ControllerBase
    {
        [HttpGet]
        public int RateByOrder(int order)
        {
            return order;
        }
    }
}


namespace ODataRoutingSample.Controllers.V2
{
    [ODataModel("v2{data}")]
    public class ODataOperationImportController : ControllerBase
    {
        [HttpGet]
        public int RateByOrder(int order)
        {
            return order;
        }

        [HttpGet]
        public string CalcByOrder(int order, string name)
        {
            return  name + ": " + order;
        }
    }
}
