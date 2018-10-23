// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using AspNetCoreODataSample.Web.Models;
using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Query;
using Microsoft.AspNet.OData.Routing;
using Microsoft.AspNetCore.Mvc;

namespace AspNetCoreODataSample.Web.Controllers
{
    public class UsersController : ODataController
    {/*
        [EnableQuery]
        public IActionResult Get([FromODataUri]string keyFirstName, [FromODataUri]string keyLastName)
        {
            Person m = new Person
            {
                FirstName = keyFirstName,
                LastName = keyLastName,
                DynamicProperties = new Dictionary<string, object>
                {
                    { "abc", "abcValue" }
                },
                MyLevel = Level.High
            };

            return Ok(m);
        }*/

        [EnableQuery(AllowedQueryOptions = AllowedQueryOptions.Expand, MaxExpansionDepth = 0)]
        [ODataRoute("Users({key})/memberOf/family/members")]
        [HttpGet]
        public IActionResult ReturnMembers(int key, ODataQueryOptions<Member> options)
        {
            IList<Member> members = new List<Member>
            {
                new Member
                {
                    Id = 1,
                    family = new Family { id = 12},
                    user = new User { id = 13 }
                },
                new Member
                {
                    Id = 2,
                    family = new Family { id = 22 },
                    user = new User { id = 23}
                }
            };

            return Ok(members);
        }
    }
}
