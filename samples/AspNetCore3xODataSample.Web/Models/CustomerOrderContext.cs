// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace AspNetCore3xODataSample.Web.Models
{
    public class CustomerOrderContext : DbContext
    {
        public CustomerOrderContext(DbContextOptions<CustomerOrderContext> options)
            : base(options)
        {
        }

        public DbSet<Customer> Customers { get; set; }

        public DbSet<Order> Orders { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Customer>().OwnsOne(c => c.HomeAddress).WithOwner();
        }


    }

    public class ProductDepartmentContext : DbContext
    {
        public DbSet<Product> Products { get; set; }
        public DbSet<Department> Departments { get; set; }

        public ProductDepartmentContext(DbContextOptions<ProductDepartmentContext> options) : base(options)
        {
        }
    }

    public class Product
    {
        public int ProductId { get; set; }

        public string ProductName { get; set; }

        public Department Department { get; set; }

        public IList<Department> DepartList { get; set; }

        public ICollection<Department> DepartCollection { get; set; }

        //        public IEnumerable<Department> DepartEnumer { get; set; }
    }

    public class Department
    {
        public int Id { get; set; }

        public string DepName { get; set; }
    }
}
