// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNet.OData.Formatter;
using Microsoft.AspNet.OData.Query;
using Microsoft.AspNet.OData.Query.Expressions;
using Microsoft.AspNet.OData.Test.Abstraction;
using Microsoft.AspNet.OData.Test.Common;
using Microsoft.AspNet.OData.Test.Formatter.Serialization.Models;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using Xunit;

namespace Microsoft.AspNet.OData.Test.Query.Expressions
{
    public class SelectExpandBinderTest
    {
        private readonly SelectExpandBinder _binder;
        private readonly CustomersModelWithInheritance _model;
        private readonly IQueryable<Customer> _queryable;
        private readonly ODataQueryContext _context;
        private readonly ODataQuerySettings _settings;


        public SelectExpandBinderTest()
        {
            _settings = new ODataQuerySettings { HandleNullPropagation = HandleNullPropagationOption.False };
            _model = new CustomersModelWithInheritance();
            _model.Model.SetAnnotationValue<ClrTypeAnnotation>(_model.Customer, new ClrTypeAnnotation(typeof(Customer)));
            _model.Model.SetAnnotationValue<ClrTypeAnnotation>(_model.SpecialCustomer, new ClrTypeAnnotation(typeof(SpecialCustomer)));
            _context = new ODataQueryContext(_model.Model, typeof(Customer)) { RequestContainer = new MockContainer() };
            _binder = new SelectExpandBinder(_settings, new SelectExpandQueryOption("*", "", _context));

            Customer customer = new Customer();
            Order order = new Order { Customer = customer };
            customer.Orders.Add(order);

            _queryable = new[] { customer }.AsQueryable();
        }

        private static SelectExpandBinder GetBinder<T>(IEdmModel model, HandleNullPropagationOption nullPropagation = HandleNullPropagationOption.False)
        {
            var settings = new ODataQuerySettings { HandleNullPropagation = nullPropagation };

            var context = new ODataQueryContext(model, typeof(T)) { RequestContainer = new MockContainer() };

            return new SelectExpandBinder(settings, context);
        }



        [Fact]
        public void Bind_ReturnsIEdmObject_WithRightEdmType2()
        {
            string csdl = Builder.MetadataTest.GetCSDL(_model.Model);
            Console.WriteLine(csdl);
        }

        [Theory]
        [InlineData("ID")]
        [InlineData("Name")]
        [InlineData("Address")]
        public void Bind_ReturnsIEdmObject_WithRightEdmType(string select)
        {
            // Arrange
            SelectExpandQueryOption selectExpand = new SelectExpandQueryOption(select: select, expand: null, context: _context);

            // Act
            IQueryable queryable = SelectExpandBinder.Bind(_queryable, _settings, selectExpand);

            // Assert
            Assert.NotNull(queryable);
            IEdmType edmType = _model.Model.GetEdmType(queryable.GetType());
            Assert.NotNull(edmType);
            Assert.Equal(EdmTypeKind.Collection, edmType.TypeKind);
            Assert.Same(_model.Customer, edmType.AsElementType());
        }

        [Fact]
        public void Bind_GeneratedExpression_ContainsExpandedObject()
        {
            // Arrange
            SelectExpandQueryOption selectExpand = new SelectExpandQueryOption("Orders", "Orders,Orders($expand=Customer)", _context);
            IPropertyMapper mapper = new IdentityPropertyMapper();
            _model.Model.SetAnnotationValue(_model.Order, new DynamicPropertyDictionaryAnnotation(typeof(Order).GetProperty("OrderProperties")));

            // Act
            IQueryable queryable = SelectExpandBinder.Bind(_queryable, _settings, selectExpand);

            // Assert
            IEnumerator enumerator = queryable.GetEnumerator();
            Assert.True(enumerator.MoveNext());
            var partialCustomer = Assert.IsAssignableFrom<SelectExpandWrapper<Customer>>(enumerator.Current);
            Assert.False(enumerator.MoveNext());
            Assert.Null(partialCustomer.Instance);
            Assert.Equal("NS.Customer", partialCustomer.InstanceType);
            IEnumerable<SelectExpandWrapper<Order>> innerOrders = partialCustomer.Container
                .ToDictionary(mapper)["Orders"] as IEnumerable<SelectExpandWrapper<Order>>;
            Assert.NotNull(innerOrders);
            SelectExpandWrapper<Order> partialOrder = innerOrders.Single();
            Assert.Same(_queryable.First().Orders.First(), partialOrder.Instance);
            object customer = partialOrder.Container.ToDictionary(mapper)["Customer"];
            SelectExpandWrapper<Customer> innerInnerCustomer = Assert.IsAssignableFrom<SelectExpandWrapper<Customer>>(customer);
            Assert.Same(_queryable.First(), innerInnerCustomer.Instance);
        }

        //[Fact]
        //public void Bind_GeneratedExpression_CheckNullObjectWithinChainProjectionByKey()
        //{
        //    // Arrange
        //    SelectExpandQueryOption selectExpand = new SelectExpandQueryOption(null, "Orders($expand=Customer($select=City))", _context);
        //    _model.Model.SetAnnotationValue(_model.Order, new DynamicPropertyDictionaryAnnotation(typeof(Order).GetProperty("OrderProperties")));

        //    // Act
        //    IQueryable queryable = SelectExpandBinder.Bind(_queryable, _settings, selectExpand);

        //    // Assert
        //    var unaryExpression = (UnaryExpression)((MethodCallExpression)queryable.Expression).Arguments.Single(a => a is UnaryExpression);
        //    var expressionString = unaryExpression.Operand.ToString();
        //    Assert.Contains("IsNull = (Convert(Param_1.Customer.ID) == null)", expressionString);
        //}

        [Fact]
        public void ProjectAsWrapper_NonCollection_ContainsRightInstance()
        {
            // Arrange
            Order order = new Order();
            SelectExpandClause selectExpand = new SelectExpandClause(new SelectItem[0], allSelected: true);
            Expression source = Expression.Constant(order);

            // Act
            Expression projection = _binder.ProjectAsWrapper(source, selectExpand, _model.Order, _model.Orders);

            // Assert
            SelectExpandWrapper<Order> projectedOrder = Expression.Lambda(projection).Compile().DynamicInvoke() as SelectExpandWrapper<Order>;
            Assert.NotNull(projectedOrder);
            Assert.Same(order, projectedOrder.Instance);
        }

        [Fact]
        public void ProjectAsWrapper_NonCollection_ProjectedValueNullAndHandleNullPropagationTrue()
        {
            // Arrange
            _settings.HandleNullPropagation = HandleNullPropagationOption.True;
            ExpandedNavigationSelectItem expandItem = new ExpandedNavigationSelectItem(
                new ODataExpandPath(new NavigationPropertySegment(_model.Order.NavigationProperties().Single(), navigationSource: _model.Customers)),
                _model.Customers,
                selectExpandOption: null);
            SelectExpandClause selectExpand = new SelectExpandClause(new SelectItem[] { expandItem }, allSelected: true);
            Expression source = Expression.Constant(null, typeof(Order));
            _model.Model.SetAnnotationValue(_model.Order, new DynamicPropertyDictionaryAnnotation(typeof(Order).GetProperty("OrderProperties")));

            // Act
            Expression projection = _binder.ProjectAsWrapper(source, selectExpand, _model.Order, _model.Orders);

            // Assert
            SelectExpandWrapper<Order> projectedOrder = Expression.Lambda(projection).Compile().DynamicInvoke() as SelectExpandWrapper<Order>;
            Assert.NotNull(projectedOrder);
            Assert.Null(projectedOrder.Instance);
            Assert.Null(projectedOrder.Container.ToDictionary(new IdentityPropertyMapper())["Customer"]);
        }

        [Fact]
        public void ProjectAsWrapper_NonCollection_ProjectedValueNullAndHandleNullPropagationFalse_Throws()
        {
            // Arrange
            _settings.HandleNullPropagation = HandleNullPropagationOption.False;
            ExpandedNavigationSelectItem expandItem = new ExpandedNavigationSelectItem(
                new ODataExpandPath(new NavigationPropertySegment(_model.Order.NavigationProperties().Single(), navigationSource: _model.Customers)),
                _model.Customers,
                selectExpandOption: null);
            SelectExpandClause selectExpand = new SelectExpandClause(new SelectItem[] { expandItem }, allSelected: true);
            _model.Model.SetAnnotationValue(_model.Order, new DynamicPropertyDictionaryAnnotation(typeof(Order).GetProperty("OrderProperties")));
            Expression source = Expression.Constant(null, typeof(Order));

            // Act
            Expression projection = _binder.ProjectAsWrapper(source, selectExpand, _model.Order, _model.Orders);

            // Assert
            var e = ExceptionAssert.Throws<TargetInvocationException>(
                () => Expression.Lambda(projection).Compile().DynamicInvoke());
            Assert.IsType<NullReferenceException>(e.InnerException);
        }

        [Fact]
        public void ProjectAsWrapper_Collection_ContainsRightInstance()
        {
            // Arrange
            Order[] orders = new Order[] { new Order() };
            SelectExpandClause selectExpand = new SelectExpandClause(new SelectItem[0], allSelected: true);
            Expression source = Expression.Constant(orders);

            // Act
            Expression projection = _binder.ProjectAsWrapper(source, selectExpand, _model.Order, _model.Orders);

            // Assert
            IEnumerable<SelectExpandWrapper<Order>> projectedOrders = Expression.Lambda(projection).Compile().DynamicInvoke() as IEnumerable<SelectExpandWrapper<Order>>;
            Assert.NotNull(projectedOrders);
            Assert.Same(orders[0], projectedOrders.Single().Instance);
        }

        [Fact]
        public void ProjectAsWrapper_Collection_AppliesPageSize_AndOrderBy()
        {
            // Arrange
            int pageSize = 5;
            var orders = Enumerable.Range(0, 10).Select(i => new Order
            {
                ID = 10 - i,
            });
            SelectExpandClause selectExpand = new SelectExpandClause(new SelectItem[0], allSelected: true);
            Expression source = Expression.Constant(orders);
            _settings.PageSize = pageSize;

            // Act
            Expression projection = _binder.ProjectAsWrapper(source, selectExpand, _model.Order, _model.Orders);

            // Assert
            IEnumerable<SelectExpandWrapper<Order>> projectedOrders = Expression.Lambda(projection).Compile().DynamicInvoke() as IEnumerable<SelectExpandWrapper<Order>>;
            Assert.NotNull(projectedOrders);
            Assert.Equal(pageSize + 1, projectedOrders.Count());
            Assert.Equal(1, projectedOrders.First().Instance.ID);
        }

        [Fact]
        public void ProjectAsWrapper_ProjectionContainsExpandedProperties()
        {
            // Arrange
            Order order = new Order();
            ExpandedNavigationSelectItem expandItem = new ExpandedNavigationSelectItem(
                new ODataExpandPath(new NavigationPropertySegment(_model.Order.NavigationProperties().Single(), navigationSource: _model.Customers)),
                _model.Customers,
                selectExpandOption: null);
            SelectExpandClause selectExpand = new SelectExpandClause(new SelectItem[] { expandItem }, allSelected: true);
            Expression source = Expression.Constant(order);
            _model.Model.SetAnnotationValue(_model.Order, new DynamicPropertyDictionaryAnnotation(typeof(Order).GetProperty("OrderProperties")));

            // Act
            Expression projection = _binder.ProjectAsWrapper(source, selectExpand, _model.Order, _model.Orders);

            // Assert
            SelectExpandWrapper<Order> projectedOrder = Expression.Lambda(projection).Compile().DynamicInvoke() as SelectExpandWrapper<Order>;
            Assert.NotNull(projectedOrder);
            Assert.Contains("Customer", projectedOrder.Container.ToDictionary(new IdentityPropertyMapper()).Keys);
        }

        [Fact]
        public void ProjectAsWrapper_NullExpandedProperty_HasNullValueInProjectedWrapper()
        {
            // Arrange
            IPropertyMapper mapper = new IdentityPropertyMapper();
            Order order = new Order();
            ExpandedNavigationSelectItem expandItem = new ExpandedNavigationSelectItem(
                new ODataExpandPath(new NavigationPropertySegment(_model.Order.NavigationProperties().Single(), navigationSource: _model.Customers)),
                _model.Customers,
                selectExpandOption: null);
            SelectExpandClause selectExpand = new SelectExpandClause(new SelectItem[] { expandItem }, allSelected: true);
            Expression source = Expression.Constant(order);
            _model.Model.SetAnnotationValue(_model.Order, new DynamicPropertyDictionaryAnnotation(typeof(Order).GetProperty("OrderProperties")));

            // Act
            Expression projection = _binder.ProjectAsWrapper(source, selectExpand, _model.Order, _model.Orders);

            // Assert
            SelectExpandWrapper<Order> projectedOrder = Expression.Lambda(projection).Compile().DynamicInvoke() as SelectExpandWrapper<Order>;
            Assert.NotNull(projectedOrder);
            Assert.Contains("Customer", projectedOrder.Container.ToDictionary(mapper).Keys);
            Assert.Null(projectedOrder.Container.ToDictionary(mapper)["Customer"]);
        }

        [Fact]
        public void ProjectAsWrapper_Collection_ProjectedValueNullAndHandleNullPropagationTrue()
        {
            // Arrange
            _settings.HandleNullPropagation = HandleNullPropagationOption.True;
            SelectExpandClause selectExpand = new SelectExpandClause(new SelectItem[0], allSelected: true);
            Expression source = Expression.Constant(null, typeof(Order[]));

            // Act
            Expression projection = _binder.ProjectAsWrapper(source, selectExpand, _model.Order, _model.Orders);

            // Assert
            IEnumerable<SelectExpandWrapper<Order>> projectedOrders = Expression.Lambda(projection).Compile().DynamicInvoke() as IEnumerable<SelectExpandWrapper<Order>>;
            Assert.Null(projectedOrders);
        }

        [Fact]
        public void ProjectAsWrapper_Collection_ProjectedValueNullAndHandleNullPropagationFalse_Throws()
        {
            // Arrange
            _settings.HandleNullPropagation = HandleNullPropagationOption.False;
            SelectExpandClause selectExpand = new SelectExpandClause(new SelectItem[0], allSelected: true);
            Expression source = Expression.Constant(null, typeof(Order[]));

            // Act
            Expression projection = _binder.ProjectAsWrapper(source, selectExpand, _model.Order, _model.Orders);

            // Assert
            var e = ExceptionAssert.Throws<TargetInvocationException>(
                () => Expression.Lambda(projection).Compile().DynamicInvoke());
            Assert.IsType<ArgumentNullException>(e.InnerException);
        }

        [Fact]
        public void ProjectAsWrapper_Element_ProjectedValueContainsModelID()
        {
            // Arrange
            Customer customer = new Customer();
            SelectExpandClause selectExpand = new SelectExpandClause(new SelectItem[0], allSelected: true);
            Expression source = Expression.Constant(customer);

            // Act
            Expression projection = _binder.ProjectAsWrapper(source, selectExpand, _model.Customer, _model.Customers);

            // Assert
            SelectExpandWrapper<Customer> customerWrapper = Expression.Lambda(projection).Compile().DynamicInvoke() as SelectExpandWrapper<Customer>;
            Assert.NotNull(customerWrapper.ModelID);
            Assert.Same(_model.Model, ModelContainer.GetModel(customerWrapper.ModelID));
        }

        [Fact]
        public void ProjectAsWrapper_Element_ProjectedValueContainsSubKeys_IfDollarRefInDollarExpand()
        {
            // Arrange
            _model.Model.SetAnnotationValue<ClrTypeAnnotation>(_model.SpecialOrder, new ClrTypeAnnotation(typeof(SpecialOrder)));
            IPropertyMapper propertyMapper = new IdentityPropertyMapper();

            Customer customer = new Customer
            {
                Orders = new[]
                {
                    new Order { ID = 42 },
                    new SpecialOrder { ID = 38 }
                }
            };
            ODataQueryOptionParser parser = new ODataQueryOptionParser(
                _model.Model,
                _model.Customer,
                _model.Customers,
                new Dictionary<string, string> { { "$expand", "Orders/$ref" } });
            SelectExpandClause selectExpand = parser.ParseSelectAndExpand();
            Expression source = Expression.Constant(customer);

            // Act
            Expression projection = _binder.ProjectAsWrapper(source, selectExpand, _model.Customer, _model.Customers);

            // Assert
            Assert.Equal(ExpressionType.MemberInit, projection.NodeType);
            Assert.NotEmpty((projection as MemberInitExpression).Bindings.Where(p => p.Member.Name == "Instance"));
            SelectExpandWrapper<Customer> customerWrapper = Expression.Lambda(projection).Compile().DynamicInvoke() as SelectExpandWrapper<Customer>;

            var orders = customerWrapper.Container.ToDictionary(propertyMapper)["Orders"] as IEnumerable<SelectExpandWrapper<Order>>;
            Assert.NotNull(orders);
            Assert.Equal(2, orders.Count());
            Assert.Equal(42, orders.ElementAt(0).Container.ToDictionary(propertyMapper)["ID"]);
            Assert.Equal(38, orders.ElementAt(1).Container.ToDictionary(propertyMapper)["ID"]);
        }

        [Fact]
        public void ProjectAsWrapper_Element_ProjectedValueContainsSubKeys_IfDollarRefInDollarExpand_AndNestedFilterClause()
        {
            // Arrange
            _model.Model.SetAnnotationValue(_model.Order, new ClrTypeAnnotation(typeof(Order)));
            _model.Model.SetAnnotationValue(_model.SpecialOrder, new ClrTypeAnnotation(typeof(SpecialOrder)));
            IPropertyMapper propertyMapper = new IdentityPropertyMapper();

            Customer customer = new Customer
            {
                Orders = new[]
                {
                    new Order { ID = 42, City = "xyz" },
                    new SpecialOrder { ID = 38, City = "abc" }
                }
            };
            ODataQueryOptionParser parser = new ODataQueryOptionParser(
                _model.Model,
                _model.Customer,
                _model.Customers,
                new Dictionary<string, string> { { "$expand", "Orders/$ref($filter=City eq 'abc')" } });
            SelectExpandClause selectExpand = parser.ParseSelectAndExpand();
            Expression source = Expression.Constant(customer);

            // Act
            Expression projection = _binder.ProjectAsWrapper(source, selectExpand, _model.Customer, _model.Customers);

            // Assert
            Assert.Equal(ExpressionType.MemberInit, projection.NodeType);
            Assert.NotEmpty((projection as MemberInitExpression).Bindings.Where(p => p.Member.Name == "Instance"));
            SelectExpandWrapper<Customer> customerWrapper = Expression.Lambda(projection).Compile().DynamicInvoke() as SelectExpandWrapper<Customer>;

            var orders = customerWrapper.Container.ToDictionary(propertyMapper)["Orders"] as IEnumerable<SelectExpandWrapper<Order>>;
            Assert.NotNull(orders);
            var order = Assert.Single(orders); // only one
            Assert.Equal(38, order.Container.ToDictionary(propertyMapper)["ID"]);
        }

        [Fact]
        public void ProjectAsWrapper_Collection_ProjectedValueContainsSubKeys_IfDollarRefInDollarExpand()
        {
            // Arrange
            _model.Model.SetAnnotationValue<ClrTypeAnnotation>(_model.SpecialOrder, new ClrTypeAnnotation(typeof(SpecialOrder)));
            IPropertyMapper propertyMapper = new IdentityPropertyMapper();

            Customer customer1 = new Customer
            {
                Orders = new[]
                {
                    new Order { ID = 8 },
                    new SpecialOrder { ID = 9 }
                }
            };
            Customer customer2 = new Customer
            {
                Orders = new[]
                {
                    new Order { ID = 18 },
                    new SpecialOrder { ID = 19 }
                }
            };

            ODataQueryOptionParser parser = new ODataQueryOptionParser(
                _model.Model,
                _model.Customer,
                _model.Customers,
                new Dictionary<string, string> { { "$expand", "Orders/$ref" } });
            SelectExpandClause selectExpand = parser.ParseSelectAndExpand();
            Expression source = Expression.Constant(new[] { customer1, customer2 });

            // Act
            Expression projection = _binder.ProjectAsWrapper(source, selectExpand, _model.Customer, _model.Customers);

            // Assert
            Assert.Equal(ExpressionType.Call, projection.NodeType);
            var customerWrappers = Expression.Lambda(projection).Compile().DynamicInvoke() as IEnumerable<SelectExpandWrapper<Customer>>;
            Assert.Equal(2, customerWrappers.Count());

            var orders = customerWrappers.ElementAt(0).Container.ToDictionary(propertyMapper)["Orders"] as IEnumerable<SelectExpandWrapper<Order>>;
            Assert.NotNull(orders);
            Assert.Equal(2, orders.Count());
            Assert.Equal(8, orders.ElementAt(0).Container.ToDictionary(propertyMapper)["ID"]);
            Assert.Equal(9, orders.ElementAt(1).Container.ToDictionary(propertyMapper)["ID"]);

            orders = customerWrappers.ElementAt(1).Container.ToDictionary(propertyMapper)["Orders"] as IEnumerable<SelectExpandWrapper<Order>>;
            Assert.NotNull(orders);
            Assert.Equal(2, orders.Count());
            Assert.Equal(18, orders.ElementAt(0).Container.ToDictionary(propertyMapper)["ID"]);
            Assert.Equal(19, orders.ElementAt(1).Container.ToDictionary(propertyMapper)["ID"]);
        }

        [Fact]
        public void ProjectAsWrapper_Collection_ProjectedValueContainsSubKeys_IfDollarRefInDollarExpand_AndNestedTopAndSkip()
        {
            // Arrange
            _model.Model.SetAnnotationValue<ClrTypeAnnotation>(_model.SpecialOrder, new ClrTypeAnnotation(typeof(SpecialOrder)));
            IPropertyMapper propertyMapper = new IdentityPropertyMapper();

            Customer customer1 = new Customer
            {
                Orders = new[]
                {
                    new Order { ID = 8 },
                    new SpecialOrder { ID = 9 }
                }
            };
            Customer customer2 = new Customer
            {
                Orders = new[]
                {
                    new Order { ID = 18 },
                    new SpecialOrder { ID = 19 }
                }
            };

            ODataQueryOptionParser parser = new ODataQueryOptionParser(
                _model.Model,
                _model.Customer,
                _model.Customers,
                new Dictionary<string, string> { { "$expand", "Orders/$ref($top=1;$skip=1)" } });
            SelectExpandClause selectExpand = parser.ParseSelectAndExpand();
            Expression source = Expression.Constant(new[] { customer1, customer2 });

            // Act
            Expression projection = _binder.ProjectAsWrapper(source, selectExpand, _model.Customer, _model.Customers);

            // Assert
            Assert.Equal(ExpressionType.Call, projection.NodeType);
            var customerWrappers = Expression.Lambda(projection).Compile().DynamicInvoke() as IEnumerable<SelectExpandWrapper<Customer>>;
            Assert.Equal(2, customerWrappers.Count());

            var orders = customerWrappers.ElementAt(0).Container.ToDictionary(propertyMapper)["Orders"] as IEnumerable<SelectExpandWrapper<Order>>;
            Assert.NotNull(orders);
            var order = Assert.Single(orders); // only one
            Assert.Equal(9, order.Container.ToDictionary(propertyMapper)["ID"]);

            orders = customerWrappers.ElementAt(1).Container.ToDictionary(propertyMapper)["Orders"] as IEnumerable<SelectExpandWrapper<Order>>;
            Assert.NotNull(orders);
            order = Assert.Single(orders);
            Assert.Equal(19, order.Container.ToDictionary(propertyMapper)["ID"]);
        }

        [Theory]
        [InlineData("*")]
        [InlineData("ID,*")]
        [InlineData("")]
        public void ProjectAsWrapper_Element_ProjectedValueContainsInstance_IfSelectionIsAll(string select)
        {
            // Arrange
            Customer customer = new Customer();
            ODataQueryOptionParser parser = new ODataQueryOptionParser(
                _model.Model,
                _model.Customer,
                _model.Customers,
                new Dictionary<string, string> { { "$select", select }, { "$expand", "Orders" } });
            SelectExpandClause selectExpand = parser.ParseSelectAndExpand();
            Expression source = Expression.Constant(customer);

            // Act
            Expression projection = _binder.ProjectAsWrapper(source, selectExpand, _model.Customer, _model.Customers);

            // Assert
            Assert.Equal(ExpressionType.MemberInit, projection.NodeType);
            Assert.NotEmpty((projection as MemberInitExpression).Bindings.Where(p => p.Member.Name == "Instance"));
            SelectExpandWrapper<Customer> customerWrapper = Expression.Lambda(projection).Compile().DynamicInvoke() as SelectExpandWrapper<Customer>;
            Assert.Same(customer, customerWrapper.Instance);
        }

        [Fact]
        public void ProjectAsWrapper_Element_ProjectedValueDoesNotContainInstance_IfSelectionIsPartial()
        {
            // Arrange
            Customer customer = new Customer();
            ODataQueryOptionParser parser = new ODataQueryOptionParser(_model.Model, _model.Customer, _model.Customers,
                new Dictionary<string, string> { { "$select", "ID,Orders" }, { "$expand", "Orders" } });
            SelectExpandClause selectExpand = parser.ParseSelectAndExpand();
            Expression source = Expression.Constant(customer);

            // Act
            Expression projection = _binder.ProjectAsWrapper(source, selectExpand, _model.Customer, _model.Customers);

            // Assert
            Assert.Equal(ExpressionType.MemberInit, projection.NodeType);
            Assert.Empty((projection as MemberInitExpression).Bindings.Where(p => p.Member.Name == "Instance"));
            Assert.NotEmpty((projection as MemberInitExpression).Bindings.Where(p => p.Member.Name == "InstanceType"));
            SelectExpandWrapper<Customer> customerWrapper = Expression.Lambda(projection).Compile().DynamicInvoke() as SelectExpandWrapper<Customer>;
            Assert.Null(customerWrapper.Instance);
            Assert.Equal("NS.Customer", customerWrapper.InstanceType);
        }

        [Fact]
        public void ProjectAsWrapper_Element_ProjectedValueContains_SelectedStructuralProperties()
        {
            // Arrange
            Customer customer = new Customer { Name = "OData" };
            ODataQueryOptionParser parser = new ODataQueryOptionParser(_model.Model, _model.Customer, _model.Customers,
                new Dictionary<string, string> { { "$select", "Name,Orders" }, { "$expand", "Orders" } });
            SelectExpandClause selectExpand = parser.ParseSelectAndExpand();
            Expression source = Expression.Constant(customer);

            // Act
            Expression projection = _binder.ProjectAsWrapper(source, selectExpand, _model.Customer, _model.Customers);

            // Assert
            SelectExpandWrapper<Customer> customerWrapper = Expression.Lambda(projection).Compile().DynamicInvoke() as SelectExpandWrapper<Customer>;
            Assert.Equal(customer.Name, customerWrapper.Container.ToDictionary(new IdentityPropertyMapper())["Name"]);
        }

        [Theory]
        [InlineData("Name")]
        [InlineData("NS.upgrade")]
        public void ProjectAsWrapper_Element_ProjectedValueContains_KeyPropertiesEvenIfNotPresentInSelectClause(string select)
        {
            // Arrange
            Customer customer = new Customer { ID = 42, FirstName = "OData" };
            ODataQueryOptionParser parser = new ODataQueryOptionParser(_model.Model, _model.Customer, _model.Customers,
                new Dictionary<string, string> { { "$select", select } });

            SelectExpandClause selectExpand = parser.ParseSelectAndExpand();
            Expression source = Expression.Constant(customer);

            // Act
            Expression projection = _binder.ProjectAsWrapper(source, selectExpand, _model.Customer, _model.Customers);

            // Assert
            SelectExpandWrapper<Customer> customerWrapper = Expression.Lambda(projection).Compile().DynamicInvoke() as SelectExpandWrapper<Customer>;
            Assert.Equal(customer.ID, customerWrapper.Container.ToDictionary(new IdentityPropertyMapper())["ID"]);
        }

        [Theory]
        [InlineData("Name")]
        [InlineData("NS.upgrade")]
        public void ProjectAsWrapper_ProjectedValueContainsConcurrencyProperties_EvenIfNotPresentInSelectClause(string select)
        {
            // Arrange
            Customer customer = new Customer { ID = 42, City = "any" };

            SelectExpandClause selectExpand = ParseSelectExpand(select, null, _model.Model, _model.Customer, _model.Customers);
            Expression source = Expression.Constant(customer);

            // Act
            Expression projection = _binder.ProjectAsWrapper(source, selectExpand, _model.Customer, _model.Customers);

            // Assert
            SelectExpandWrapper<Customer> customerWrapper = Expression.Lambda(projection).Compile().DynamicInvoke() as SelectExpandWrapper<Customer>;
            Assert.Equal(customer.City, customerWrapper.Container.ToDictionary(new IdentityPropertyMapper())["City"]);
        }

        [Theory]
     //   [InlineData("Name")]
        [InlineData("HomeAddress/Street")]
        public void ProjectAsWrapper_ProjectedValueContainsConcurrencyProperties_EvenIfNotPresentInSelectClause1(string select)
        {
            // Arrange
            QueryCustomer aCustomer = new QueryCustomer
            {
                Id = 42,
                Name = "Peter",
                HomeAddress = new QueryAddress
                {
                    Street = "MyStreet",
                    AddressETag = 76.5
                },
                CustomerETag = 1.14926
            };

            IEdmModel model = QueryModel;
            IEdmEntityType customer = model.SchemaElements.OfType<IEdmEntityType>().FirstOrDefault(c => c.Name == "QueryCustomer");
            Assert.NotNull(customer); // Guard

            IEdmEntitySet customers = model.EntityContainer.FindEntitySet("Customers");
            Assert.NotNull(customers);

            SelectExpandClause selectExpandClause = ParseSelectExpand(select, null, model, customer, customers);
            Assert.NotNull(selectExpandClause);

            Expression source = Expression.Constant(aCustomer);
            SelectExpandBinder binder = GetBinder<QueryCustomer>(model);

            // Act
            Expression projection = binder.ProjectAsWrapper(source, selectExpandClause, customer, customers);

            // Assert
            var mapper = new IdentityPropertyMapper();
            SelectExpandWrapper<QueryCustomer> customerWrapper = Expression.Lambda(projection).Compile().DynamicInvoke() as SelectExpandWrapper<QueryCustomer>;
            var customerSelectedProperties = customerWrapper.Container.ToDictionary(mapper);
            Assert.Equal(3, customerSelectedProperties.Count);
            Assert.Equal(42, customerSelectedProperties["Id"]);
            Assert.Equal(1.14926, customerSelectedProperties["CustomerETag"]);

            SelectExpandWrapper<QueryAddress> addressWrapper = customerSelectedProperties["HomeAddress"] as SelectExpandWrapper<QueryAddress>;
            var addressSelectedProperties = addressWrapper.Container.ToDictionary(mapper);
            Assert.Single(addressSelectedProperties);
            Assert.Equal("MyStreet", addressSelectedProperties["Street"]);

            //Assert.Equal(customer.City, customerWrapper.Container.ToDictionary(new IdentityPropertyMapper())["City"]);
        }

        [Fact]
        public void ProjectAsWrapper_Element_ProjectedValueContains_SelectedStructuralProperties2()
        {
            // Arrange
            QueryCustomer aCustomer = new QueryCustomer
            {
                Name = "Peter",
                HomeAddress = new QueryAddress
                {
                    Street = "MyStreet",
                    AddressETag = 76.5
                }
            };

            IEdmModel model = QueryModel;
            IEdmEntityType customer = model.SchemaElements.OfType<IEdmEntityType>().FirstOrDefault(c => c.Name == "QueryCustomer");
            Assert.NotNull(customer); // Guard

            IEdmEntitySet customers = model.EntityContainer.FindEntitySet("Customers");
            Assert.NotNull(customers);

            string select = "Name";
            SelectExpandClause selectExpandClause = ParseSelectExpand(select, null, model, customer, customers);
            Assert.NotNull(selectExpandClause);

            Expression source = Expression.Constant(aCustomer);

            SelectExpandBinder binder = GetBinder<QueryCustomer>(model);

            // Act
            Expression projection = binder.ProjectAsWrapper(source, selectExpandClause, customer, customers);

            // Assert
            SelectExpandWrapper<QueryCustomer> customerWrapper = Expression.Lambda(projection).Compile().DynamicInvoke() as SelectExpandWrapper<QueryCustomer>;
            var container = customerWrapper.Container.ToDictionary(new IdentityPropertyMapper());
            Assert.Equal(aCustomer.Name, container["Name"]);
        }

        #region GetSelectExpandProperties Tests
        [Theory]
        [InlineData("HomeAddress")] // $select=property
        [InlineData("Addresses")]
        [InlineData("Emails")]
        [InlineData("Name")]
        [InlineData("Microsoft.AspNet.OData.Test.Query.Expressions.QueryVipCustomer/Level")] // $select=typeCast/Property
        [InlineData("Microsoft.AspNet.OData.Test.Query.Expressions.QueryVipCustomer/Birthday")]
        [InlineData("Microsoft.AspNet.OData.Test.Query.Expressions.QueryVipCustomer/Taxes")]
        [InlineData("Microsoft.AspNet.OData.Test.Query.Expressions.QueryVipCustomer/VipAddress")]
        [InlineData("Microsoft.AspNet.OData.Test.Query.Expressions.QueryVipCustomer/VipAddresses")]
        public void GetSelectExpandProperties_ForDirectProperty_OutputCorrectProperties(string select)
        {
            // Arrange & Act
            SelectExpandClause selectExpandClause;
            IDictionary<IEdmStructuralProperty, PathSelectItem> propertiesToInclude;
            IDictionary<IEdmNavigationProperty, ExpandedReferenceSelectItem> propertiesToExpand;
            ISet<IEdmStructuralProperty> autoSelectedProperties;
            bool isContainDynamicProperty = GetSelectExpandPropertiesHelper(select, null, out selectExpandClause,
                out propertiesToInclude,
                out propertiesToExpand,
                out autoSelectedProperties);

            // Assert
            Assert.False(selectExpandClause.AllSelected); // guard
            SelectItem selectItem = selectExpandClause.SelectedItems.First();
            PathSelectItem pathSelectItem = Assert.IsType<PathSelectItem>(selectItem); // Guard

            Assert.False(isContainDynamicProperty);
            Assert.Null(propertiesToExpand); // No navigation property to expand

            Assert.NotNull(autoSelectedProperties); // auto select the keys
            Assert.Equal("Id", Assert.Single(autoSelectedProperties).Name);

            Assert.NotNull(propertiesToInclude); // has one structural property to select
            var propertyToInclude = Assert.Single(propertiesToInclude);

            string[] segments = select.Split('/');
            if (segments.Length == 2)
            {
                Assert.Equal(segments[1], propertyToInclude.Key.Name);
            }
            else
            {
                Assert.Equal(segments[0], propertyToInclude.Key.Name);
            }

            Assert.NotNull(propertyToInclude.Value);
            Assert.Same(pathSelectItem, propertyToInclude.Value);
        }

        [Theory]
        [InlineData("HomeAddress,HomeAddress/Codes")]
        [InlineData("HomeAddress,HomeAddress/Codes($top=2)")]
        public void GetSelectExpandProperties_ForSelectAllAndSelectSpecialy_OutputCorrectProperties(string select)
        {
            // Arrange & Act
            SelectExpandClause selectExpandClause;
            IDictionary<IEdmStructuralProperty, PathSelectItem> propertiesToInclude;
            IDictionary<IEdmNavigationProperty, ExpandedReferenceSelectItem> propertiesToExpand;
            ISet<IEdmStructuralProperty> autoSelectedProperties;
            bool isContainDynamicProperty = GetSelectExpandPropertiesHelper(select, null, out selectExpandClause,
                out propertiesToInclude,
                out propertiesToExpand,
                out autoSelectedProperties);

            // Assert
            Assert.False(isContainDynamicProperty);
            Assert.Null(propertiesToExpand);

            Assert.NotNull(autoSelectedProperties);
            Assert.Equal("Id", Assert.Single(autoSelectedProperties).Name);

            Assert.NotNull(propertiesToInclude);
            var propertyToInclude = Assert.Single(propertiesToInclude);
            Assert.Equal("HomeAddress", propertyToInclude.Key.Name);

            Assert.NotNull(propertyToInclude.Value.SelectAndExpand);
            Assert.True(propertyToInclude.Value.SelectAndExpand.AllSelected);
            var selectItem = Assert.Single(propertyToInclude.Value.SelectAndExpand.SelectedItems);
            Assert.IsType<PathSelectItem>(selectItem);
        }

        [Theory]
        [InlineData("HomeAddress/Street,HomeAddress/Region,HomeAddress/Codes")]
        [InlineData("HomeAddress($select=Street,Region,Codes)")]
        public void GetSelectExpandProperties_ForMultipleSubPropertiesSelection_OutputCorrectProperties(string select)
        {
            // Arrange & Act
            SelectExpandClause selectExpandClause;
            IDictionary<IEdmStructuralProperty, PathSelectItem> propertiesToInclude;
            IDictionary<IEdmNavigationProperty, ExpandedReferenceSelectItem> propertiesToExpand;
            ISet<IEdmStructuralProperty> autoSelectedProperties;
            bool isContainDynamicProperty = GetSelectExpandPropertiesHelper(select, null, out selectExpandClause,
                out propertiesToInclude,
                out propertiesToExpand,
                out autoSelectedProperties);

            // Assert
            Assert.False(isContainDynamicProperty);
            Assert.Null(propertiesToExpand); // No navigation property to expand

            Assert.NotNull(autoSelectedProperties); // auto select the keys
            Assert.Equal("Id", Assert.Single(autoSelectedProperties).Name);

            Assert.NotNull(propertiesToInclude); // has one structural property to select
            var propertyToInclude = Assert.Single(propertiesToInclude);
            Assert.Equal("HomeAddress", propertyToInclude.Key.Name);

            Assert.NotNull(propertyToInclude.Value);

            Assert.NotNull(propertyToInclude.Value.SelectAndExpand); // Sub select & expand
            Assert.False(propertyToInclude.Value.SelectAndExpand.AllSelected);

            Assert.Equal(3, propertyToInclude.Value.SelectAndExpand.SelectedItems.Count()); // Street, Region, Codes

            Assert.Equal(new [] { "Street", "Region", "Codes"},
                propertyToInclude.Value.SelectAndExpand.SelectedItems.Select(s =>
            {
                PathSelectItem subSelectItem = (PathSelectItem)s;
                PropertySegment propertySegment = subSelectItem.SelectedPath.Single() as PropertySegment;
                Assert.NotNull(propertySegment);
                return propertySegment.Property.Name;
            }));
        }

        [Theory]
        [InlineData("CustomerDynamicProperty1", true)]
        [InlineData("CustomerDynamicProperty2", true)]
        [InlineData("HomeAddress/AddressDynPriperty", false)]
        public void GetSelectExpandProperties_ForDynamicProperty_OutputCorrectBoolean(string select, bool expect)
        {
            // Arrange & Act
            SelectExpandClause selectExpandClause;
            bool isContainDynamicProperty = GetSelectExpandPropertiesHelper(select, null, out selectExpandClause, out _, out _, out _);

            // Assert
            Assert.False(selectExpandClause.AllSelected); // guard
            Assert.Equal(expect, isContainDynamicProperty);
        }

        [Theory]
        [InlineData("Orders")]
        [InlineData("PrivateOrder")]
        [InlineData("Microsoft.AspNet.OData.Test.Query.Expressions.QueryVipCustomer/SpecialOrder")]
        [InlineData("Microsoft.AspNet.OData.Test.Query.Expressions.QueryVipCustomer/SpecialOrders")]
        public void GetSelectExpandProperties_SkipForNavigationSelection(string select)
        {
            // Arrange & Act
            SelectExpandClause selectExpandClause;
            IDictionary<IEdmStructuralProperty, PathSelectItem> propertiesToInclude;
            IDictionary<IEdmNavigationProperty, ExpandedReferenceSelectItem> propertiesToExpand;
            ISet<IEdmStructuralProperty> autoSelectedProperties;
            bool isContainDynamicProperty = GetSelectExpandPropertiesHelper(select, null, out selectExpandClause,
                out propertiesToInclude,
                out propertiesToExpand,
                out autoSelectedProperties);

            // Assert
            Assert.False(isContainDynamicProperty);
            Assert.Null(propertiesToInclude);
            Assert.Null(propertiesToExpand);

            Assert.NotNull(autoSelectedProperties);
        }

        [Theory]
        [InlineData("PrivateOrder")]
        [InlineData("PrivateOrder/$ref")]
        [InlineData("Orders")]
        [InlineData("Orders/$ref")]
        [InlineData("Orders($top=2;$count=true)")]
        [InlineData("Microsoft.AspNet.OData.Test.Query.Expressions.QueryVipCustomer/SpecialOrder")]
        [InlineData("Microsoft.AspNet.OData.Test.Query.Expressions.QueryVipCustomer/SpecialOrder/$ref")]
        [InlineData("Microsoft.AspNet.OData.Test.Query.Expressions.QueryVipCustomer/SpecialOrders")]
        [InlineData("Microsoft.AspNet.OData.Test.Query.Expressions.QueryVipCustomer/SpecialOrders/$ref")]
        [InlineData("Microsoft.AspNet.OData.Test.Query.Expressions.QueryVipCustomer/SpecialOrders($search=abc)")]
        public void GetSelectExpandProperties_ForDirectNavigationProperty_ReturnsProperties(string expand)
        {
            // Arrange & Act
            SelectExpandClause selectExpandClause;
            IDictionary<IEdmStructuralProperty, PathSelectItem> propertiesToInclude;
            IDictionary<IEdmNavigationProperty, ExpandedReferenceSelectItem> propertiesToExpand;
            ISet<IEdmStructuralProperty> autoSelectedProperties;
            bool isContainDynamicProperty = GetSelectExpandPropertiesHelper(null, expand, out selectExpandClause,
                out propertiesToInclude,
                out propertiesToExpand,
                out autoSelectedProperties);

            // Assert
            var selectItem = Assert.Single(selectExpandClause.SelectedItems);
            ExpandedReferenceSelectItem expandedItem = selectItem as ExpandedReferenceSelectItem;
            Assert.NotNull(expandedItem);
            var navigationSegment = expandedItem.PathToNavigationProperty.First(p => p is NavigationPropertySegment) as NavigationPropertySegment;

            Assert.False(isContainDynamicProperty); // not container dynamic properties selection
            Assert.Null(propertiesToInclude); // no structural properties to include
            Assert.Null(autoSelectedProperties); // no auto select properties

            Assert.NotNull(propertiesToExpand);
            var propertyToExpand = Assert.Single(propertiesToExpand);

            Assert.Same(navigationSegment.NavigationProperty, propertyToExpand.Key);
            Assert.Same(expandedItem, propertyToExpand.Value);
        }

        [Theory]
        [InlineData("HomeAddress/RelatedCity")]
        [InlineData("HomeAddress/RelatedCity/$ref")]
        [InlineData("HomeAddress/Cities")]
        [InlineData("HomeAddress/Cities($top=2)")]
        [InlineData("HomeAddress/Cities/$ref")]
        [InlineData("Addresses/RelatedCity")]
        [InlineData("Addresses/RelatedCity/$ref")]
        [InlineData("Addresses/Cities")]
        [InlineData("Addresses/Cities($count=true)")]
        [InlineData("Addresses/Cities/$ref")]
        [InlineData("HomeAddress/Info/InfoCity")]
        [InlineData("Addresses/Info/InfoCity")]
        [InlineData("HomeAddress/Microsoft.AspNet.OData.Test.Query.Expressions.QueryUsAddress/UsCity")]
        [InlineData("HomeAddress/Microsoft.AspNet.OData.Test.Query.Expressions.QueryUsAddress/UsCities")]
        [InlineData("HomeAddress/Microsoft.AspNet.OData.Test.Query.Expressions.QueryUsAddress/UsCities($select=CityName)")]
        [InlineData("HomeAddress/Microsoft.AspNet.OData.Test.Query.Expressions.QueryUsAddress/UsCities/$ref")]
        [InlineData("Addresses/Microsoft.AspNet.OData.Test.Query.Expressions.QueryCnAddress/CnCity")]
        [InlineData("Addresses/Microsoft.AspNet.OData.Test.Query.Expressions.QueryCnAddress/CnCities")]
        [InlineData("Microsoft.AspNet.OData.Test.Query.Expressions.QueryVipCustomer/VipAddress/Microsoft.AspNet.OData.Test.Query.Expressions.QueryCnAddress/CnCities")]
        [InlineData("Microsoft.AspNet.OData.Test.Query.Expressions.QueryVipCustomer/VipAddresses/Microsoft.AspNet.OData.Test.Query.Expressions.QueryUsAddress/UsCities")]
        public void GetSelectExpandProperties_ForNonDirectNavigationProperty_ReturnsCorrectExpandedProperties(string expand)
        {
            // Arrange & Act
            SelectExpandClause selectExpandClause;
            IDictionary<IEdmStructuralProperty, PathSelectItem> propertiesToInclude;
            IDictionary<IEdmNavigationProperty, ExpandedReferenceSelectItem> propertiesToExpand;
            ISet<IEdmStructuralProperty> autoSelectedProperties;
            bool isContainDynamicProperty = GetSelectExpandPropertiesHelper(null, expand, out selectExpandClause,
                out propertiesToInclude,
                out propertiesToExpand,
                out autoSelectedProperties);

            // Assert
            var selectItem = Assert.Single(selectExpandClause.SelectedItems);
            ExpandedReferenceSelectItem expandedItem = selectItem as ExpandedReferenceSelectItem;
            Assert.NotNull(expandedItem);
            var propertySegment = expandedItem.PathToNavigationProperty.First(p => p is PropertySegment) as PropertySegment;

            Assert.Null(propertiesToExpand); // nothing to expand at current level

            Assert.NotEmpty(propertiesToInclude);
            var propertyToInclude = Assert.Single(propertiesToInclude);

            Assert.Same(propertySegment.Property, propertyToInclude.Key);
            Assert.NotNull(propertyToInclude.Value);

            PathSelectItem pathItem = propertyToInclude.Value;
            Assert.NotNull(pathItem);
            Assert.NotNull(pathItem.SelectAndExpand);
            Assert.True(pathItem.SelectAndExpand.AllSelected);
            var nextLevelSelectItem = Assert.Single(pathItem.SelectAndExpand.SelectedItems);
            var nextLevelExpandedItem = nextLevelSelectItem as ExpandedReferenceSelectItem;
            Assert.NotNull(nextLevelExpandedItem);
        }

        [Fact]
        public void GetSelectExpandProperties_FoSelectAndExpand_ReturnsCorrectExpandedProperties()
        {
            // Arrange
            string select = "HomeAddress($select=Street),Addresses/Codes($top=2)";
            string expand = "HomeAddress/RelatedCity/$ref,HomeAddress/Cities($count=true),PrivateOrder";

            // Act
            SelectExpandClause selectExpandClause;
            IDictionary<IEdmStructuralProperty, PathSelectItem> propertiesToInclude;
            IDictionary<IEdmNavigationProperty, ExpandedReferenceSelectItem> propertiesToExpand;
            ISet<IEdmStructuralProperty> autoSelectedProperties;
            bool isContainDynamicProperty = GetSelectExpandPropertiesHelper(select, expand, out selectExpandClause,
                out propertiesToInclude,
                out propertiesToExpand,
                out autoSelectedProperties);

            // Arrange
            Assert.False(isContainDynamicProperty);

            Assert.NotNull(selectExpandClause);
            Assert.False(selectExpandClause.AllSelected);

            // Why it's 6, because ODL includes "HomeAddress" as Selected automatic when parsing $expand=HomeAddress/Nav
            // It's an issue reported at: https://github.com/OData/odata.net/issues/1574
            Assert.Equal(6, selectExpandClause.SelectedItems.Count());

            Assert.NotNull(propertiesToInclude);
            Assert.Equal(2, propertiesToInclude.Count);
            Assert.Equal(new[] { "HomeAddress", "Addresses" }, propertiesToInclude.Keys.Select(e => e.Name));

            Assert.NotNull(propertiesToExpand);
            var propertyToExpand = Assert.Single(propertiesToExpand);
            Assert.Equal("PrivateOrder", propertyToExpand.Key.Name);

            Assert.NotNull(autoSelectedProperties);
            Assert.Equal("Id", Assert.Single(autoSelectedProperties).Name);
        }

        private static bool GetSelectExpandPropertiesHelper(string select, string expand,
            out SelectExpandClause selectExpandClause,
            out IDictionary<IEdmStructuralProperty, PathSelectItem> propertiesToInclude,
            out IDictionary<IEdmNavigationProperty, ExpandedReferenceSelectItem> propertiesToExpand,
            out ISet<IEdmStructuralProperty> autoSelectedProperties)
        {
            IEdmModel model = QueryModel;

            IEdmEntityType customer = model.SchemaElements.OfType<IEdmEntityType>().FirstOrDefault(c => c.Name == "QueryCustomer");
            Assert.NotNull(customer); // Guard

            IEdmEntitySet customers = model.EntityContainer.FindEntitySet("Customers");
            Assert.NotNull(customers);

            selectExpandClause = ParseSelectExpand(select, expand, model, customer, customers);
            Assert.NotNull(selectExpandClause);

            return SelectExpandBinder.GetSelectExpandProperties(model, customer, customers, selectExpandClause,
                out propertiesToInclude,
                out propertiesToExpand,
                out autoSelectedProperties);
        }
        #endregion

        #region CreatePropertyNameExpression Tests
        [Fact]
        public void CreatePropertyNameExpression_ReturnsCorrectExpression()
        {
            // Arrange
            IEdmModel model = QueryModel;

            // Retrieve base info
            IEdmEntityType customer = model.SchemaElements.OfType<IEdmEntityType>().FirstOrDefault(c => c.Name == "QueryCustomer");
            Assert.NotNull(customer); // Guard
            IEdmProperty baseProperty = customer.FindProperty("PrivateOrder");
            Assert.NotNull(baseProperty); // Guard

            // Retrieve derived info
            IEdmEntityType vipCustomer = model.SchemaElements.OfType<IEdmEntityType>().FirstOrDefault(c => c.Name == "QueryVipCustomer");
            Assert.NotNull(vipCustomer); // Guard
            IEdmProperty derivedProperty = vipCustomer.FindProperty("Birthday");
            Assert.NotNull(derivedProperty); // Guard

            Expression source = Expression.Parameter(typeof(QueryCustomer), "aCustomer");
            SelectExpandBinder binder = GetBinder<QueryCustomer>(model);

            // Act & Assert
            // #1. Base property on base type
            Expression property = binder.CreatePropertyNameExpression(customer, baseProperty, source);
            Assert.Equal(ExpressionType.Constant, property.NodeType);
            Assert.Equal(typeof(string), property.Type);
            Assert.Equal("PrivateOrder", (property as ConstantExpression).Value);

            // #2. Base property on derived type
            property = binder.CreatePropertyNameExpression(vipCustomer, baseProperty, source);
            Assert.Equal(ExpressionType.Constant, property.NodeType);
            Assert.Equal(typeof(string), property.Type);
            Assert.Equal("PrivateOrder", (property as ConstantExpression).Value);

            // #3. Derived property on base type
            property = binder.CreatePropertyNameExpression(customer, derivedProperty, source);
            Assert.Equal(ExpressionType.Conditional, property.NodeType);
            Assert.Equal(typeof(string), property.Type);
            Assert.Equal(String.Format("IIF((aCustomer Is QueryVipCustomer), \"Birthday\", null)", customer.FullName()), property.ToString());

            // #4. Derived property on derived type.
            property = binder.CreatePropertyNameExpression(vipCustomer, derivedProperty, source);
            Assert.Equal(ExpressionType.Constant, property.NodeType);
            Assert.Equal(typeof(string), property.Type);
            Assert.Equal("Birthday", (property as ConstantExpression).Value);
        }

        [Fact]
        public void CreatePropertyNameExpression_ReturnsConstantExpression_IfPropertyTypeCannotAssignedToElementType()
        {
            // Arrange
            IEdmModel model = QueryModel;
            IEdmEntityType customer = model.SchemaElements.OfType<IEdmEntityType>().FirstOrDefault(c => c.Name == "QueryCustomer");
            Assert.NotNull(customer); // Guard

            IEdmEntityType order = model.SchemaElements.OfType<IEdmEntityType>().FirstOrDefault(c => c.Name == "QueryOrder");
            Assert.NotNull(order); // Guard

            Assert.False(order.IsOrInheritsFrom(customer)); // make sure order has no inheritance-ship with customer.

            IEdmProperty edmProperty = order.FindProperty("Title");
            Assert.NotNull(edmProperty);

            Expression source = Expression.Parameter(typeof(QueryCustomer), "aCustomer");
            SelectExpandBinder binder = GetBinder<QueryCustomer>(model);

            // Act
            Expression property = binder.CreatePropertyNameExpression(customer, edmProperty, source);

            // Assert
            Assert.Equal(ExpressionType.Constant, property.NodeType);
            Assert.Equal(typeof(string), property.Type);
            Assert.Equal("Title", (property as ConstantExpression).Value);
        }

        [Fact]
        public void CreatePropertyNameExpression_ThrowsODataException_IfMappingTypeIsNotFoundInModel()
        {
            // Arrange
            EdmModel model = GetEdmModel() as EdmModel;
            IEdmEntityType customer = model.SchemaElements.OfType<IEdmEntityType>().FirstOrDefault(c => c.Name == "QueryCustomer");
            Assert.NotNull(customer); // Guard

            // Create a "SubCustomer" derived from "Customer", but without the CLR type in the Edm model.
            EdmEntityType subCustomer = new EdmEntityType("NS", "SubCustomer", customer);
            EdmStructuralProperty subNameProperty = subCustomer.AddStructuralProperty("SubName", EdmPrimitiveTypeKind.String);
            model.AddElement(subCustomer);

            Expression source = Expression.Constant(new Customer());
            SelectExpandBinder binder = GetBinder<QueryCustomer>(model);

            // Act & Assert
            ExceptionAssert.Throws<ODataException>(() => binder.CreatePropertyNameExpression(customer, subNameProperty, source),
                "The provided mapping does not contain a resource for the resource type 'NS.SubCustomer'.");
        }
        #endregion



        /*
                [Fact]
                public void CreatePropertyValueExpression_NonDerivedProperty_ReturnsMemberAccessExpression()
                {
                    Expression customer = Expression.Constant(new Customer());
                    IEdmNavigationProperty ordersProperty = _model.Customer.NavigationProperties().Single();

                    Expression property = _binder.CreatePropertyValueExpression(_model.Customer, ordersProperty, customer);

                    Assert.Equal(ExpressionType.MemberAccess, property.NodeType);
                    Assert.Equal(typeof(Customer).GetProperty("Orders"), (property as MemberExpression).Member);
                }

                [Fact]
                public void CreateNamedPropertyExpression_NonDerivedProperty_ReturnsMemberAccessExpression()
                {
                    Expression customer = Expression.Constant(new Customer());
                    IEdmStructuralProperty accountProperty = _model.Customer.StructuralProperties().Single(c => c.Name == "Account");

                    ODataSelectPath selectPath = new ODataSelectPath(new PropertySegment(accountProperty));
                    PathSelectItem pathSelectItem = new PathSelectItem(selectPath);

                    NamedPropertyExpression namedProperty = _binder.CreatePropertyNameExpression(customer, _model.Customer, pathSelectItem);

                    //Assert.Equal(ExpressionType.MemberAccess, property.NodeType);
                    //Assert.Equal(typeof(Customer).GetProperty("Orders"), (property as MemberExpression).Member);
                }*/

        //[Fact]
        //public void CreatePropertyValueExpression_ThrowsODataException_IfMappingTypeIsNotFoundInModel()
        //{
        //    // Arrange
        //    _model.Model.SetAnnotationValue<ClrTypeAnnotation>(_model.SpecialCustomer, null);
        //    Expression customer = Expression.Constant(new Customer());
        //    IEdmNavigationProperty specialOrdersProperty = _model.SpecialCustomer.DeclaredNavigationProperties().Single();

        //    // Act & Assert
        //    ExceptionAssert.Throws<ODataException>(
        //        () => _binder.CreatePropertyValueExpression(_model.Customer, specialOrdersProperty, customer),
        //        "The provided mapping does not contain a resource for the resource type 'NS.SpecialCustomer'.");
        //}

        /*
    [Fact]
    public void CreatePropertyValueExpression_DerivedProperty_ReturnsPropertyAccessExpression()
    {
        Expression customer = Expression.Constant(new Customer());
        IEdmNavigationProperty specialOrdersProperty = _model.SpecialCustomer.DeclaredNavigationProperties().Single();

        Expression property = _binder.CreatePropertyValueExpression(_model.Customer, specialOrdersProperty, customer);

        Assert.Equal(String.Format("({0} As SpecialCustomer).SpecialOrders", customer.ToString()), property.ToString());
    }

    [Fact]
    public void CreatePropertyValueExpressionWithFilter_ReturnsPropertyAccessExpression()
    {
        _model.Model.SetAnnotationValue<ClrTypeAnnotation>(_model.Address, new ClrTypeAnnotation(typeof(Microsoft.AspNet.OData.Test.Formatter.Serialization.Models.Address)));
        Expression customer = Expression.Constant(new Customer());

        IEdmStructuralProperty homeAddressProperty = _model.Customer.Properties().FirstOrDefault(c => c.Name == "Address") as IEdmStructuralProperty;
        IEdmStructuralProperty streetProperty = _model.Address.Properties().FirstOrDefault(c => c.Name == "Street") as IEdmStructuralProperty;

        PathSelectItem selectItem = new PathSelectItem(new ODataSelectPath(new PropertySegment(homeAddressProperty), new PropertySegment(streetProperty)));

        Expression property = _binder.CreatePropertyValueExpressionWithFilter(_model.Customer, streetProperty, customer, selectItem);

        Assert.Equal(String.Format("({0} As SpecialCustomer).SpecialOrders", customer.ToString()), property.ToString());
    }

    //[Fact]
    //public void CreatePropertyValueExpression_DerivedNonNullableProperty_ReturnsPropertyAccessExpressionCastToNullable()
    //{
    //    Expression customer = Expression.Constant(new Customer());
    //    IEdmStructuralProperty specialCustomerProperty = _model.SpecialCustomer.DeclaredStructuralProperties()
    //        .Single(s => s.Name == "SpecialCustomerProperty");

    //    Expression property = _binder.CreatePropertyValueExpression(_model.Customer, specialCustomerProperty, customer);

    //    Assert.Equal(
    //        String.Format("Convert(({0} As SpecialCustomer).SpecialCustomerProperty)", customer.ToString()),
    //        property.ToString());
    //}
    */
        [Fact]
        public void CreatePropertyValueExpression_HandleNullPropagationTrue_AddsNullCheck()
        {
            _settings.HandleNullPropagation = HandleNullPropagationOption.True;
            Expression customer = Expression.Constant(new Customer());
            IEdmProperty idProperty = _model.Customer.StructuralProperties().Single(p => p.Name == "ID");

            Expression property = _binder.CreatePropertyValueExpression(_model.Customer, idProperty, customer, null);

            // NetFx and NetCore differ in the way Expression is converted to a string.
            Assert.Equal(ExpressionType.Conditional, property.NodeType);
            ConditionalExpression conditionalExpression = property as ConditionalExpression;
            Assert.NotNull(conditionalExpression);
            Assert.Equal(typeof(int?), conditionalExpression.Type);

            Assert.Equal(ExpressionType.Convert, conditionalExpression.IfFalse.NodeType);
            UnaryExpression falseUnaryExpression = conditionalExpression.IfFalse as UnaryExpression;
            Assert.NotNull(falseUnaryExpression);
            Assert.Equal(String.Format("{0}.ID", customer.ToString()), falseUnaryExpression.Operand.ToString());
            Assert.Equal(typeof(int?), falseUnaryExpression.Type);

            Assert.Equal(ExpressionType.Constant, conditionalExpression.IfTrue.NodeType);
            ConstantExpression trueUnaryExpression = conditionalExpression.IfTrue as ConstantExpression;
            Assert.NotNull(trueUnaryExpression);
            Assert.Equal("null", trueUnaryExpression.ToString());

            Assert.Equal(ExpressionType.Equal, conditionalExpression.Test.NodeType);
            BinaryExpression binaryExpression = conditionalExpression.Test as BinaryExpression;
            Assert.NotNull(binaryExpression);
            Assert.Equal(customer.ToString(), binaryExpression.Left.ToString());
            Assert.Equal("null", binaryExpression.Right.ToString());
            Assert.Equal(typeof(bool), binaryExpression.Type);
        }

        [Fact]
        public void CreatePropertyValueExpression_HandleNullPropagationFalse_ConvertsToNullableType()
        {
            _settings.HandleNullPropagation = HandleNullPropagationOption.False;
            Expression customer = Expression.Constant(new Customer());
            IEdmProperty idProperty = _model.Customer.StructuralProperties().Single(p => p.Name == "ID");

            Expression property = _binder.CreatePropertyValueExpression(_model.Customer, idProperty, customer, filterClause: null);

#if NETCORE
            Assert.Equal(String.Format("Convert({0}.ID, Nullable`1)", customer.ToString()), property.ToString());
#else
            Assert.Equal(String.Format("Convert({0}.ID)", customer.ToString()), property.ToString());
#endif
            Assert.Equal(typeof(int?), property.Type);
        }

        [Fact]
        public void CreatePropertyValueExpressionWithFilter_Collection_ThrowsODataException_IfMappingTypeIsNotFoundInModel()
        {
            // Arrange
            _model.Model.SetAnnotationValue<ClrTypeAnnotation>(_model.Order, value: null);
            var customer = Expression.Constant(new Customer());
            var ordersProperty = _model.Customer.NavigationProperties().Single(p => p.Name == "Orders");
            var parser = new ODataQueryOptionParser(
                _model.Model,
                _model.Order,
                _model.Orders,
                new Dictionary<string, string> { { "$filter", "ID eq 1" } });
            var filterClause = parser.ParseFilter();
            ExpandedNavigationSelectItem expandItem = new ExpandedNavigationSelectItem(new ODataExpandPath(new NavigationPropertySegment(_model.Order.NavigationProperties().Single(), navigationSource: _model.Customers)),
                null, null, filterClause, null, null, null, null, null, null);

            // Act & Assert
            ExceptionAssert.Throws<ODataException>(
                () => _binder.CreatePropertyValueExpression(_model.Customer, ordersProperty, customer, filterClause),
                "The provided mapping does not contain a resource for the resource type 'NS.Order'.");

            // NetFx and NetCore differ in the way Expression is converted to a string.
            //Assert.Equal(ExpressionType.Convert, property.NodeType);
            //var unaryExpression = (property as UnaryExpression);
            //Assert.NotNull(unaryExpression);
            //Assert.Equal(String.Format("{0}.ID", customer.ToString()), unaryExpression.Operand.ToString());
            //Assert.Equal(typeof(int?), unaryExpression.Type);
        }

        //[Fact]
        //public void CreatePropertyValueExpressionWithFilter_ThrowsODataException_IfMappingTypeIsNotFoundInModel()
        //{
        //    // Arrange
        //    _model.Model.SetAnnotationValue<ClrTypeAnnotation>(_model.Order, value: null);
        //    var customer = Expression.Constant(new Customer());
        //    var ordersProperty = _model.Customer.NavigationProperties().Single(p => p.Name == "Orders");
        //    var parser = new ODataQueryOptionParser(
        //        _model.Model,
        //        _model.Order,
        //        _model.Orders,
        //        new Dictionary<string, string> { { "$filter", "ID eq 1" } });
        //    var filterCaluse = parser.ParseFilter();

        //    // Act & Assert
        //    ExceptionAssert.Throws<ODataException>(
        //        () => _binder.CreatePropertyValueExpressionWithFilter(_model.Customer, ordersProperty, customer, filterCaluse),
        //        "The provided mapping does not contain a resource for the resource type 'NS.Order'.");
        //}

        [Fact]
        public void CreatePropertyValueExpressionWithFilter_Collection_Works_HandleNullPropagationOptionIsTrue()
        {
            // Arrange
            _model.Model.SetAnnotationValue(_model.Order, new ClrTypeAnnotation(typeof(Order)));
            _settings.HandleNullPropagation = HandleNullPropagationOption.True;
            var customer =
                Expression.Constant(new Customer { Orders = new[] { new Order { ID = 1 }, new Order { ID = 2 } } });
            var ordersProperty = _model.Customer.NavigationProperties().Single(p => p.Name == "Orders");
            var parser = new ODataQueryOptionParser(
                _model.Model,
                _model.Order,
                _model.Orders,
                new Dictionary<string, string> { { "$filter", "ID eq 1" } });
            var filterClause = parser.ParseFilter();
            ExpandedNavigationSelectItem expandItem = new ExpandedNavigationSelectItem(new ODataExpandPath(new NavigationPropertySegment(_model.Order.NavigationProperties().Single(), navigationSource: _model.Customers)),
                            null, null, filterClause, null, null, null, null, null, null);

            // Act
            var filterInExpand = _binder.CreatePropertyValueExpression(
                _model.Customer,
                ordersProperty,
                customer,
                filterClause);

            // Assert
            Assert.Equal(
                string.Format(
                    "IIF((value({0}) == null), null, IIF((value({0}).Orders == null), null, " +
                    "value({0}).Orders.Where($it => ($it.ID == value({1}).TypedProperty))))",
                    customer.Type,
                    "Microsoft.AspNet.OData.Query.Expressions.LinqParameterContainer+TypedLinqParameterContainer`1[System.Int32]"),
                filterInExpand.ToString());
            var orders = Expression.Lambda(filterInExpand).Compile().DynamicInvoke() as IEnumerable<Order>;
            Assert.Single(orders);
            Assert.Equal(1, orders.ToList()[0].ID);
        }

        [Fact]
        public void CreatePropertyValueExpressionWithFilter_Collection_Works_HandleNullPropagationOptionIsFalse()
        {
            // Arrange
            _model.Model.SetAnnotationValue(_model.Order, new ClrTypeAnnotation(typeof(Order)));
            _settings.HandleNullPropagation = HandleNullPropagationOption.False;
            var customer =
                Expression.Constant(new Customer { Orders = new[] { new Order { ID = 1 }, new Order { ID = 2 } } });
            var ordersProperty = _model.Customer.NavigationProperties().Single(p => p.Name == "Orders");
            var parser = new ODataQueryOptionParser(
                _model.Model,
                _model.Order,
                _model.Orders,
                new Dictionary<string, string> { { "$filter", "ID eq 1" } });
            var filterClause = parser.ParseFilter();
            ExpandedNavigationSelectItem expandItem = new ExpandedNavigationSelectItem(new ODataExpandPath(new NavigationPropertySegment(_model.Order.NavigationProperties().Single(), navigationSource: _model.Customers)),
                            null, null, filterClause, null, null, null, null, null, null);

            // Act
            var filterInExpand = _binder.CreatePropertyValueExpression(
                _model.Customer,
                ordersProperty,
                customer,
                filterClause);

            // Assert
            Assert.Equal(
                string.Format(
                    "value({0}).Orders.Where($it => ($it.ID == value(" +
                    "Microsoft.AspNet.OData.Query.Expressions.LinqParameterContainer+TypedLinqParameterContainer`1[System.Int32]).TypedProperty))",
                    customer.Type),
                filterInExpand.ToString());
            var orders = Expression.Lambda(filterInExpand).Compile().DynamicInvoke() as IEnumerable<Order>;
            Assert.Single(orders);
            Assert.Equal(1, orders.ToList()[0].ID);
        }

        [Fact]
        public void CreatePropertyValueExpressionWithFilter_Single_ThrowsODataException_IfMappingTypeIsNotFoundInModel()
        {
            // Arrange
            _model.Model.SetAnnotationValue<ClrTypeAnnotation>(_model.Customer, value: null);
            _settings.HandleReferenceNavigationPropertyExpandFilter = true;
            var order = Expression.Constant(new Order());
            var customerProperty = _model.Order.NavigationProperties().Single(p => p.Name == "Customer");

            var parser = new ODataQueryOptionParser(
                _model.Model,
                _model.Customer,
                _model.Customers,
                new Dictionary<string, string> { { "$filter", "ID eq 1" } });
            var filterClause = parser.ParseFilter();
            ExpandedNavigationSelectItem expandItem = new ExpandedNavigationSelectItem(new ODataExpandPath(new NavigationPropertySegment(_model.Order.NavigationProperties().Single(), navigationSource: _model.Customers)),
                            null, null, filterClause, null, null, null, null, null, null);
            // Act & Assert
            ExceptionAssert.Throws<ODataException>(
                () => _binder.CreatePropertyValueExpression(_model.Order, customerProperty, order, filterClause),
                "The provided mapping does not contain a resource for the resource type 'NS.Customer'.");
        }

        [Fact]
        public void CreatePropertyValueExpressionWithFilter_Single_Works_IfSettingIsOff()
        {
            // Arrange
            _settings.HandleReferenceNavigationPropertyExpandFilter = false;
            var order = Expression.Constant(
                    new Order
                    {
                        Customer = new Customer
                        {
                            ID = 1
                        }
                    }
            );
            var customerProperty = _model.Order.NavigationProperties().Single(p => p.Name == "Customer");

            var parser = new ODataQueryOptionParser(
                _model.Model,
                _model.Customer,
                _model.Customers,
                new Dictionary<string, string> { { "$filter", "ID ne 1" } });
            var filterClause = parser.ParseFilter();
            ExpandedNavigationSelectItem expandItem = new ExpandedNavigationSelectItem(new ODataExpandPath(new NavigationPropertySegment(_model.Order.NavigationProperties().Single(), navigationSource: _model.Customers)),
                            null, null, filterClause, null, null, null, null, null, null);
            // Act 
            var filterInExpand = _binder.CreatePropertyValueExpression(_model.Order, customerProperty, order, filterClause);

            // Assert            
            var customer = Expression.Lambda(filterInExpand).Compile().DynamicInvoke() as Customer;
            Assert.NotNull(customer);
            Assert.Equal(1, customer.ID);
        }

        [Fact]
        public void CreatePropertyValueExpressionWithFilter_Single_Works_HandleNullPropagationOptionIsTrue()
        {
            // Arrange
            _settings.HandleReferenceNavigationPropertyExpandFilter = true;
            _settings.HandleNullPropagation = HandleNullPropagationOption.True;
            var order = Expression.Constant(
                    new Order
                    {
                        Customer = new Customer
                        {
                            ID = 1
                        }
                    }
            );
            var customerProperty = _model.Order.NavigationProperties().Single(p => p.Name == "Customer");

            var parser = new ODataQueryOptionParser(
                _model.Model,
                _model.Customer,
                _model.Customers,
                new Dictionary<string, string> { { "$filter", "ID ne 1" } });
            var filterClause = parser.ParseFilter();
            ExpandedNavigationSelectItem expandItem = new ExpandedNavigationSelectItem(new ODataExpandPath(new NavigationPropertySegment(_model.Order.NavigationProperties().Single(), navigationSource: _model.Customers)),
                            null, null, filterClause, null, null, null, null, null, null);
            // Act
            var filterInExpand = _binder.CreatePropertyValueExpression(_model.Order, customerProperty, order, filterClause);

            // Assert
            Assert.Equal(
                string.Format(
                    "IIF((value({0}) == null), null, IIF((value({0}).Customer == null), null, " +
                    "IIF((value({0}).Customer.ID != value(Microsoft.AspNet.OData.Query.Expressions.LinqParameterContainer+TypedLinqParameterContainer`1[System.Int32]).TypedProperty), " +
                    "value({0}).Customer, null)))",
                    order.Type),
                filterInExpand.ToString());
            var customer = Expression.Lambda(filterInExpand).Compile().DynamicInvoke() as Customer;
            Assert.Null(customer);
        }

        [Fact]
        public void CreatePropertyValueExpressionWithFilter_Single_Works_HandleNullPropagationOptionIsFalse()
        {
            // Arrange
            _settings.HandleReferenceNavigationPropertyExpandFilter = true;
            _settings.HandleNullPropagation = HandleNullPropagationOption.False;
            var source = Expression.Constant(
                    new Order
                    {
                        Customer = new Customer
                        {
                            ID = 1
                        }
                    }
            );
            var customerProperty = _model.Order.NavigationProperties().Single(p => p.Name == "Customer");

            var parser = new ODataQueryOptionParser(
                _model.Model,
                _model.Customer,
                _model.Customers,
                new Dictionary<string, string> { { "$filter", "ID ne 1" } });
            var filterClause = parser.ParseFilter();
            ExpandedNavigationSelectItem expandItem = new ExpandedNavigationSelectItem(new ODataExpandPath(new NavigationPropertySegment(_model.Order.NavigationProperties().Single(), navigationSource: _model.Customers)),
                            null, null, filterClause, null, null, null, null, null, null);
            // Act
            var filterInExpand = _binder.CreatePropertyValueExpression(_model.Order, customerProperty, source, filterClause);

            // Assert
            Assert.Equal(
                string.Format(
                    "IIF((value({0}).Customer.ID != value(Microsoft.AspNet.OData.Query.Expressions.LinqParameterContainer+TypedLinqParameterContainer`1[System.Int32]).TypedProperty), " +
                    "value({0}).Customer, null)",
                    source.Type),
                filterInExpand.ToString());
            var customer = Expression.Lambda(filterInExpand).Compile().DynamicInvoke() as Customer;
            Assert.Null(customer);
        }

        [Fact]
        public void CreateTypeNameExpression_ReturnsNull_IfTypeHasNoDerivedTypes()
        {
            // Arrange
            IEdmEntityType baseType = new EdmEntityType("NS", "BaseType");
            EdmModel model = new EdmModel();
            model.AddElement(baseType);

            Expression source = Expression.Constant(42);

            // Act
            Expression result = SelectExpandBinder.CreateTypeNameExpression(source, baseType, model);

            // Assert
            Assert.Null(result);
        }

        //[Fact]
        //public void CreateTypeNameExpression_ThrowsODataException_IfTypeHasNoMapping()
        //{
        //    // Arrange
        //    IEdmEntityType baseType = new EdmEntityType("NS", "BaseType");
        //    IEdmEntityType derivedType = new EdmEntityType("NS", "DerivedType", baseType);
        //    EdmModel model = new EdmModel();
        //    model.AddElement(baseType);
        //    model.AddElement(derivedType);

        //    Expression source = Expression.Constant(42);

        //    // Act & Assert
        //    ExceptionAssert.Throws<ODataException>(
        //        () => SelectExpandBinder.CreateTypeNameExpression(source, baseType, model),
        //        "The provided mapping does not contain a resource for the resource type 'NS.DerivedType'.");
        //}

        [Fact]
        public void CreateTypeNameExpression_ReturnsConditionalExpression_IfTypeHasDerivedTypes()
        {
            // Arrange
            IEdmEntityType baseType = new EdmEntityType("NS", "BaseType");
            IEdmEntityType typeA = new EdmEntityType("NS", "A", baseType);
            IEdmEntityType typeB = new EdmEntityType("NS", "B", baseType);
            IEdmEntityType typeAA = new EdmEntityType("NS", "AA", typeA);
            IEdmEntityType typeAAA = new EdmEntityType("NS", "AAA", typeAA);
            IEdmEntityType[] types = new[] { baseType, typeA, typeAAA, typeB, typeAA };

            EdmModel model = new EdmModel();
            foreach (var type in types)
            {
                model.AddElement(type);
                model.SetAnnotationValue(type, new ClrTypeAnnotation(new MockType(type.Name, @namespace: type.Namespace)));
            }

            Expression source = Expression.Constant(42);

            // Act
            Expression result = SelectExpandBinder.CreateTypeNameExpression(source, baseType, model);

            // Assert
            Assert.Equal(
                @"IIF((42 Is AAA), ""NS.AAA"", IIF((42 Is AA), ""NS.AA"", IIF((42 Is B), ""NS.B"", IIF((42 Is A), ""NS.A"", ""NS.BaseType""))))",
                result.ToString());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CorrelatedSubqueryIncludesToListIfBufferingOptimizationIsTrue(bool enableOptimization)
        {
            // Arrange
            _settings.EnableCorrelatedSubqueryBuffering = enableOptimization;
            var customer =
                Expression.Constant(new Customer { Orders = new[] { new Order { ID = 1 }, new Order { ID = 2 } } });
            var parser = new ODataQueryOptionParser(
                _model.Model,
                _model.Customer,
                _model.Customers,
                new Dictionary<string, string> { { "$expand", "Orders" } });
            var expandClause = parser.ParseSelectAndExpand();

            // Act
            var expand = _binder.ProjectAsWrapper(
                customer,
                expandClause,
                _model.Customer,
                _model.Customers);

            // Assert
            Assert.True(expand.ToString().Contains("ToList") == enableOptimization);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CorrelatedSubqueryIncludesToListIfBufferingOptimizationIsTrueAndPagesizeIsSet(bool enableOptimization)
        {
            // Arrange
            _settings.EnableCorrelatedSubqueryBuffering = enableOptimization;
            _settings.PageSize = 100;
            var customer =
                Expression.Constant(new Customer { Orders = new[] { new Order { ID = 1 }, new Order { ID = 2 } } });
            var parser = new ODataQueryOptionParser(
                _model.Model,
                _model.Customer,
                _model.Customers,
                new Dictionary<string, string> { { "$expand", "Orders" } });
            var expandClause = parser.ParseSelectAndExpand();

            // Act
            var expand = _binder.ProjectAsWrapper(
                customer,
                expandClause,
                _model.Customer,
                _model.Customers);

            // Assert
            Assert.True(expand.ToString().Contains("ToList") == enableOptimization);
        }

        private class SpecialCustomer : Customer
        {
            public int SpecialCustomerProperty { get; set; }

            public SpecialOrder[] SpecialOrders { get; set; }
        }

        private class SpecialOrder : Order
        {
            public SpecialCustomer[] SpecialCustomers { get; set; }
        }

        private static IEdmModel _queryModel;

        public static IEdmModel QueryModel
        {
            get
            {
                if (_queryModel == null)
                {
                    _queryModel = GetEdmModel();
                }

                return _queryModel;
            }
        }

        public static IEdmModel GetEdmModel()
        {
            var builder = new ODataConventionModelBuilder();
            builder.EntitySet<QueryCustomer>("Customers");
            builder.EntitySet<QueryOrder>("Orders");
            builder.EntitySet<QueryCity>("Cities");
            return builder.GetEdmModel();
        }

        public static SelectExpandClause ParseSelectExpand(string select, string expand)
        {
            // use default:
            IEdmModel model = QueryModel;
            IEdmEntityType customer = model.SchemaElements.OfType<IEdmEntityType>().FirstOrDefault(c => c.Name == "QueryCustomer");
            Assert.NotNull(customer);

            IEdmEntitySet customers = model.EntityContainer.FindEntitySet("Customers");
            Assert.NotNull(customers);

            return ParseSelectExpand(select, expand, model, customer, customers);
        }

        public static SelectExpandClause ParseSelectExpand(string select, string expand, IEdmModel model, IEdmType edmType, IEdmNavigationSource navigationSource)
        {
            return new ODataQueryOptionParser(model, edmType, navigationSource,
                new Dictionary<string, string>
                {
                    { "$expand", expand == null ? "" : expand },
                    { "$select", select == null ? "" : select }
                }).ParseSelectAndExpand();
        }
    }

    public class QueryCity
    {
        public int Id { get; set; }

        public int CityName { get; set; }
    }


    public class QueryAddressInfo
    {
        public QueryCity InfoCity { get; set; }
    }

    public class QueryAddress
    {
        public string Street { get; set; }

        public string Region { get; set; }

        public IList<string> Codes { get; set; }

        public QueryAddressInfo Info { get; set; }

        public IList<int> Prices { get; set; }

        public QueryCity RelatedCity { get; set; }

        [ConcurrencyCheck]
        public double AddressETag { get; set; }

        public IList<QueryCity> Cities { get; set; }

        public IDictionary<string, object> AddressDynaicProperties { get; set; }
    }

    public class QueryUsAddress : QueryAddress
    {
        public QueryCity UsCity { get; set; }

        public IList<QueryCity> UsCities { get; set; }
    }

    public class QueryCnAddress : QueryAddress
    {
        public QueryCity CnCity { get; set; }

        public IList<QueryCity> CnCities { get; set; }
    }

    public class QueryOrder
    {
        public int Id { get; set; }

        public string Title { get; set; }

        public QueryCustomer Customer { get; set; }

        public IDictionary<string, object> OrderProperties { get; set; }
    }

    public class QueryCustomer
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public IList<string> Emails { get; set; }

        [ConcurrencyCheck]
        public double CustomerETag { get; set; }

        public QueryColor FarivateColor { get; set; }

        public QueryAddress HomeAddress { get; set; }

        public IList<QueryAddress> Addresses { get; set; }

        public QueryOrder PrivateOrder { get; set; }

        public IList<QueryOrder> Orders { get; set; }

        public IDictionary<string, object> CustomerProperties { get; set; }
    }

    public class QueryVipCustomer : QueryCustomer
    {
        public int Level { get; set; }

        public DateTimeOffset Birthday { get; set; }

        public IList<int> Taxes { get; set; }

        public decimal Bonus { get; set; }

        public QueryAddress VipAddress { get; set; }

        public QueryAddress[] VipAddresses { get; set; }

        public QueryVipOrder SpecialOrder { get; set; }

        public QueryVipOrder[] SpecialOrders { get; set; }
    }

    public class QueryVipOrder : QueryOrder
    {
        public QueryVipCustomer[] SpecialCustomers { get; set; }
    }

    public enum QueryColor
    {
        Red,

        Green,

        Blue
    }
}
