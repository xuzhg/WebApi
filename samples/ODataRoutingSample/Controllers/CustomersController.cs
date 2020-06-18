using Microsoft.AspNet.OData;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Routing;
using ODataRoutingSample.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ODataRoutingSample.Controllers
{
    public class ProductsController : ControllerBase
    {
        [HttpGet]
        public IEnumerable<Product> Get()
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

    [ODataModel("v1")]
    public class CustomersController : ControllerBase
    {
        [HttpGet]
        public IEnumerable<Customer> Get()
        {
            var rng = new Random();
            return Enumerable.Range(1, 5).Select(index => new Customer
            {
                Id = index,
                Name = "Name + " + index
            })
            .ToArray();
        }
    }

    [ODataModel("v2{data}")]
    public class OrdersController : ControllerBase
    {
        [HttpGet]
        public IEnumerable<Order> Get()
        {
            var rng = new Random();
            return Enumerable.Range(1, 5).Select(index => new Order
            {
                Id = index,
                Title = "Title + " + index
            })
            .ToArray();
        }

        [HttpGet]
        public bool CanMoveToAddress(int key, [FromODataUri]Address address)
        {
            return true;
        }
    }


    [ODataModel("v2{data}")]
    public class MeOrderController : ControllerBase
    {
        [HttpGet]
        public Order Get()
        {
            return new Order { Id = 9, Title = "Singleton Title" };
        }
    }
}
