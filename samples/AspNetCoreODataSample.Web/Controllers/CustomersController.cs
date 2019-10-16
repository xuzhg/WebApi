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
                        Street = "148TH AVE NE",
                        Region = "Redmond",
                        Emails = new List<string>{ "abc@look.com", "xyz@eye.com" }
                    },
                    Addresses = new List<Address>
                    {
                        new CnAddress { Street = "LianHua Rd", Region = "Shanghai", PostCode = "201501", Emails = new List<string>{ "shangh_abc@look.com", "shangh_xyz@eye.com" }, RelatedCity = new City { Id = 81, Name = "Redm81"}, },
                        new CnAddress { Street = "Tiananmen Rd", Region = "Beijing", PostCode = "101501", Emails = new List<string>{ "beijing_abc@look.com", "beijing_xyz@eye.com" }, RelatedCity = new City { Id = 82, Name = "Redm82"},},
                        new UsAddress { Street = "Klahanie Rd", Region = "Remond", ZipCode = "98029", Emails = new List<string>{ "Remond_abc@look.com", "Remond_xyz@eye.com" }, RelatedCity = null },
                        new UsAddress { Street = "Sammamish Rd", Region = "Sammamish", ZipCode = "98072", Emails = new List<string>{ "Sammamish_abc@look.com", "Sammamish_xyz@eye.com" }, RelatedCity = new City { Id = 83, Name = "Redm83"}, },
                    },
                    VipOrder = new Order
                    {
                        Id = 11,
                        Title = "John's Order",
                    }
                },
                new Customer
                {
                    CustomerId = 2,
                    Name = "Smith",
                    HomeAddress = new Address
                    {
                        RelatedCity = new City { Id = 31, Name = "Bellevue"},
                        Street = "8TH ST",
                        Region = "Bellevue",
                        Emails = new List<string>{ "efj@alis.com", "783@eye.com" }
                    },
                    Addresses = new List<Address>
                    {
                        new CnAddress { Street = "Shangzhong Rd", Region = "Zhejiang", PostCode = "301501", Emails = new List<string>{ "Zhejiang_abc@look.com", "Zhejiang_xyz@eye.com" }, RelatedCity = new City { Id = 51, Name = "Redm51"}, },
                        new CnAddress { Street = "Chongqiu Rd", Region = "Jiangsu", PostCode = "401501", Emails = new List<string>{ "Jiangsu_abc@look.com", "Jiangsu_xyz@eye.com" }, RelatedCity = null},
                        new UsAddress { Street = "Issaquah Rd", Region = "Issaquah", ZipCode = "98031", Emails = new List<string>{ "Issaquah_abc@look.com", "Issaquah_xyz@eye.com" }, RelatedCity = new City { Id = 52, Name = "Redm52"}, },
                        new UsAddress { Street = "Bel-Red Rd", Region = "Redmond", ZipCode = "98052", Emails = new List<string>{ "Red_abc@look.com", "Red_xyz@eye.com" }, RelatedCity = new City { Id = 53, Name = "Redm53"}, },
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
