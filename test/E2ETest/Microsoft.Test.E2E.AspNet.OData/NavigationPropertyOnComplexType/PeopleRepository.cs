// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Test.E2E.AspNet.OData.NavigationPropertyOnComplexType
{
    public class PeopleRepository
    {
        public List<Person> People { get; private set; }

        public IDictionary<string, object> propertyBag = new Dictionary<string, object>();

        public PeopleRepository()
        {
            var zipCodes = new List<ZipCode>
            {
                new ZipCode { Zip = 98052, City = "Redmond", State="Washington"},
                new ZipCode { Zip = 35816, City = "Huntsville", State = "Alabama"},
                new ZipCode { Zip = 10048, City = "New York", State = "New York"}
            };

            IDictionary<string, object> propertyBag = new Dictionary<string, object>
            {
                {
                    "DynamicAddress",
                    new Address
                    {
                        Street = "",
                        Emails = new List<string>
                        {
                            "abc@1.com",
                            "xyz@2.com"
                        }
                    }
                }
            };

            var repoLocations = new Address[]
            {
                new Address
                {
                    Street = "110th",
                    TaxNo = 19,
                    Emails = new [] { "E1", "E3", "E2" },
                    ZipCode = zipCodes[0],
                    DetailCodes = zipCodes
                },
                new GeoLocation
                {
                    Street = "120th",
                    TaxNo = 17,
                    Emails = new [] { "E7", "E4", "E5" },
                    Latitude = "12",
                    Longitude = "22",
                    ZipCode = zipCodes[1],
                    DetailCodes = zipCodes,
                    Area = zipCodes[2]
                },
                new Address
                {
                    Street = "130th",
                    TaxNo = 18,
                    Emails = new [] { "E9", "E6", "E10" },
                    ZipCode = zipCodes[2],
                    DetailCodes = zipCodes
                },
            };

            People = new List<Person>
            {
                new Person
                {
                    Id = 1,
                    Name = "Kate",
                    Age = 5,
                    Taxes = new [] { 7, 5, 9 },
                    HomeLocation = repoLocations[0],
                    RepoLocations = repoLocations,
                    PreciseLocation = null, // by design
                    Order = new OrderInfo
                    {
                        BillLocation = repoLocations[0],
                        SubInfo = null
                    }
                },
                new Person
                {
                    Id = 2,
                    Name = "Lewis",
                    Age = 6 ,
                    Taxes = new [] { 1, 5, 2 },
                    HomeLocation = new GeoLocation{ ZipCode = zipCodes[1], Street = "110th", Latitude = "12.211", Longitude ="231.131" },
                    RepoLocations = repoLocations,
                    PreciseLocation = null, // by design
                    Order = new OrderInfo
                    {
                        BillLocation = new Address{ ZipCode = zipCodes[0], Street = "110th" }
                    }
                },
                new Person
                {
                    Id = 3,
                    Name = "Carlos",
                    Age = 7,
                    HomeLocation = null, // by design
                    RepoLocations = repoLocations,
                    Order = new OrderInfo
                    {
                        BillLocation = new Address{ ZipCode = zipCodes[0], Street = "110th" }
                    },
                    PreciseLocation = new GeoLocation{Area = zipCodes[2], Latitude = "12", Longitude = "22", Street = "50th", ZipCode = zipCodes[1]}
                },
                new Person
                {
                    Id = 4,
                    Name = "Jones",
                    Age = 9,
                    HomeLocation = new Address{ ZipCode = zipCodes[2], Street = "110th" },
                    RepoLocations = repoLocations,
                    PreciseLocation = new GeoLocation{Area = zipCodes[2], Latitude = "12", Longitude = "22", Street = "50th", ZipCode = zipCodes[1]},
                    Order = new OrderInfo
                    {
                        BillLocation = new Address{ ZipCode = zipCodes[0], Street = "110th" },
                        SubInfo = new OrderInfo{ BillLocation = new Address{ ZipCode = zipCodes[1], Street = "110th" }}
                    }
                },
                new Person
                {
                    Id = 5,
                    Name = "Park",
                    Age = 17,
                    Order = new OrderInfo()
                    {
                        propertybag = propertyBag
                    }
                }
            };
        }
    }
}