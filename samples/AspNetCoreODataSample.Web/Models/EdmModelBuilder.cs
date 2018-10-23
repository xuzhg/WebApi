// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using Microsoft.AspNet.OData.Builder;
using Microsoft.OData.Edm;
using System.Collections.Generic;

namespace AspNetCoreODataSample.Web.Models
{
    public static class EdmModelBuilder
    {
        private static IEdmModel _edmModel;

        public static IEdmModel GetEdmModel()
        {
            if (_edmModel == null)
            {
                var builder = new ODataConventionModelBuilder();
                builder.EntitySet<Movie>("Movies");
                _edmModel = builder.GetEdmModel();
            }

            return _edmModel;
        }

        public static IEdmModel GetCompositeModel()
        {
            var builder = new ODataConventionModelBuilder();
            builder.EntitySet<Person>("People");
            var type = builder.EntitySet<Person>("Person").EntityType;
            type.HasKey(x => new { x.FirstName, x.LastName });
            return builder.GetEdmModel();
        }

        public static IEdmModel GetModel()
        {
            var builder = new ODataConventionModelBuilder();
            builder.EntitySet<User>("Users");
            builder.EntitySet<User>("Families");
            builder.EntitySet<Member>("Members");
            return builder.GetEdmModel();
        }
    }

    public class User
    {
        public int id { get; set; }

        public Member memberOf { get; set; }
    }

    public class Family
    {
        public int id { get; set; }
        public IList<Member> members { get; set; }
    }

    public class Member
    {
        public int Id { get; set; }

        public User user { get; set; }

        public Family family { get; set; }
    }
}
