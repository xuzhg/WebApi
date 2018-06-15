// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Web.Http;
using AspNetODataSample.Web.Models;
using Microsoft.AspNet.OData;
using System.Collections.Generic;

namespace AspNetODataSample.Web.Controllers
{
    public class KISSController : ApiController
    {
        readonly List<KISS> kissList = new List<KISS>();
        public KISSController()
        {
            for (int i = 0; i < 1000; i++)
            {
                this.kissList.Add(new KISS
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = $"KISS_{i}",
                    Properties = new List<KeyValue> { new KeyValue("index", i.ToString()) }
                });
            }
        }


        /// <summary>
        /// This will throw an exception for the following filter:
        /// KISS?$filter=properties/any(prop: prop/value eq '139')
        /// </summary>
        /// <returns></returns>
        [EnableQuery]
        [Route("KISS")] // <-- this causes an exception
        public IQueryable<KISS> GetAll()
        {
            return this.kissList.AsQueryable();
        }
    }

    public class TodoItemsController : ODataController
    {
        private TodoItemContext _db = new TodoItemContext();

        public TodoItemsController()
        {
            if (!_db.TodoItems.Any())
            {
                foreach (var a in DataSource.GetTodoItems())
                {
                    _db.TodoItems.Add(a);
                }

                _db.SaveChanges();
            }
        }

        [EnableQuery]
        public IHttpActionResult Get()
        {
            return Ok(_db.TodoItems);
        }

        [EnableQuery]
        public IHttpActionResult Get(int key)
        {
            return Ok(_db.TodoItems.FirstOrDefault(c => c.Id == key));
        }

        [HttpPost]
        public IHttpActionResult Post(TodoItem item)
        {
            _db.TodoItems.Add(item);
            _db.SaveChanges();
            return Created(item);
        }
    }
}