// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNet.OData;

namespace Microsoft.Test.E2E.AspNet.OData.Aggregation.Paged
{
    public class CustomersController
    {
        [EnableQuery(PageSize = 5)]
        public IQueryable<Customer> Get()
        {
            return Generate().AsQueryable<Customer>();
        }

        public IList<Customer> Generate()
        {
            IList<Customer> customers = new List<Customer>();
            for (int i = 1; i < 10; i++)
            {
                var customer = new Customer
                {
                    Id = i,
                    Name = "Customer" + i % 2,
                    Bucket = i % 2 == 0 ? (CustomerBucket?)CustomerBucket.Small : null,
                    Order = new Order
                    {
                        Id = i,
                        Name = "Order" + i % 2,
                        Price = i * 100
                    },
                    Address = new Address
                    {
                        Name = "City" + i % 2,
                        Street = "Street" + i % 2,
                    }
                };

                customers.Add(customer);
            }

            customers.Add(new Customer()
            {
                Id = 10,
                Name = null,
                Bucket = CustomerBucket.Big,
                Address = new Address
                {
                    Name = "City1",
                    Street = "Street",
                },
                Order = new Order
                {
                    Id = 10,
                    Name = "Order" + 10 % 2,
                    Price = 0
                },
            });

            return customers;
        }
    }
}
