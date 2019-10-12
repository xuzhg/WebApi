// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using AspNetCoreODataSample.Web.Models;
using Microsoft.AspNet.OData;
using Microsoft.AspNetCore.Mvc;

namespace AspNetCoreODataSample.Web.Controllers
{
    public class CustomersController : ODataController
    {
        public static IList<Customer> Customers;

        static CustomersController()
        {
            Customers = new List<Customer>
            {
                new Customer
                {
                    CustomerId = 1,
                    Name = "John",
                    HomeAddress = new Address
                    {
                        RelatedCity = new City { Id = 31, Name = "Redmond"},
                        Street = "148TH AVE NE"
                    },
                    VipOrder = new Order
                    {
                        Id = 11,
                        Title = "John's Order"
                    }
                },
                new Customer
                {
                    CustomerId = 2,
                    Name = "Smith",
                    HomeAddress = new Address
                    {
                        RelatedCity = new City { Id = 31, Name = "Bellevue"},
                        Street = "8TH ST"
                    },
                    VipOrder = new Order
                    {
                        Id = 21,
                        Title = "Smith's Order"
                    }
                }
            };
        }

        [EnableQuery]
        public IActionResult Get()
        {
            return Ok(Customers);
        }

        [EnableQuery]
        public IActionResult Get(int key)
        {
            Customer c = Customers.FirstOrDefault(k => k.CustomerId == key);
            if (c == null)
            {
                return NotFound();
            }

            return Ok(c);
        }
    }
}
