// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNet.OData.Formatter.Serialization;
using Microsoft.AspNet.OData.Test.Common;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using Moq;
using Xunit;

namespace Microsoft.AspNet.OData.Test.Formatter.Serialization
{
    public class SelectExpandNodeTest
    {
        private CustomersModelWithInheritance _model = new CustomersModelWithInheritance();

        [Fact]
        public void Ctor_ThrowsArgumentNull_StructuredType()
        {
            // Arrange & Act & Assert
            ExceptionAssert.ThrowsArgumentNull(
                () => new SelectExpandNode(selectExpandClause: null, structuredType: null, model: EdmCoreModel.Instance),
                "structuredType");
        }

        [Fact]
        public void Ctor_ThrowsArgumentNull_EdmModel()
        {
            // Arrange & Act & Assert
            ExceptionAssert.ThrowsArgumentNull(
                () => new SelectExpandNode(selectExpandClause: null, structuredType: new Mock<IEdmEntityType>().Object, model: null),
                "model");
        }

        [Theory]
        [InlineData("ID,ID", "ID")]
        [InlineData("NS.upgrade,NS.upgrade", "NS.upgrade")]
        public void DuplicatedSelectPathInOneDollarSelectThrows(string select, string error)
        {
            // Arrange
            ODataQueryOptionParser parser = new ODataQueryOptionParser(_model.Model, _model.Customer, _model.Customers,
                new Dictionary<string, string> { { "$select", select } });

            // Act
            Action test = () => parser.ParseSelectAndExpand();

            // Assert
            ExceptionAssert.Throws<ODataException>(test,
                String.Format("Found mutliple select terms with same select path '{0}' at one $select, please combine them together.", error));
        }

        [Theory]
        [InlineData(null, null, false, "City,ID,Name,SimpleEnum", "Account,Address,OtherAccounts")] // no select and expand -> select all
        [InlineData(null, null, true, "City,ID,Name,SimpleEnum,SpecialCustomerProperty", "Account,Address,OtherAccounts,SpecialAddress")] // no select and expand on derived type -> select all
        [InlineData("ID", null, false, "ID", null)] // simple select -> select requested
        [InlineData("ID", null, true, "ID", null)] // simple select on derived type -> select requested
        [InlineData("*", null, false, "City,ID,Name,SimpleEnum", "Account,Address,OtherAccounts")] // simple select with wild card -> select all, no duplication
        [InlineData("*", null, true, "City,ID,Name,SimpleEnum,SpecialCustomerProperty", "Account,Address,OtherAccounts,SpecialAddress")] // simple select with wild card on derived type -> select all, no duplication
        [InlineData("ID,*", null, false, "City,ID,Name,SimpleEnum", "Account,Address,OtherAccounts")] // simple select with wild card and duplicate -> select all, no duplicates
        [InlineData("ID,*", null, true, "City,ID,Name,SimpleEnum,SpecialCustomerProperty", "Account,Address,OtherAccounts,SpecialAddress")] // simple select with wild card and duplicate -> select all, no duplicates
        [InlineData("ID,Name", null, false, "ID,Name", null)] // multiple select -> select requested
        [InlineData("ID,Name", null, true, "ID,Name", null)] // multiple select on derived type -> select requested
        [InlineData("Orders", "Orders", false, null, null)] // only expand -> select no structural property
        [InlineData("Orders", "Orders", true, null, null)] // only expand -> select no structural property
        [InlineData(null, "Orders", false, "City,ID,Name,SimpleEnum", "Account,Address,OtherAccounts")] // simple expand -> select all
        [InlineData(null, "Orders", true, "City,ID,Name,SimpleEnum,SpecialCustomerProperty", "Account,Address,OtherAccounts,SpecialAddress")] // simple expand on derived type -> select all
        [InlineData("ID,Name,Orders", "Orders", false, "ID,Name", null)] // expand and select -> select requested
        [InlineData("ID,Name,Orders", "Orders", true, "ID,Name", null)] // expand and select on derived type -> select requested
        [InlineData("NS.SpecialCustomer/SpecialCustomerProperty", null, false, null, null)] // select derived type properties -> select none
        [InlineData("NS.SpecialCustomer/SpecialCustomerProperty", null, true, "SpecialCustomerProperty", null)] // select derived type properties on derived type -> select requested
        [InlineData("ID", "Orders($select=ID),Orders($expand=Customer($select=ID))", true, "ID", null)] // deep expand and selects
        public void SelectProperties_SelectsExpectedProperties_OnCustomer(
            string select, string expand, bool specialCustomer, string structuralsToSelect, string complexesToSelect)
        {
            // Arrange
            SelectExpandClause selectExpandClause = ParseSelectExpand(select, expand);

            IEdmStructuredType structuralType = specialCustomer ? _model.SpecialCustomer : _model.Customer;

            // Act
            SelectExpandNode selectExpandNode = new SelectExpandNode(selectExpandClause, structuralType, _model.Model);

            // Assert
            if (structuralsToSelect == null)
            {
                Assert.Null(selectExpandNode.SelectedStructuralProperties);
            }
            else
            {
                Assert.Equal(structuralsToSelect, String.Join(",", selectExpandNode.SelectedStructuralProperties.Select(p => p.Name).OrderBy(n => n)));
            }

            if (complexesToSelect == null)
            {
                Assert.Null(selectExpandNode.SelectedComplexProperties);
            }
            else
            {
                Assert.Equal(complexesToSelect, String.Join(",", selectExpandNode.SelectedComplexProperties.Select(p => p.Name).OrderBy(n => n)));
            }
        }

        [Theory]
        [InlineData("ID,Name,Orders", "Orders", false, "Amount,City,ID")] // expand and select -> select all
        [InlineData("ID,Name,Orders", "Orders", true, "Amount,City,ID,SpecialOrderProperty")] // expand and select on derived type -> select all
        [InlineData("ID,Name,Orders", "Orders($select=ID)", false, "ID")] // expand and select properties on expand -> select requested
        [InlineData("ID,Name,Orders", "Orders($select=ID)", true, "ID")] // expand and select properties on expand on derived type -> select requested
        [InlineData("Orders", "Orders,Orders($expand=Customer)", false, "Amount,City,ID")]
        [InlineData("Orders", "Orders,Orders($expand=Customer)", true, "Amount,City,ID,SpecialOrderProperty")]
        public void GetPropertiesToBeSelected_Selects_ExpectedProperties_OnExpandedOrders(
            string select, string expand, bool specialOrder, string structuralPropertiesToSelect)
        {
            // Arrange
            SelectExpandClause selectExpandClause = ParseSelectExpand(select, expand);

            SelectExpandClause nestedSelectExpandClause = selectExpandClause.SelectedItems.OfType<ExpandedNavigationSelectItem>().Single().SelectAndExpand;

            IEdmStructuredType structuralType = specialOrder ? _model.SpecialOrder : _model.Order;

            // Act
            SelectExpandNode selectExpandNode = new SelectExpandNode(nestedSelectExpandClause, structuralType, _model.Model);
            var result = selectExpandNode.SelectedStructuralProperties;

            // Assert
            Assert.Equal(structuralPropertiesToSelect, String.Join(",", result.Select(p => p.Name).OrderBy(n => n)));
        }

        [Theory]
        [InlineData(null, null, false, "Orders")] // no select and expand -> select all
        [InlineData(null, null, true, "Orders,SpecialOrders")] // no select and expand on derived type -> select all
        [InlineData("ID", null, false, null)] // simple select -> select none 
        [InlineData("ID", null, true, null)] // simple select on derived type -> select none
        [InlineData(null, "Orders", false, null)] // simple expand -> select non expanded
        [InlineData(null, "Orders", true, "SpecialOrders")] // simple expand on derived type -> select non expanded
        [InlineData("ID", "Orders", false, null)] // simple expand without corresponding select -> select none
        [InlineData("ID", "Orders", true, null)] // simple expand without corresponding select on derived type -> select none
        [InlineData("ID,Orders", "Orders", false, null)] // simple expand with corresponding select -> select none
        [InlineData("ID,Orders", "Orders", true, null)] // simple expand with corresponding select on derived type -> select none
        [InlineData("ID,Orders", null, false, "Orders")] // simple select without corresponding expand -> select requested
        [InlineData("ID,Orders", null, true, "Orders")] // simple select with corresponding expand on derived type -> select requested
        [InlineData("NS.SpecialCustomer/SpecialOrders", "", false, "SpecialOrders")] // select derived type properties -> select none
        [InlineData("NS.SpecialCustomer/SpecialOrders", "", true, "SpecialOrders")] // select derived type properties on derived type -> select requested
        public void SelectNavigationProperties_SelectsExpectedProperties(string select, string expand, bool specialCustomer, string propertiesToSelect)
        {
            // Arrange
            SelectExpandClause selectExpandClause = ParseSelectExpand(select, expand);
            IEdmStructuredType structuralType = specialCustomer ? _model.SpecialCustomer : _model.Customer;

            // Act
            SelectExpandNode selectExpandNode = new SelectExpandNode(selectExpandClause, structuralType, _model.Model);
            var result = selectExpandNode.SelectedNavigationProperties;

            // Assert
            if (propertiesToSelect == null)
            {
                Assert.Null(selectExpandNode.SelectedNavigationProperties);
            }
            else
            {
                Assert.Equal(propertiesToSelect, String.Join(",", selectExpandNode.SelectedNavigationProperties.Select(p => p.Name)));
            }
        }

        [Theory]
        [InlineData(null, null, false, null)] // no select and expand -> expand none
        [InlineData(null, null, true, null)] // no select and expand on derived type -> expand none
        [InlineData("Orders", null, false, null)] // simple select and no expand -> expand none
        [InlineData("Orders", null, true, null)] // simple select and no expand on derived type -> expand none
        [InlineData(null, "Orders", false, "Orders")] // simple expand and no select -> expand requested
        [InlineData(null, "Orders", true, "Orders")] // simple expand and no select on derived type -> expand requested
        [InlineData(null, "Orders,Orders,Orders", false, "Orders")] // duplicate expand -> expand requested
        [InlineData(null, "Orders,Orders,Orders", true, "Orders")] // duplicate expand on derived type -> expand requested
        [InlineData("ID", "Orders", false, "Orders")] // Expanded navigation properties MUST be returned, even if they are not specified as a selectItem.
        [InlineData("ID", "Orders", true, "Orders")] // Expanded navigation properties MUST be returned, even if they are not specified as a selectItem.
        [InlineData("Orders", "Orders", false, "Orders")] // only expand -> expand requested
        [InlineData("ID,Orders", "Orders", false, "Orders")] // simple expand and expand in select -> expand requested
        [InlineData("ID,Orders", "Orders", true, "Orders")] // simple expand and expand in select on derived type -> expand requested
        [InlineData(null, "NS.SpecialCustomer/SpecialOrders", false, "SpecialOrders")] // expand derived navigation property -> expand requested
        [InlineData(null, "NS.SpecialCustomer/SpecialOrders", true, "SpecialOrders")] // expand derived navigation property on derived type -> expand requested
        public void ExpandNavigationProperties_ExpandsExpectedProperties(string select, string expand, bool specialCustomer, string propertiesToExpand)
        {
            // Arrange
            SelectExpandClause selectExpandClause = ParseSelectExpand(select, expand);
            IEdmStructuredType structuralType = specialCustomer ? _model.SpecialCustomer : _model.Customer;

            // Act
            SelectExpandNode selectExpandNode = new SelectExpandNode(selectExpandClause, structuralType, _model.Model);

            // Assert
            if (propertiesToExpand == null)
            {
                Assert.Null(selectExpandNode.ExpandedProperties);
            }
            else
            {
                Assert.Equal(propertiesToExpand, String.Join(",", selectExpandNode.ExpandedProperties.Select(p => p.Key.Name)));
            }
        }

        [Theory]
        [InlineData(null, false, 1, 8)] // no select and no expand means to select all operations
        [InlineData(null, true, 2, 10)]
        [InlineData("*", false, null, null)] // select * means to select no operations
        [InlineData("*", true, null, null)]
        [InlineData("NS.*", false, 1, 8)] // select wild card actions means to select all starting with "NS"
        [InlineData("NS.*", true, 2, 10)]
        [InlineData("NS.upgrade", false, 1, null)] // select single action -> select requested action
        [InlineData("NS.upgrade", true, 1, null)]
        [InlineData("NS.SpecialCustomer/NS.specialUpgrade", false, null, null)] // select single derived action on base type -> select nothing
        [InlineData("NS.SpecialCustomer/NS.specialUpgrade", true, 1, null)] // select single derived action on derived type  -> select requested action
        [InlineData("NS.GetSalary", false, null, 1)] // select single function -> select requested function
        [InlineData("NS.GetSalary", true, null, 1)]
        [InlineData("NS.SpecialCustomer/NS.IsSpecialUpgraded", false, null, null)] // select single derived function on base type -> select nothing
        [InlineData("NS.SpecialCustomer/NS.IsSpecialUpgraded", true, null, 1)] // select single derived function on derived type  -> select requested function
        public void OperationsToBeSelected_Selects_ExpectedOperations(string select, bool specialCustomer, int? actionsSelected, int? functionsSelected)
        {
            // Arrange
            SelectExpandClause selectExpandClause = ParseSelectExpand(select, expand: null);
            IEdmStructuredType structuralType = specialCustomer ? _model.SpecialCustomer : _model.Customer;

            // Act
            SelectExpandNode selectExpandNode = new SelectExpandNode(selectExpandClause, structuralType, _model.Model);

            // Assert: Actions
            if (actionsSelected == null)
            {
                Assert.Null(selectExpandNode.SelectedActions);
            }
            else
            {
                Assert.Equal(actionsSelected, selectExpandNode.SelectedActions.Count);
            }

            // Assert: Functions
            if (functionsSelected == null)
            {
                Assert.Null(selectExpandNode.SelectedFunctions);
            }
            else
            {
                Assert.Equal(functionsSelected, selectExpandNode.SelectedFunctions.Count);
            }
        }

        [Fact]
        public void BuildSelectExpandNode_ThrowsODataException_IfUnknownSelectItemPresent()
        {
            // Arrange
            SelectExpandClause selectExpandClause = new SelectExpandClause(new SelectItem[] { new Mock<SelectItem>().Object }, allSelected: false);
            IEdmStructuredType structuralType = _model.Customer;

            // Act & Assert
            ExceptionAssert.Throws<ODataException>(() => new SelectExpandNode(selectExpandClause, structuralType, _model.Model),
                "$select does not support selections of type 'SelectItemProxy'.");
        }

        /*
        [Fact]
        public void ValidatePathIsSupported_ThrowsForUnsupportedPathForSelect()
        {
            ODataPath path = new ODataPath(new ValueSegment(previousType: null));

            ExceptionAssert.Throws<ODataException>(
                () => SelectExpandNode.ValidatePathIsSupportedForSelect(path),
                "A path within the select or expand query option is not supported.");
        }

        [Fact]
        public void ValidatePathIsSupported_ThrowsForUnsupportedPathForExpand()
        {
            ODataPath path = new ODataPath(new ValueSegment(previousType: null));

            ExceptionAssert.Throws<ODataException>(
                () => SelectExpandNode.ValidatePathIsSupportedForExpand(path),
                "A path within the select or expand query option is not supported.");
        }*/


        #region Test IsComplexOrCollectionComplex
        [Fact]
        public void IsComplexOrCollectionComplex_TestNullInputCorrect()
        {
            // Arrange & Act
            IEdmStructuralProperty primitiveProperty = null;

            // Assert
            Assert.False(SelectExpandNode.IsComplexOrCollectionComplex(primitiveProperty));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void IsComplexOrCollectionComplex_TestPrimitiveStructuralPropertyCorrect(bool isCollection)
        {
            // Arrange & Act
            var stringType = EdmCoreModel.Instance.GetString(false);
            EdmEntityType entityType = new EdmEntityType("NS", "Entity");
            IEdmStructuralProperty primitiveProperty;
            if (isCollection)
            {
                primitiveProperty = entityType.AddStructuralProperty("Codes", new EdmCollectionTypeReference(new EdmCollectionType(stringType)));
            }
            else
            {
                primitiveProperty = entityType.AddStructuralProperty("Id", stringType);
            }

            // Assert
            Assert.False(SelectExpandNode.IsComplexOrCollectionComplex(primitiveProperty));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void IsComplexOrCollectionComplex_TestComplexStructuralPropertyCorrect(bool isCollection)
        {
            // Arrange & Act
            var complexType = new EdmComplexTypeReference(new EdmComplexType("NS", "Complex"), false);
            EdmEntityType entityType = new EdmEntityType("NS", "Entity");

            IEdmStructuralProperty complexProperty;
            if (isCollection)
            {
                complexProperty = entityType.AddStructuralProperty("Complexes", new EdmCollectionTypeReference(new EdmCollectionType(complexType)));
            }
            else
            {
                complexProperty = entityType.AddStructuralProperty("Single", complexType);
            }

            // Assert
            Assert.True(SelectExpandNode.IsComplexOrCollectionComplex(complexProperty));
        }
        #endregion

        #region Test GetAllProperties
        [Fact]
        public void GetAllProperties_ReturnsCorrectProperties()
        {
            // Assert

            // Act
           // SelectExpandNode.GetAllProperties(_model.Model, _model.Customer, out allNavigationProperties,
           //     out allActions, out allFunctions);
            // Assert
        }
        #endregion

        public SelectExpandClause ParseSelectExpand(string select, string expand)
        {
            return new ODataQueryOptionParser(_model.Model, _model.Customer, _model.Customers,
                new Dictionary<string, string>
                {
                    { "$expand", expand == null ? "" : expand },
                    { "$select", select == null ? "" : select }
                }).ParseSelectAndExpand();
        }
    }
}
