// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AspNetCoreODataSample.Web.Models;
using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Query;
using Microsoft.AspNetCore.Mvc;

namespace AspNetCoreODataSample.Web.Controllers
{
    public class MyEntitiesController : ODataController
    {
        [EnableQuery]
        public IEnumerable<MyEntity> Get(ODataQueryOptions<MyEntity> options)
        {
            IList<MyEntity> entities = new List<MyEntity>
            {
                new MyEntity { Id = 1, MyName = "Sam" },
                new MyEntity { Id = 2, MyName = "Sannie" },
                new MyEntity { Id = 3, MyName = "Sem" }
            };

            return entities;
        }
    }

    public class MoviesController : ODataController
    {
        private readonly MovieContext _context;

        private readonly IList<Movie> _inMemoryMovies;

        public MoviesController(MovieContext context)
        {
            _context = context;

            if (_context.Movies.Count() == 0)
            {
                Movie m = new Movie
                {
                    Title = "Conan",
                    ReleaseDate = new DateTimeOffset(new DateTime(2017, 3, 3)),
                    Genre = Genre.Comedy,
                    Price = 1.99m
                };
                _context.Movies.Add(m);
                _context.SaveChanges();
            }

            _inMemoryMovies = new List<Movie>
            {
                new Movie
                {
                    ID = 1,
                    Title = "Conan",
                    ReleaseDate = new DateTimeOffset(new DateTime(2018, 3, 3)),
                    Genre = Genre.Comedy,
                    Price = 1.99m
                },
                new Movie
                {
                    ID = 2,
                    Title = "James",
                    ReleaseDate = new DateTimeOffset(new DateTime(2017, 3, 3)),
                    Genre = Genre.Adult,
                    Price = 91.99m
                }
            };
        }

        [EnableQuery]
        public IActionResult Get()
        {
            if (Request.Path.Value.Contains("efcore"))
            {
                return Ok(_context.Movies);
            }
            else
            {
                return Ok(_inMemoryMovies);
            }
        }

        [EnableQuery]
        public IActionResult Get(int key)
        {
            Movie m;
            if (Request.Path.Value.Contains("efcore"))
            {
                m = _context.Movies.FirstOrDefault(c => c.ID == key);
            }
            else
            {
                m = _inMemoryMovies.FirstOrDefault(c => c.ID == key);
            }

            if (m == null)
            {
                return NotFound();
            }

            return Ok(m);
        }
    }
}
