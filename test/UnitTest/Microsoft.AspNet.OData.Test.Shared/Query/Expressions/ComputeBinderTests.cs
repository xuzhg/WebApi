// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNet.OData.Interfaces;
using Microsoft.AspNet.OData.Query;
using Microsoft.AspNet.OData.Query.Expressions;
using Microsoft.AspNet.OData.Test.Abstraction;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using Microsoft.OData.UriParser.Aggregation;
using Xunit;

namespace Microsoft.AspNet.OData.Test.Query.Expressions
{
    public class ComputeBinderTests
    {
        private IQueryable _computeCustomers;
        public ComputeBinderTests()
        {
            IList<ComputeCustomer> customers = new List<ComputeCustomer>();
            ComputeCustomer customer = new ComputeCustomer
            {

            };

            customers.Add(customer);
            _computeCustomers = customers.AsQueryable();
        }

#if false
        [Fact]
        public void SingleGroupBy()
        {
            var filters = VerifyQueryDeserialization(
                "groupby((ProductName))",
                ".GroupBy($it => new GroupByWrapper() {GroupByContainer = new LastInChain() {Name = ProductName, Value = $it.ProductName, }, })"
                + ".Select($it => new AggregationWrapper() {GroupByContainer = $it.Key.GroupByContainer, })");
        }

        [Fact]
        public void MultipleGroupBy()
        {
            var filters = VerifyQueryDeserialization(
                "groupby((ProductName, SupplierID))",
                ".GroupBy($it => new GroupByWrapper() {GroupByContainer = new AggregationPropertyContainer() {Name = SupplierID, Value = Convert($it.SupplierID), Next = new LastInChain() {Name = ProductName, Value = $it.ProductName, }, }, })"
                + ".Select($it => new AggregationWrapper() {GroupByContainer = $it.Key.GroupByContainer, })");
        }

        [Fact]
        public void NavigationGroupBy()
        {
            var filters = VerifyQueryDeserialization(
                "groupby((Category/CategoryName))",
                ".GroupBy($it => new GroupByWrapper() {GroupByContainer = new NestedPropertyLastInChain() {Name = Category, NestedValue = new GroupByWrapper() {GroupByContainer = new LastInChain() {Name = CategoryName, Value = $it.Category.CategoryName, }, }, }, })"
                + ".Select($it => new AggregationWrapper() {GroupByContainer = $it.Key.GroupByContainer, })");
        }

        [Fact]
        public void NestedNavigationGroupBy()
        {
            var filters = VerifyQueryDeserialization(
                "groupby((Category/Product/ProductName))",
                ".GroupBy($it => new GroupByWrapper() {GroupByContainer = new NestedPropertyLastInChain() {Name = Category, NestedValue = new GroupByWrapper() {GroupByContainer = new NestedPropertyLastInChain() {Name = Product, NestedValue = new GroupByWrapper() {GroupByContainer = new LastInChain() {Name = ProductName, Value = $it.Category.Product.ProductName, }, }, }, }, }, })"
                + ".Select($it => new AggregationWrapper() {GroupByContainer = $it.Key.GroupByContainer, })");
        }

        [Fact]
        public void NavigationMultipleGroupBy()
        {
            var filters = VerifyQueryDeserialization(
                "groupby((Category/CategoryName, SupplierAddress/State))",
                ".GroupBy($it => new GroupByWrapper() {GroupByContainer = new NestedProperty() {Name = SupplierAddress, NestedValue = new GroupByWrapper() {GroupByContainer = new LastInChain() {Name = State, Value = $it.SupplierAddress.State, }, }, Next = new NestedPropertyLastInChain() {Name = Category, NestedValue = new GroupByWrapper() {GroupByContainer = new LastInChain() {Name = CategoryName, Value = $it.Category.CategoryName, }, }, }, }, })"
                + ".Select($it => new AggregationWrapper() {GroupByContainer = $it.Key.GroupByContainer, })");
        }

        [Fact]
        public void NestedNavigationMultipleGroupBy()
        {
            var filters = VerifyQueryDeserialization(
                "groupby((Category/Product/ProductName, Category/Product/UnitPrice))",
                ".GroupBy($it => new GroupByWrapper() {GroupByContainer = new NestedPropertyLastInChain() {Name = Category, NestedValue = new GroupByWrapper() {GroupByContainer = new NestedPropertyLastInChain() {Name = Product, NestedValue = new GroupByWrapper() {GroupByContainer = new AggregationPropertyContainer() {Name = UnitPrice, Value = Convert($it.Category.Product.UnitPrice), Next = new LastInChain() {Name = ProductName, Value = $it.Category.Product.ProductName, }, }, }, }, }, }, })"
                + ".Select($it => new AggregationWrapper() {GroupByContainer = $it.Key.GroupByContainer, })");
        }

        [Fact]
        public void SingleDynamicGroupBy()
        {
            var filters = VerifyQueryDeserialization<DynamicProduct>(
                "groupby((ProductProperty))",
                ".GroupBy($it => new GroupByWrapper() {GroupByContainer = new LastInChain() {Name = ProductProperty, Value = IIF($it.ProductProperties.ContainsKey(ProductProperty), $it.ProductPropertiesProductProperty, null), }, })"
                + ".Select($it => new AggregationWrapper() {GroupByContainer = $it.Key.GroupByContainer, })");
        }


        [Fact]
        public void SingleSum()
        {
            var filters = VerifyQueryDeserialization(
                "aggregate(SupplierID with sum as SupplierID)",
                ".GroupBy($it => new NoGroupByWrapper())"
                + ".Select($it => new NoGroupByAggregationWrapper() {Container = new LastInChain() {Name = SupplierID, Value = Convert(Convert($it).Sum($it => $it.SupplierID)), }, })");
        }

        [Fact]
        public void SingleDynamicSum()
        {
            var filters = VerifyQueryDeserialization<DynamicProduct>(
                "aggregate(ProductProperty with sum as ProductProperty)",
                ".GroupBy($it => new NoGroupByWrapper())"
                + ".Select($it => new NoGroupByAggregationWrapper() {Container = new LastInChain() {Name = ProductProperty, Value = Convert(Convert($it).Sum($it => IIF($it.ProductProperties.ContainsKey(ProductProperty), $it.ProductPropertiesProductProperty, null).SafeConvertToDecimal())), }, })");
        }

        [Fact]
        public void SingleMin()
        {
            var filters = VerifyQueryDeserialization(
                "aggregate(SupplierID with min as SupplierID)",
                ".GroupBy($it => new NoGroupByWrapper())"
                + ".Select($it => new NoGroupByAggregationWrapper() {Container = new LastInChain() {Name = SupplierID, Value = Convert(Convert($it).Min($it => $it.SupplierID)), }, })");
        }

        [Fact]
        public void SingleDynamicMin()
        {
            var filters = VerifyQueryDeserialization<DynamicProduct>(
                "aggregate(ProductProperty with min as MinProductProperty)",
                ".GroupBy($it => new NoGroupByWrapper())"
                + ".Select($it => new NoGroupByAggregationWrapper() {Container = new LastInChain() {Name = MinProductProperty, Value = Convert($it).Min($it => IIF($it.ProductProperties.ContainsKey(ProductProperty), $it.ProductPropertiesProductProperty, null)), }, })");
        }

        [Fact]
        public void SingleMax()
        {
            var filters = VerifyQueryDeserialization(
                "aggregate(SupplierID with max as SupplierID)",
                ".GroupBy($it => new NoGroupByWrapper())"
                + ".Select($it => new NoGroupByAggregationWrapper() {Container = new LastInChain() {Name = SupplierID, Value = Convert(Convert($it).Max($it => $it.SupplierID)), }, })");
        }

        [Fact]
        public void SingleAverage()
        {
            var filters = VerifyQueryDeserialization(
                "aggregate(UnitPrice with average as AvgUnitPrice)",
                ".GroupBy($it => new NoGroupByWrapper())"
                + ".Select($it => new NoGroupByAggregationWrapper() {Container = new LastInChain() {Name = AvgUnitPrice, Value = Convert(Convert($it).Average($it => $it.UnitPrice)), }, })");
        }

        [Fact]
        public void SingleCountDistinct()
        {
            var filters = VerifyQueryDeserialization(
                "aggregate(SupplierID with countdistinct as Count)",
                ".GroupBy($it => new NoGroupByWrapper())"
                + ".Select($it => new NoGroupByAggregationWrapper() {Container = new LastInChain() {Name = Count, Value = Convert(Convert($it).Select($it => $it.SupplierID).Distinct().LongCount()), }, })");
        }

        [Fact]
        public void MultipleAggregate()
        {
            var filters = VerifyQueryDeserialization(
                "aggregate(SupplierID with sum as SupplierID, CategoryID with sum as CategoryID)",
                ".GroupBy($it => new NoGroupByWrapper())"
                + ".Select($it => new NoGroupByAggregationWrapper() {Container = new AggregationPropertyContainer() {Name = CategoryID, Value = Convert(Convert($it).Sum($it => $it.CategoryID)), Next = new LastInChain() {Name = SupplierID, Value = Convert(Convert($it).Sum($it => $it.SupplierID)), }, }, })");
        }

        [Fact]
        public void GroupByAndAggregate()
        {
            var filters = VerifyQueryDeserialization(
                "groupby((ProductName), aggregate(SupplierID with sum as SupplierID))",
                ".Select($it => new FlatteningWrapper`1() {Source = $it, GroupByContainer = new LastInChain() {Name = Property0, Value = Convert($it.SupplierID), }, })"
                + ".GroupBy($it => new GroupByWrapper() {GroupByContainer = new LastInChain() {Name = ProductName, Value = $it.Source.ProductName, }, })"
                + ".Select($it => new AggregationWrapper() {GroupByContainer = $it.Key.GroupByContainer, Container = new LastInChain() {Name = SupplierID, Value = Convert(Convert($it).Sum($it => Convert($it.GroupByContainer.Value))), }, })");
        }

        [Fact]
        public void GroupByAndMultipleAggregations()
        {
            var filters = VerifyQueryDeserialization(
                "groupby((ProductName), aggregate(SupplierID with sum as SupplierID, CategoryID with sum as CategoryID))",
                ".Select($it => new FlatteningWrapper`1() {Source = $it, GroupByContainer = new AggregationPropertyContainer() {Name = Property1, Value = Convert($it.SupplierID), Next = new LastInChain() {Name = Property0, Value = Convert($it.CategoryID), }, }, })"
                + ".GroupBy($it => new GroupByWrapper() {GroupByContainer = new LastInChain() {Name = ProductName, Value = $it.Source.ProductName, }, })"
                + ".Select($it => new AggregationWrapper() {GroupByContainer = $it.Key.GroupByContainer, Container = new AggregationPropertyContainer() {Name = CategoryID, Value = Convert(Convert($it).Sum($it => Convert($it.GroupByContainer.Next.Value))), Next = new LastInChain() {Name = SupplierID, Value = Convert(Convert($it).Sum($it => Convert($it.GroupByContainer.Value))), }, }, })");
        }

        [Fact]
        public void ClassicEFQueryShape()
        {
            var filters = VerifyQueryDeserialization(
                "aggregate(SupplierID with sum as SupplierID)",
                ".GroupBy($it => new NoGroupByWrapper())"
                + ".Select($it => new NoGroupByAggregationWrapper() {Container = new LastInChain() {Name = SupplierID, Value = $it.AsQueryable().Sum($it => $it.SupplierID), }, })",
                classicEF: true);
        }
#endif

        [Fact]
        public void ComputeClauseTest()
        {
            // $compute=cast(Prop1, 'Edm.String') as Property1AsString, tolower(Prop1) as Property1Lower
            // $compute=Price mul Qty as TotalPrice
            string clauseString = "Price mul Qty as TotalPrice";

            var computed = GetComputedProperties(_computeCustomers, clauseString);
            Assert.NotNull(computed);
            Assert.Single(computed);
        }

        [Fact]
        public void ComputeClauseTest2()
        {
            // $compute=cast(Prop1, 'Edm.String') as Property1AsString, tolower(Prop1) as Property1Lower
            // $compute=Price mul Qty as TotalPrice
            string clauseString = "cast(Name, 'Edm.String') as Property1AsString,tolower(Name) as Property1Lower,Age div 2 as HalfAge";

            var computed = GetComputedProperties(_computeCustomers, clauseString);
            Assert.NotNull(computed);
            Assert.Single(computed);
        }

        private IDictionary<string, Expression> GetComputedProperties(IQueryable source, string clauseString)
        {
            IEdmModel model = GetModel();
            ComputeClause computeClause = CreateComputeNode(clauseString, model, typeof(ComputeCustomer));

            var settings = new ODataQuerySettings { HandleNullPropagation = HandleNullPropagationOption.False };
            var computeResult = ComputeBinder.Bind(source, settings, typeof(ComputeCustomer), model, computeClause);

           // var applyExpr = queryResult.Expression;

          //  VerifyExpression<T>(applyExpr, expectedResult);

            return computeResult;
        }



        private void VerifyExpression<T>(Expression clause, string expectedExpression)
        {
            // strip off the beginning part of the expression to get to the first
            // actual query operator
            string resultExpression = ExpressionStringBuilder.ToString(clause);
            var replace = typeof(T).FullName + "[]";
            resultExpression = resultExpression.Replace(replace, string.Empty);
            Assert.True(resultExpression == expectedExpression,
                String.Format("Expected expression '{0}' but the deserializer produced '{1}'", expectedExpression, resultExpression));
        }

        private ComputeClause CreateComputeNode(string clause, IEdmModel model, Type entityType)
        {
            IEdmEntityType customerType = model.SchemaElements.OfType<IEdmEntityType>().Single(t => t.Name == entityType.Name);
            Assert.NotNull(customerType); // Guard

            IEdmEntitySet customers = model.EntityContainer.FindEntitySet("Customers");
            Assert.NotNull(customers); // Guard

            ODataQueryOptionParser parser = new ODataQueryOptionParser(model, customerType, customers,
                new Dictionary<string, string> { { "$compute", clause } });

            return parser.ParseCompute();
        }

        private IEdmModel GetModel()
        {
            ODataModelBuilder model = ODataConventionModelBuilderFactory.Create();
            model.EntitySet<ComputeCustomer>("Customers");
            return model.GetEdmModel();
        }
    }

    public class ComputeCustomer
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public int Age { get; set; }

        public int Price { get; set; }

        public int Qty { get; set; }
    }

    public class ComputeAddress
    {
        public string Street { get; set; }
    }
}
