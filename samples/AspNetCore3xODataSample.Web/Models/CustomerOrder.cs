using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AspNetCore3xODataSample.Web.Models
{
    public class Customer
    {
        public int Id { get; set; }

        public Order Order { get; set; }

    }

    public class Order
    {
        public int Id { get; set; }
    }
}
