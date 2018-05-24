// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace AspNetCoreODataSample.Web.Models
{
    public class Person
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
    }
}

namespace Dynasource.Models.Constants
{
    public class BaseConstant
    {
        public Guid Id { get; set; }
    }

    public class location_type : BaseConstant
    {

    }

    public class company_size : BaseConstant
    {

    }

    public class language : BaseConstant
    { }

    public class role : BaseConstant
    { }
}


namespace Dynasource.Models
{
    using Dynasource.Models.Constants;

    public class company
    {
        public Guid id { get; set; }

        public company_size company_size { get; set; }

        public IList<location> locations { get; set; }

        public IList<expert_summary> experts { get; set; }

        public IList<community_summary> communities { get; set; }

        public IDictionary<string, object> Dynamics { get; set; }
    }

    public class location
    {
        public Guid id { get; set; }

        public location_type location_type { get; set; }
    }

    public class expert_summary
    {
        public Guid id { get; set; }

        public role job_function { get; set; }

        public IList<language_summary> languages { get; set; }

        public location location { get; set; }
    }

    public class language_summary
    {
        public Guid id { get; set; }

        public language language { get; set; }
    }

    public class community_summary
    {
        public Guid id { get; set; }

        public IList<community_solution> solutions { get; set; }
    }

    public class community_solution
    {
        public Guid id { get; set; }
    }
}

