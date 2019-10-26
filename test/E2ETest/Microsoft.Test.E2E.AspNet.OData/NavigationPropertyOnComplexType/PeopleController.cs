// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Routing;
using Microsoft.Test.E2E.AspNet.OData.Common.Controllers;

namespace Microsoft.Test.E2E.AspNet.OData.NavigationPropertyOnComplexType
{
    public class PeopleController : TestODataController
    {
        private PeopleRepository _repo = new PeopleRepository();

        [HttpGet]
        [EnableQuery]
        public IEnumerable<Person> Get()
        {
            return _repo.People;
        }

        [HttpGet]
        [EnableQuery]
        public ITestActionResult Get([FromODataUri]int key)
        {
            Person person = _repo.people.FirstOrDefault(p => p.Id == key);
            if (person == null)
            {
                return NotFound();
            }

            return Ok(person);
        }

        [EnableQuery]
        public ITestActionResult GetHomeLocationFromPerson([FromODataUri]int key)
        {
            Person person = _repo.people.FirstOrDefault(p => p.Id == key);
            if (person == null)
            {
                return NotFound();
            }
            return Ok(person.HomeLocation);
        }
        /*
        [EnableQuery]
        public ITestActionResult GetLocationOfAddress([FromODataUri]int key)
        {
            Person person = _repo.people.FirstOrDefault(p => p.Id == key);
            if (person == null)
            {
                return NotFound();
            }
            return Ok(person.Location as Address);
        }*/

        [EnableQuery]
        public ITestActionResult GetHomeLocationOfGeolocation([FromODataUri]int key)
        {
            Person person = _repo.people.FirstOrDefault(p => p.Id == key);
            if (person == null)
            {
                return NotFound();
            }
            return Ok(person.HomeLocation as GeoLocation);
        }

        [EnableQuery]
        [ODataRoute("People({id})/Order")]
        public ITestActionResult GetOrdeFromPerson([FromODataUri]int id)
        {
            return Ok(_repo.people.FirstOrDefault(p => p.Id == id).Order);
        }

        [ODataRoute("People({id})/Location/ZipCode")]
        public ITestActionResult GetZipCode([FromODataUri]int id)
        {
            return Ok(_repo.people.FirstOrDefault().HomeLocation.ZipCode);
        }

        [ODataRoute("People({id})/Location/ZipCode/$ref")]
        public ITestActionResult CreateRefToZipCode([FromODataUri] int id, [FromBody] ZipCode zip)
        {
            return Ok(zip);
        }
    }
}
