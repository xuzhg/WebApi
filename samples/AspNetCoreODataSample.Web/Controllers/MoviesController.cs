// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using AspNetCoreODataSample.Web.Models;
using Dynasource.Models;
using Dynasource.Models.Constants;
using Microsoft.AspNet.OData;
using Microsoft.AspNetCore.Mvc;

namespace AspNetCoreODataSample.Web.Controllers
{
    public class companiesController : ODataController
    {
        [EnableQuery]
        public IActionResult Get()
        {
            company c = new company
            {
                id = Guid.NewGuid(),
                company_size = new company_size { Id = Guid.NewGuid() },
                locations = new List<location>
                {
                    new location
                    {
                        id = Guid.NewGuid(),
                        location_type = new location_type
                        {
                            Id = Guid.NewGuid()
                        }
                    }
                }
            };

            return Ok(new company[] { c });
        }

        [EnableQuery(MaxExpansionDepth = 4)]
        public IActionResult Get([FromODataUri]Guid key)
        {
            company c = new company
            {
                id = key,
                company_size = new company_size { Id = Guid.NewGuid() },
                locations = new List<location>
                {
                    new location
                    {
                        id = Guid.NewGuid(),
                        location_type = new location_type
                        {
                            Id = Guid.NewGuid()
                        }
                    }
                },
                experts = new List<expert_summary>
                {
                    new expert_summary
                    {
                        id = Guid.NewGuid(),
                        job_function = new role { Id = Guid.NewGuid() },
                        languages = new List<language_summary>
                        {
                            new language_summary { id = Guid.NewGuid(), language = new language { Id = Guid.NewGuid() }}
                        },
                        location = new location {
                            id = Guid.NewGuid(),
                            location_type = new location_type
                            {
                                Id = Guid.NewGuid()
                            }
                        }
                    }
                },
                communities = new List<community_summary>
                {
                    new community_summary
                    {
                         id = Guid.NewGuid(),
                         solutions = new List<community_solution>
                         {
                             new community_solution { id = Guid.NewGuid() }
                         }
                    }
                }
            };

            return Ok( c);
        }
    }

    public class RoutesController : ODataController
    {
        [EnableQuery]
        public IActionResult Get()
        {
            Route r = new Route { Id = Guid.NewGuid() };
            return Ok(new Route[] { r });
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
