// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using AspNetCoreODataSample.Web.Models;
using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Query;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using static AspNetCoreODataSample.Web.Models.EdmModelBuilder;

namespace AspNetCoreODataSample.Web.Controllers
{
    public class PeopleController : ODataController
    {
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

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public class MyQueryAttribute : EnableQueryAttribute
    {
        public override void ValidateQuery(HttpRequest request, ODataQueryOptions queryOptions)
        {
            //Do custom stuff
            base.ValidateQuery(request, queryOptions);
        }
    }


    public class EntityController : ODataController
    {
        private static IList<Entity> _entity = new List<Entity>
        {
            new Entity
            {
                Id = 1
            },
            new Entity{Id = 2},
            new Entity{Id = 3},
            new Entity{Id = 4},
        };

        [MyQuery]
        public IQueryable<Entity> Get()
        {
            return _entity.AsQueryable();
        }

        [MyQuery]
        public SingleResult<Entity> Get([FromODataUri] int key)
        {
            var entity = _entity.Where(x => x.Id == key);
            return SingleResult.Create(entity.AsQueryable());
        }
    }
}
