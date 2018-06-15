// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Runtime.Serialization;

namespace AspNetCoreODataSample.Web.Models
{
    [DataContract]

    public class Person
    {
        [DataMember]
        public int Id { get; set; }
        [DataMember(Name ="first_name")]
        public string FirstName { get; set; }
        [DataMember(Name ="last_name")]
        public string LastName { get; set; }

    }
}
