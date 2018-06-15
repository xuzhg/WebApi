// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace AspNetODataSample.Web.Models
{
    public class TodoItem
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public bool IsComplete { get; set; }
    }

    public class KeyValue
    {
        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }

        public KeyValue(string key, string value)
        {
            this.Key = key;
            this.Value = value;
        }

        public KeyValue()
        { }
    }

    public class KISS
    {
        /// <summary>
        ///     Guid Id, auto populated field.
        /// </summary>
        [Key]
        [JsonProperty("id")]
        public virtual string Id { get; set; }

        [JsonProperty("catalogItems")]
        public string Name { get; set; }

        /// <summary>
        ///     List of properties that describe the product.
        /// </summary>
        [JsonProperty("properties")]
        public IEnumerable<KeyValue> Properties { get; set; } = new List<KeyValue>();
    }
}