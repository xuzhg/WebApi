
using System.Collections.Generic;
using System.Linq;
using AspNetCore3xODataSample.Web.Models;
using Microsoft.AspNet.OData;
using Microsoft.AspNetCore.Mvc;

namespace AspNetCore3xODataSample.Web.Controllers
{
    public class CustomersController : ODataController
    {
        private IList<Customer> _customers = new List<Customer>
        {
            new Customer{ Id = 1, Order = new Order { Id = 11 }},
            new Customer{ Id = 2, Order = new Order { Id = 21 }},
            new Customer{ Id = 3, Order = new Order { Id = 31 }},
        };

        [EnableQuery]
        public IActionResult Get()
        {
            return Ok(_customers);
        }

        [EnableQuery]
        public IActionResult Get(int key)
        {
            return Ok(_customers.FirstOrDefault(c => c.Id == key));
        }
    }
}
