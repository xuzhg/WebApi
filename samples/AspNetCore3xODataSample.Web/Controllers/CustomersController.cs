// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using AspNetCore3xODataSample.Web.Models;
using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Query;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AspNetCore3xODataSample.Web.Controllers
{
    public class CustomersController : ODataController
    {
        private readonly CustomerOrderContext _context;

        public CustomersController(CustomerOrderContext context)
        {
            _context = context;

            if (_context.Customers.Count() == 0)
            {
                IList<Customer> customers = new List<Customer>
                {
                    new Customer
                    {
                        Name = "Jonier",
                        CreatedDate = new DateTimeOffset(2020, 2, 19, 11, 12, 13, TimeSpan.Zero),
                        HomeAddress = new Address { City = "Redmond", Street = "156 AVE NE"},
                        FavoriteAddresses = new List<Address>
                        {
                            new Address { City = "Redmond", Street = "256 AVE NE"},
                            new Address { City = "Redd", Street = "56 AVE NE"},
                        },
                        Order = new Order { Title = "104m" },
                        Orders = Enumerable.Range(0, 2).Select(e => new Order { Title = "abc" + e }).ToList()
                    },
                    new Customer
                    {
                        Name = "Sam",
                        CreatedDate = new DateTimeOffset(1998, 4, 29, 1, 2, 3, TimeSpan.Zero),

                        HomeAddress = new Address { City = "Bellevue", Street = "Main St NE"},
                        FavoriteAddresses = new List<Address>
                        {
                            new Address { City = "Red4ond", Street = "456 AVE NE"},
                            new Address { City = "Re4d", Street = "51 NE"},
                        },
                        Order = new Order { Title = "Zhang" },
                        Orders = Enumerable.Range(0, 2).Select(e => new Order { Title = "xyz" + e }).ToList()
                    },
                    new Customer
                    {
                        Name = "Peter",
                        CreatedDate = new DateTimeOffset(1993, 11, 9, 4, 2, 53, TimeSpan.Zero),
                        HomeAddress = new Address {  City = "Hollewye", Street = "Main St NE"},
                        FavoriteAddresses = new List<Address>
                        {
                            new Address { City = "R4mond", Street = "546 NE"},
                            new Address { City = "R4d", Street = "546 AVE"},
                        },
                        Order = new Order { Title = "Jichan" },
                        Orders = Enumerable.Range(0, 2).Select(e => new Order { Title = "ijk" + e }).ToList()
                    },
                };

                foreach (var customer in customers)
                {
                    _context.Customers.Add(customer);
                    _context.Orders.Add(customer.Order);
                    _context.Orders.AddRange(customer.Orders);
                }

                _context.SaveChanges();
            }
        }

        [EnableQuery]
        public IActionResult Get()
        {
            // Be noted: without the NoTracking setting, the query for $select=HomeAddress with throw exception:
            // A tracking query projects owned entity without corresponding owner in result. Owned entities cannot be tracked without their owner...
            _context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

            return Ok(_context.Customers);
        }

        [EnableQuery]
        public IActionResult Get(int key)
        {
            return Ok(_context.Customers.FirstOrDefault(c => c.Id == key));
        }

        /*
         Request: Delete http://localhost:5000/odata/Customers(2)
         If-Match: W/"MTk5OC0wNC0yOVQwMTowMjowM1o="
         It works fine.
         */
        [HttpDelete]
        public IActionResult Delete(int key, ODataQueryOptions<Customer> options)
        {
            var query = _context.Customers.Where(c => c.Id == key);
            if (query == null)
            {
                return NotFound();
            }

            if (options.IfMatch != null)
            {
                Customer customer = options.IfMatch.ApplyTo(query).FirstOrDefault();
                if (customer != null)
                {
                    return StatusCode(204);
                }
            }

            return StatusCode(200);
        }
    }
}
