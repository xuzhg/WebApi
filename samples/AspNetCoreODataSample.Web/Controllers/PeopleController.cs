// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using AspNetCoreODataSample.Web.Models;
using Microsoft.AspNet.OData;
using Microsoft.AspNetCore.Mvc;

namespace AspNetCoreODataSample.Web.Controllers
{
    public class PeopleController : ODataController
    {
        [EnableQuery]
        public IActionResult Get()
        {
            Person m = new Person
            {
                Id = 1,
                FirstName = "FirstName",
                LastName = "LastName"
            };

            Person m2 = new Person
            {
                Id = 2,
                FirstName = "SamName",
                LastName = "KKName"
            };

            return Ok(new[] { m, m2 });
        }

        [EnableQuery]
        public IActionResult Get([FromODataUri]string keyFirstName, [FromODataUri]string keyLastName)
        {
            Person m = new Person
            {
                FirstName = keyFirstName,
                LastName = keyLastName
            };

            return Ok(m);
        }
    }
}
