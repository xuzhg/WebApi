// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNet.OData.Builder;
using Microsoft.OData.Edm;

namespace Microsoft.Test.E2E.AspNet.OData.NavigationPropertyOnComplexType
{
    public class Person
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public int Age { get; set; }

        public IList<int> Taxes { get; set; }

        public Address HomeLocation { get; set; }

        public IList<Address> RepoLocations { get; set; }

        public OrderInfo Order { get; set; }
    }

    public class OrderInfo
    {
        public Address BillLocation { get; set; }

        public OrderInfo SubInfo { get; set; }

        public IDictionary<string, object> propertybag { get; set; }
    }

    public class Address
    {
        public string Street { get; set; }

        public IList<string> Emails { get; set; }

        public ZipCode ZipCode { get; set; }

        public IList<ZipCode> DetailCodes { get; set; }
    }

    public class ZipCode
    {
        [Key]
        public int Zip { get; set; }

        public string City { get; set; }

        public string State { get; set; }
    }

    public class GeoLocation : Address
    {
        public string Latitude { get; set; }

        public string Longitude { get; set; }

        public ZipCode Area { get; set; }
    }

    public class ModelGenerator
    {
        // Builds the EDM model for the OData service.
        public static IEdmModel GetConventionalEdmModel()
        {
            var modelBuilder = new ODataConventionModelBuilder();
            modelBuilder.EntitySet<Person>("People");
            modelBuilder.EntitySet<ZipCode>("ZipCodes");

            modelBuilder.Namespace = typeof(Person).Namespace;
            return modelBuilder.GetEdmModel();
        }
    }
}