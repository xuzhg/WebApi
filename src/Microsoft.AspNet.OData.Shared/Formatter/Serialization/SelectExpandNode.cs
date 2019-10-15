// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNet.OData.Common;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;

namespace Microsoft.AspNet.OData.Formatter.Serialization
{
    /// <summary>
    /// Describes the set of structural properties and navigation properties and actions to select and navigation properties to expand while
    /// writing an <see cref="ODataResource"/> in the response.
    /// </summary>
    public class SelectExpandNode
    {
        /// <summary>
        /// Exists to support backward compatibility as we introduced ExpandedProperties.
        /// </summary>
        private Dictionary<IEdmNavigationProperty, SelectExpandClause> cachedExpandedClauses;

        /// <summary>
        /// Creates a new instance of the <see cref="SelectExpandNode"/> class.
        /// </summary>
        /// <remarks>The default constructor is for unit testing only.</remarks>
        public SelectExpandNode()
        {
            SelectedStructuralProperties = new HashSet<IEdmStructuralProperty>();
            SelectedComplexProperties = new HashSet<IEdmStructuralProperty>();
            SelectedNavigationProperties = new HashSet<IEdmNavigationProperty>();
            ExpandedPropertiesOnSubChildren = new Dictionary<IEdmStructuralProperty, ExpandedNavigationSelectItem>();
            ExpandedProperties = new Dictionary<IEdmNavigationProperty, ExpandedNavigationSelectItem>();
            ReferencedNavigationProperties = new HashSet<IEdmNavigationProperty>();
            SelectedActions = new HashSet<IEdmAction>();
            SelectedFunctions = new HashSet<IEdmFunction>();
            SelectedDynamicProperties = new HashSet<string>();

            SelectedComplexProperties2 = new Dictionary<IEdmStructuralProperty, PathSelectItem>();
            SelectedStructuralProperties2 = new Dictionary<IEdmStructuralProperty, PathSelectItem>();
        }

        /// <summary>
        /// Creates a new instance of the <see cref="SelectExpandNode"/> class by copying the state of another instance. This is
        /// intended for scenarios that wish to modify state without updating the values cached within ODataResourceSerializer.
        /// </summary>
        /// <param name="selectExpandNodeToCopy">The instance from which the state for the new instance will be copied.</param>
        public SelectExpandNode(SelectExpandNode selectExpandNodeToCopy)
        {
            ExpandedPropertiesOnSubChildren = new Dictionary<IEdmStructuralProperty, ExpandedNavigationSelectItem>(selectExpandNodeToCopy.ExpandedPropertiesOnSubChildren);
            ExpandedProperties = new Dictionary<IEdmNavigationProperty, ExpandedNavigationSelectItem>(selectExpandNodeToCopy.ExpandedProperties);
            ReferencedNavigationProperties = new HashSet<IEdmNavigationProperty>(selectExpandNodeToCopy.ReferencedNavigationProperties);

            SelectedActions = new HashSet<IEdmAction>(selectExpandNodeToCopy.SelectedActions);
            SelectAllDynamicProperties = selectExpandNodeToCopy.SelectAllDynamicProperties;
            SelectedComplexProperties = new HashSet<IEdmStructuralProperty>(selectExpandNodeToCopy.SelectedComplexProperties);
            SelectedDynamicProperties = new HashSet<string>(selectExpandNodeToCopy.SelectedDynamicProperties);
            SelectedFunctions = new HashSet<IEdmFunction>(selectExpandNodeToCopy.SelectedFunctions);
            SelectedNavigationProperties = new HashSet<IEdmNavigationProperty>(selectExpandNodeToCopy.SelectedNavigationProperties);
            SelectedStructuralProperties = new HashSet<IEdmStructuralProperty>(selectExpandNodeToCopy.SelectedStructuralProperties);

            SelectedStructuralProperties2 = new Dictionary<IEdmStructuralProperty, PathSelectItem>();
            SelectedComplexProperties2 = new Dictionary<IEdmStructuralProperty, PathSelectItem>();
        }

        /// <summary>
        /// Creates a new instance of the <see cref="SelectExpandNode"/> class describing the set of structural properties,
        /// nested properties, navigation properties, and actions to select and expand for the given <paramref name="writeContext"/>.
        /// </summary>
        /// <param name="structuredType">The structural type of the resource that would be written.</param>
        /// <param name="writeContext">The serializer context to be used while creating the collection.</param>
        /// <remarks>The default constructor is for unit testing only.</remarks>
        public SelectExpandNode(IEdmStructuredType structuredType, ODataSerializerContext writeContext)
            : this()
        {
            Property = writeContext.EdmProperty;
            PropertiesInPath = writeContext.PropertiesInPath;
            Initialize(writeContext.SelectExpandClause, structuredType, writeContext.Model, writeContext.ExpandReference);
        }

        /// <summary>
        /// Creates a new instance of the <see cref="SelectExpandNode"/> class describing the set of structural properties,
        /// nested properties, navigation properties, and actions to select and expand for the given <paramref name="selectExpandClause"/>.
        /// </summary>
        /// <param name="selectExpandClause">The parsed $select and $expand query options.</param>
        /// <param name="structuredType">The structural type of the resource that would be written.</param>
        /// <param name="model">The <see cref="IEdmModel"/> that contains the given structural type.</param>
        public SelectExpandNode(SelectExpandClause selectExpandClause, IEdmStructuredType structuredType, IEdmModel model)
            : this(selectExpandClause, structuredType, model, false)
        {
        }

        /// <summary>
        /// Creates a new instance of the <see cref="SelectExpandNode"/> class describing the set of structural properties,
        /// nested properties, navigation properties, and actions to select and expand for the given <paramref name="selectExpandClause"/>.
        /// </summary>
        /// <param name="selectExpandClause">The parsed $select and $expand query options.</param>
        /// <param name="structuredType">The structural type of the resource that would be written.</param>
        /// <param name="model">The <see cref="IEdmModel"/> that contains the given structural type.</param>
        /// <param name="expandedReference">a boolean value indicating whether it's expanded reference.</param>
        internal SelectExpandNode(SelectExpandClause selectExpandClause, IEdmStructuredType structuredType, IEdmModel model, bool expandedReference)
            : this()
        {
            Initialize(selectExpandClause, structuredType, model, false);
        }

        private void Initialize(SelectExpandClause selectExpandClause, IEdmStructuredType structuredType, IEdmModel model, bool expandedReference)
        {
            if (structuredType == null)
            {
                throw Error.ArgumentNull("structuredType");
            }

            if (model == null)
            {
                throw Error.ArgumentNull("model");
            }

            // So far, it includes all properties of primitive, enum and collection of them
            HashSet<IEdmStructuralProperty> allStructuralProperties = new HashSet<IEdmStructuralProperty>();

            IEdmEntityType entityType = structuredType as IEdmEntityType;
            if (expandedReference)
            {
                SelectAllDynamicProperties = false;
                if (entityType != null)
                {
                    // only need to include the key properties.
                    SelectedStructuralProperties = new HashSet<IEdmStructuralProperty>(entityType.Key());
                }
            }
            else
            {
                // So far, it includes all properties of complex and collection of complex
                HashSet<IEdmStructuralProperty> allComplexStructuralProperties = new HashSet<IEdmStructuralProperty>();
                GetStructuralProperties(structuredType, allStructuralProperties, allComplexStructuralProperties);

                // So far, it includes all navigation properties
                HashSet<IEdmNavigationProperty> allNavigationProperties;
                HashSet<IEdmAction> allActions;
                HashSet<IEdmFunction> allFunctions;
                IEnumerable<SelectItem> selectItems = new List<SelectItem>();

                if (entityType != null)
                {
                    allNavigationProperties = new HashSet<IEdmNavigationProperty>(entityType.NavigationProperties());
                    allActions = new HashSet<IEdmAction>(model.GetAvailableActions(entityType));
                    allFunctions = new HashSet<IEdmFunction>(model.GetAvailableFunctions(entityType));
                }
                else if (structuredType != null)
                {
                    allNavigationProperties = new HashSet<IEdmNavigationProperty>(structuredType.NavigationProperties());
                    
                    // Currently, the library does not support for bounded operations on complex type.
                    allActions = new HashSet<IEdmAction>();
                    allFunctions = new HashSet<IEdmFunction>();
                }
                else
                {
                    allNavigationProperties = new HashSet<IEdmNavigationProperty>();
                    allActions = new HashSet<IEdmAction>();
                    allFunctions = new HashSet<IEdmFunction>();
                }

              //  IDictionary<IEdmStructuralProperty, IncludePropertySelectItem> currentLevelPropertiesInclude = null;
                if (selectExpandClause == null || selectExpandClause.AllSelected)
                {
                    SelectedStructuralProperties = allStructuralProperties;
                    SelectedComplexProperties = allComplexStructuralProperties;

                    Convert(allStructuralProperties, SelectedStructuralProperties2);
                    Convert(allComplexStructuralProperties, SelectedComplexProperties2);

                    SelectedNavigationProperties = allNavigationProperties;
                    SelectedActions = allActions;
                    SelectedFunctions = allFunctions;
                    SelectAllDynamicProperties = true;
                }
                else
                {
                    BuildSelectExpand(selectExpandClause)
                }
                /*
                else
                {
                    if (selectExpandClause.AllSelected)
                    {
                        SelectedStructuralProperties = allStructuralProperties;

                        SelectedComplexProperties = allComplexStructuralProperties;

                        Convert(allStructuralProperties, SelectedStructuralProperties2);
                        Convert(allComplexStructuralProperties, SelectedComplexProperties2);

                        SelectedNavigationProperties = allNavigationProperties;
                        SelectedActions = allActions;
                        SelectedFunctions = allFunctions;
                        SelectAllDynamicProperties = true;
                    }
                    else
                    {
                        // Explicitly set SelectAllDynamicProperties as false, while the BuildSelections method will set it as true
                        // if it meets the select all condition.
                        SelectAllDynamicProperties = false;
                        BuildSelections(selectExpandClause, allStructuralProperties, allComplexStructuralProperties, allNavigationProperties, allActions, allFunctions);
                    }

                    selectItems = selectExpandClause.SelectedItems;
                }*/

             //   BuildExpand(selectExpandClause, currentLevelPropertiesInclude);

                BuildExpansions(selectItems, allNavigationProperties);

                // remove expanded navigation properties from the selected navigation properties.
                SelectedNavigationProperties.ExceptWith(ExpandedProperties.Keys);

                // remove referenced navigation properties from the selected navigation properties.
                SelectedNavigationProperties.ExceptWith(ReferencedNavigationProperties);
            }
        }

        private void Convert(HashSet<IEdmStructuralProperty> properties, IDictionary<IEdmStructuralProperty, PathSelectItem> dics)
        {
            foreach (var property in properties)
            {
                dics[property] = null;
            }
        }

        private void BuildSelectExpand(SelectExpandClause selectExpandClause)
        {
            if (selectExpandClause == null)
            {
                return;
            }

            Dictionary<IEdmStructuralProperty, IncludePropertySelectItem> currentLevelPropertiesInclude = new Dictionary<IEdmStructuralProperty, IncludePropertySelectItem>();

            IEnumerable<SelectItem> selectedItems = selectExpandClause.SelectedItems;
            foreach (ExpandedReferenceSelectItem expandReferenceItem in selectedItems.OfType<ExpandedReferenceSelectItem>())
            {
                ValidatePathIsSupportedForExpand(expandReferenceItem.PathToNavigationProperty);

                IList<ODataPathSegment> remainingSegments = null;
                ODataPathSegment segment = ProcessSelectExpandPath(expandReferenceItem.PathToNavigationProperty, out remainingSegments);
                PropertySegment firstPropertySegment = segment as PropertySegment;
                NavigationPropertySegment firstNavigationSegment = segment as NavigationPropertySegment;

                if (firstPropertySegment != null)
                {
                    // for example: $expand=abc/xyz
                    Contract.Assert(remainingSegments != null);

                    IncludePropertySelectItem newPropertySelectItem;
                    if (!currentLevelPropertiesInclude.TryGetValue(firstPropertySegment.Property, out newPropertySelectItem))
                    {
                        newPropertySelectItem = new IncludePropertySelectItem(firstPropertySegment);
                        currentLevelPropertiesInclude[firstPropertySegment.Property] = newPropertySelectItem;
                    }

                    newPropertySelectItem.AddSubExpandItem(remainingSegments, expandReferenceItem);
                }
                else
                {
                    // for example: $expand=xyz
                    Contract.Assert(remainingSegments == null);
                    Contract.Assert(firstNavigationSegment != null);

                    if (expandReferenceItem is ExpandedNavigationSelectItem)
                    {
                        ExpandedNavigationProperties2[firstNavigationSegment.NavigationProperty] = expandReferenceItem as ExpandedNavigationSelectItem;
                    }
                    else
                    {
                        ReferencedNavigationProperties2[firstNavigationSegment.NavigationProperty] = expandReferenceItem;
                    }
                }
            }

            if (!selectExpandClause.AllSelected)
            {
                foreach (SelectItem selectItem in selectExpandClause.SelectedItems)
                {
                    if (selectItem is ExpandedReferenceSelectItem)
                    {
                        continue;
                    }

                    PathSelectItem pathSelectItem = selectItem as PathSelectItem;
                    if (pathSelectItem != null)
                    {
                        ValidatePathIsSupportedForSelect(pathSelectItem.SelectedPath);

                        IList<ODataPathSegment> remainingSegments = null;
                        ODataPathSegment segment = ProcessSelectExpandPath(pathSelectItem.SelectedPath, out remainingSegments);
                        PropertySegment firstPropertySegment = segment as PropertySegment;
                        if (firstPropertySegment != null)
                        {
                            IncludePropertySelectItem newPropertySelectItem;
                            if (!currentLevelPropertiesInclude.TryGetValue(firstPropertySegment.Property, out newPropertySelectItem))
                            {
                                newPropertySelectItem = new IncludePropertySelectItem(firstPropertySegment);
                                currentLevelPropertiesInclude[firstPropertySegment.Property] = newPropertySelectItem;
                            }

                            newPropertySelectItem.AddSubSelectItem(remainingSegments, pathSelectItem);

                            continue;
                        }

                        Contract.Assert(remainingSegments == null);

                        NavigationPropertySegment navigationSegment = segment as NavigationPropertySegment;
                        if (navigationSegment != null)
                        {
                            // for example: $select=NavigationProperty
                            // or         : $select=NS.VipCustomer/VipNav
                            SelectedNavigationProperties.Add(navigationSegment.NavigationProperty);
                            continue;
                        }

                        OperationSegment operationSegment = segment as OperationSegment;
                        if (operationSegment != null)
                        {
                            AddOperations(allActions, allFunctions, operationSegment);
                            continue;
                        }

                        DynamicPathSegment dynamicPathSegment = segment as DynamicPathSegment;
                        if (dynamicPathSegment != null)
                        {
                            SelectedDynamicProperties.Add(dynamicPathSegment.Identifier);
                            continue;
                        }

                        throw new ODataException(Error.Format(SRResources.SelectionTypeNotSupported, segment.GetType().Name));
                    }

                    WildcardSelectItem wildCardSelectItem = selectItem as WildcardSelectItem;
                    if (wildCardSelectItem != null)
                    {
                        SelectedStructuralProperties = allStructuralProperties;
                        SelectedComplexProperties = allNestedProperties;

                        Convert(allStructuralProperties, SelectedStructuralProperties2);
                        Convert(allNestedProperties, SelectedComplexProperties2);

                        SelectedNavigationProperties = allNavigationProperties;
                        SelectAllDynamicProperties = true;
                        continue;
                    }

                    NamespaceQualifiedWildcardSelectItem wildCardActionSelection = selectItem as NamespaceQualifiedWildcardSelectItem;
                    if (wildCardActionSelection != null)
                    {
                        SelectedActions = allActions;
                        SelectedFunctions = allFunctions;
                        continue;
                    }

                    throw new ODataException(Error.Format(SRResources.SelectionTypeNotSupported, selectItem.GetType().Name));
                }
            }
        }

        /// <summary>
        /// Gets the list of EDM structural properties (primitive, enum or collection of them) to be included in the response.
        /// </summary>
        public ISet<IEdmStructuralProperty> SelectedStructuralProperties { get; private set; }

        /// <summary>
        /// Gets the list of EDM navigation properties to be included as links in the response. It is deprecated in favor of ExpandedProperties
        /// </summary>
        public ISet<IEdmNavigationProperty> SelectedNavigationProperties { get; private set; }
        /*
        /// <summary>
        /// Gets the list of EDM navigation properties to be expanded in the response.
        /// </summary>
        [Obsolete("This property is deprecated in favor of ExpandedProperties as this property only contains a subset of the information.")]
        public IDictionary<IEdmNavigationProperty, SelectExpandClause> ExpandedNavigationProperties
        {
            get
            {
                if (this.cachedExpandedClauses == null)
                {
                    this.cachedExpandedClauses = ExpandedProperties.ToDictionary(item => item.Key,
                        item => item.Value != null ? item.Value.SelectAndExpand : null);
                }

                return this.cachedExpandedClauses;
            }
        }*/

        /// <summary>
        /// Gets the list of EDM navigation properties to be expanded in the response along with the nested query options embedded in the expand.
        /// </summary>
        public IDictionary<IEdmNavigationProperty, ExpandedNavigationSelectItem> ExpandedProperties { get; private set; }

        /// <summary>
        /// Gets the list of EDM navigation properties to be expand referenced in the response.
        /// </summary>
        public ISet<IEdmNavigationProperty> ReferencedNavigationProperties { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public ISet<IEdmNavigationProperty> ExpandedNavigationProperties { get; private set; }

        /// <summary>
        /// Gets the list of EDM navigation properties to be expanded on ComplexTypes in the response.
        /// </summary>
        internal IDictionary<IEdmStructuralProperty, ExpandedNavigationSelectItem> ExpandedPropertiesOnSubChildren { get; private set; }

        /// <summary>
        /// Gets the list of EDM nested properties (complex or collection of complex) to be included in the response.
        /// </summary>
        public ISet<IEdmStructuralProperty> SelectedComplexProperties { get; private set; }

        /// <summary>
        /// Gets the list of dynamic properties to select.
        /// </summary>
        public ISet<string> SelectedDynamicProperties { get; private set; }

        /// <summary>
        /// Gets the flag to indicate the dynamic property to be included in the response or not.
        /// </summary>
        public bool SelectAllDynamicProperties { get; private set; }

        /// <summary>
        /// Gets the list of OData actions to be included in the response.
        /// </summary>
        public ISet<IEdmAction> SelectedActions { get; private set; }

        /// <summary>
        /// Gets the list of OData functions to be included in the response.
        /// </summary>
        public ISet<IEdmFunction> SelectedFunctions { get; private set; }

        /// <summary>
        /// Gets the path to property corresponding to the SelectExpandNode. Null for a top-level select expand.
        /// </summary>
        internal Queue<IEdmProperty> PropertiesInPath { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public IDictionary<IEdmStructuralProperty, PathSelectItem> SelectedComplexProperties2 { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public IDictionary<IEdmStructuralProperty, PathSelectItem> SelectedStructuralProperties2 { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public IDictionary<IEdmNavigationProperty, ExpandedNavigationSelectItem> ExpandedNavigationProperties2 { get; private set; }

        /// <summary>
        /// Gets the list of EDM navigation properties to be expand referenced in the response.
        /// </summary>
        public IDictionary<IEdmNavigationProperty, ExpandedReferenceSelectItem> ReferencedNavigationProperties2 { get; private set; }

        /// <summary>
        /// Gets the property corresponding to the SelectExpandNode. Null for a top-level select expand.
        /// </summary>
        internal IEdmProperty Property { get; private set; }

        private void BuildExpand(SelectExpandClause selectExpandClause, IDictionary<IEdmStructuralProperty, IncludePropertySelectItem> currentLevelPropertiesInclude)
        {
            if (selectExpandClause == null)
            {
                // Select All?
                return;
            }

        //    Dictionary<IEdmStructuralProperty, IncludePropertySelectItem> currentLevelPropertiesInclude = new Dictionary<IEdmStructuralProperty, IncludePropertySelectItem>();

            IEnumerable<SelectItem> selectedItems = selectExpandClause.SelectedItems;
            foreach (ExpandedReferenceSelectItem expandReferenceItem in selectedItems.OfType<ExpandedReferenceSelectItem>())
            {
                ValidatePathIsSupportedForExpand(expandReferenceItem.PathToNavigationProperty);

                IList<ODataPathSegment> remainingSegments = null;
                ODataPathSegment segment = ProcessSelectExpandPath(expandReferenceItem.PathToNavigationProperty, out remainingSegments);
                PropertySegment firstPropertySegment = segment as PropertySegment;
                NavigationPropertySegment firstNavigationSegment = segment as NavigationPropertySegment;

                if (firstPropertySegment != null)
                {
                    // for example: $expand=abc/xyz
                    Contract.Assert(remainingSegments != null);

                    IncludePropertySelectItem newPropertySelectItem;
                    if (!currentLevelPropertiesInclude.TryGetValue(firstPropertySegment.Property, out newPropertySelectItem))
                    {
                        newPropertySelectItem = new IncludePropertySelectItem(firstPropertySegment);
                        currentLevelPropertiesInclude[firstPropertySegment.Property] = newPropertySelectItem;
                    }

                    newPropertySelectItem.AddSubExpandItem(remainingSegments, expandReferenceItem);
                }
                else
                {
                    // for example: $expand=xyz
                    Contract.Assert(remainingSegments == null);
                    Contract.Assert(firstNavigationSegment != null);

                    if (expandReferenceItem is ExpandedNavigationSelectItem)
                    {
                        ExpandedNavigationProperties.Add(firstNavigationSegment.NavigationProperty);
                    }
                    else
                    {
                        ReferencedNavigationProperties.Add(firstNavigationSegment.NavigationProperty);
                    }
                }
            }
        }

        private void BuildExpansions(IEnumerable<SelectItem> selectedItems, HashSet<IEdmNavigationProperty> allNavigationProperties)
        {
            foreach (SelectItem selectItem in selectedItems)
            {
                ExpandedReferenceSelectItem expandReferenceItem = selectItem as ExpandedReferenceSelectItem;
                if (expandReferenceItem != null)
                {
                    ValidatePathIsSupportedForExpand(expandReferenceItem.PathToNavigationProperty);

                    IList<ODataPathSegment> remainingSegments = null;
                    ODataPathSegment segment = ProcessSelectExpandPath(expandReferenceItem.PathToNavigationProperty, out remainingSegments);
                    PropertySegment firstPropertySegment = segment as PropertySegment;
                    NavigationPropertySegment firstNavigationSegment = segment as NavigationPropertySegment;

                    if (firstPropertySegment != null)
                    {
                        Contract.Assert(remainingSegments != null);
                        // DO Nothing here, this case will be process in $select
                    }

                    /*
                    NavigationPropertySegment navigationSegment = (NavigationPropertySegment)expandReferenceItem.PathToNavigationProperty.LastSegment;
                    IEdmNavigationProperty navigationProperty = navigationSegment.NavigationProperty;

                    int propertyCountInPath =
                        expandReferenceItem.PathToNavigationProperty.OfType<PropertySegment>().Count();

                    bool numberOfPropertiesInPathMatch =
                        (propertyCountInPath > 0 && PropertiesInPath != null &&
                         PropertiesInPath.Count == propertyCountInPath) || propertyCountInPath < 1;

                    if (numberOfPropertiesInPathMatch && allNavigationProperties.Contains(navigationProperty))
                    {
                        ExpandedNavigationSelectItem expandItem = selectItem as ExpandedNavigationSelectItem;
                        if (expandItem != null)
                        {
                            if (!ExpandedProperties.ContainsKey(navigationProperty))
                            {
                                ExpandedProperties.Add(navigationProperty, expandItem);
                            }
                            else
                            {
                                ExpandedProperties[navigationProperty] = expandItem;
                            }
                        }
                        else
                        {
                            ReferencedNavigationProperties.Add(navigationProperty);
                        }
                    }
                    else
                    {
                        //This is the case where the navigation property is not on the current type. We need to propagate the expand item to deeper SelectExpandNode.
                        IEdmStructuralProperty complexProperty = FindNextPropertySegment(expandReferenceItem.PathToNavigationProperty);

                        if (complexProperty != null)
                        {
                            SelectExpandClause newClause;
                            if (ExpandedPropertiesOnSubChildren.ContainsKey(complexProperty))
                            {
                                SelectExpandClause oldClause = ExpandedPropertiesOnSubChildren[complexProperty].SelectAndExpand;
                                newClause = new SelectExpandClause(
                                    oldClause.SelectedItems.Concat(new SelectItem[] { expandReferenceItem }), false);
                                ExpandedNavigationSelectItem newItem = new ExpandedNavigationSelectItem(expandReferenceItem.PathToNavigationProperty, navigationSegment.NavigationSource, newClause);
                                ExpandedPropertiesOnSubChildren[complexProperty] = newItem;
                            }
                            else
                            {
                                newClause = new SelectExpandClause(new SelectItem[] { expandReferenceItem }, false);
                                ExpandedNavigationSelectItem newItem = new ExpandedNavigationSelectItem(expandReferenceItem.PathToNavigationProperty, navigationSegment.NavigationSource, newClause);
                                ExpandedPropertiesOnSubChildren.Add(complexProperty, newItem);
                            }
                        }
                    }*/
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="selectExpandClause"></param>
        /// <returns></returns>
        public static bool IsSelectAll(SelectExpandClause selectExpandClause)
        {
            if (selectExpandClause == null)
            {
                return true;
            }

            if (selectExpandClause.AllSelected || selectExpandClause.SelectedItems.OfType<WildcardSelectItem>().Any())
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Finds the appropriate property segment which should be responsible for propagating the expand item.
        /// For instance, if we are creating the SelectExpandNode for property p2 for the query ~/EnitySet/Key/p1/p2/p3?$expand=NP, we want to return property p3 here.
        /// </summary>
        private IEdmStructuralProperty FindNextPropertySegment(ODataPath path)
        {
            IEdmStructuralProperty complexProperty = null;
            // If the current SelectExpandNode is not top-level and has a property associated with it then return the next property segment from the path.
            if (Property != null)
            {
                Debug.Assert(PropertiesInPath != null, "PropertiesInPath should not be null if Property is not null");
                Queue<IEdmProperty> propertyQueue = new Queue<IEdmProperty>(PropertiesInPath);

                foreach (ODataPathSegment segment in path)
                {
                    PropertySegment propertySegment = segment as PropertySegment;
                    if (propertySegment != null)
                    {
                        complexProperty = propertySegment.Property;
                        if (propertyQueue.Count == 0)
                        {
                            break;
                        }

                        if (propertyQueue.Peek().Name == complexProperty.Name)
                        {
                            propertyQueue.Dequeue();
                        }
                        else
                        {
                            return null;
                        }
                    }
                }
            }
            else
            {
                // Return the first property if top-level resource
                PropertySegment segment = path.OfType<PropertySegment>().FirstOrDefault();
                if (segment != null)
                {
                    complexProperty = segment.Property;
                }
            }

            return complexProperty;
        }

        private void BuildSelections(
            SelectExpandClause selectExpandClause,
            HashSet<IEdmStructuralProperty> allStructuralProperties,
            HashSet<IEdmStructuralProperty> allNestedProperties,
            HashSet<IEdmNavigationProperty> allNavigationProperties,
            HashSet<IEdmAction> allActions,
            HashSet<IEdmFunction> allFunctions)
        {
            Dictionary<IEdmStructuralProperty, IncludePropertySelectItem> currentLevelPropertiesInclude = new Dictionary<IEdmStructuralProperty, IncludePropertySelectItem>();

            foreach (SelectItem selectItem in selectExpandClause.SelectedItems)
            {
                //if (selectItem is ExpandedNavigationSelectItem)
                //{
                //    continue;
                //}

                ExpandedReferenceSelectItem expandedRef = selectItem as ExpandedReferenceSelectItem;
                if (expandedRef != null)
                {
                    ValidatePathIsSupportedForExpand(expandedRef.PathToNavigationProperty);

                    IList<ODataPathSegment> remainingSegments = null;
                    ODataPathSegment segment = ProcessSelectExpandPath(expandedRef.PathToNavigationProperty, out remainingSegments);
                    PropertySegment firstPropertySegment = segment as PropertySegment;
                    NavigationPropertySegment firstNavigationSegment = segment as NavigationPropertySegment;
                    if (firstPropertySegment != null)
                    {
                        // for example: $expand=abc/xyz
                        Contract.Assert(remainingSegments != null);

                        IncludePropertySelectItem newPropertySelectItem;
                        if (!currentLevelPropertiesInclude.TryGetValue(firstPropertySegment.Property, out newPropertySelectItem))
                        {
                            newPropertySelectItem = new IncludePropertySelectItem(firstPropertySegment);
                            currentLevelPropertiesInclude[firstPropertySegment.Property] = newPropertySelectItem;
                        }

                        newPropertySelectItem.AddSubExpandItem(remainingSegments, expandedRef);
                    }
                    else
                    {
                        // for example: $expand=xyz
                        Contract.Assert(remainingSegments == null);
                        Contract.Assert(firstNavigationSegment != null);

                        // DO nothing, will process in BuildExpansions
                    }

                    continue;
                }

                PathSelectItem pathSelectItem = selectItem as PathSelectItem;
                if (pathSelectItem != null)
                {
                    ValidatePathIsSupportedForSelect(pathSelectItem.SelectedPath);

                    IList<ODataPathSegment> remainingSegments = null;
                    ODataPathSegment segment = ProcessSelectExpandPath(pathSelectItem.SelectedPath, out remainingSegments);
                    PropertySegment firstPropertySegment = segment as PropertySegment;
                    if (firstPropertySegment != null)
                    {
                        IncludePropertySelectItem newPropertySelectItem;
                        if (!currentLevelPropertiesInclude.TryGetValue(firstPropertySegment.Property, out newPropertySelectItem))
                        {
                            newPropertySelectItem = new IncludePropertySelectItem(firstPropertySegment);
                            currentLevelPropertiesInclude[firstPropertySegment.Property] = newPropertySelectItem;
                        }

                        newPropertySelectItem.AddSubSelectItem(remainingSegments, pathSelectItem);

                        /*
                        ODataSelectPath nextSelectPath = remainingSegments == null ?
                            new ODataSelectPath() :
                            new ODataSelectPath(remainingSegments);

                        PathSelectItem newSelectItem = new PathSelectItem(nextSelectPath, pathSelectItem.NavigationSource,
                                    pathSelectItem.SelectAndExpand,
                                    pathSelectItem.FilterOption,
                                    pathSelectItem.OrderByOption,
                                    pathSelectItem.TopOption,
                                    pathSelectItem.SkipOption,
                                    pathSelectItem.CountOption,
                                    pathSelectItem.SearchOption,
                                    pathSelectItem.ComputeOption);

                        bool isComplexOrCollectComplex = IsComplexOrCollectionComplex(firstPropertySegment.Property);
                        IList<PathSelectItem> value;

                        if (!isComplexOrCollectComplex)
                        {
                            SelectedStructuralProperties.Add(firstPropertySegment.Property);

                            if (!SelectedStructuralProperties2.TryGetValue(firstPropertySegment.Property, out value))
                            {
                                value = new List<PathSelectItem>();

                                // $select=PrimitiveCollect($top=2)
                                SelectedStructuralProperties2[firstPropertySegment.Property] = value;
                            }
                        }
                        else
                        {
                            if (!SelectedComplexProperties2.TryGetValue(firstPropertySegment.Property, out value))
                            {
                                value = new List<PathSelectItem>();
                                SelectedComplexProperties2[firstPropertySegment.Property] = value;
                            }
                        }

                        value.Add(newSelectItem);*/
                        continue;
                    }

                    Contract.Assert(remainingSegments == null);

                    NavigationPropertySegment navigationSegment = segment as NavigationPropertySegment;
                    if (navigationSegment != null)
                    {
                        // for example: $select=NavigationProperty
                        // or         : $select=NS.VipCustomer/VipNav
                        SelectedNavigationProperties.Add(navigationSegment.NavigationProperty);
                        continue;
                    }


                    /*
                    PropertySegment structuralPropertySegment = segment as PropertySegment;
                    if (structuralPropertySegment != null)
                    {
                        IEdmStructuralProperty structuralProperty = structuralPropertySegment.Property;
                        if (allStructuralProperties.Contains(structuralProperty))
                        {
                            SelectedStructuralProperties.Add(structuralProperty);
                        }
                        else if (allNestedProperties.Contains(structuralProperty))
                        {
                            SelectedComplexProperties.Add(structuralProperty);
                        }

                        continue;
                    }*/

                    OperationSegment operationSegment = segment as OperationSegment;
                    if (operationSegment != null)
                    {
                        AddOperations(allActions, allFunctions, operationSegment);
                        continue;
                    }

                    DynamicPathSegment dynamicPathSegment = segment as DynamicPathSegment;
                    if (dynamicPathSegment != null)
                    {
                        SelectedDynamicProperties.Add(dynamicPathSegment.Identifier);
                        continue;
                    }

                    throw new ODataException(Error.Format(SRResources.SelectionTypeNotSupported, segment.GetType().Name));
                }

                WildcardSelectItem wildCardSelectItem = selectItem as WildcardSelectItem;
                if (wildCardSelectItem != null)
                {
                    SelectedStructuralProperties = allStructuralProperties;
                    SelectedComplexProperties = allNestedProperties;

                    Convert(allStructuralProperties, SelectedStructuralProperties2);
                    Convert(allNestedProperties, SelectedComplexProperties2);

                    SelectedNavigationProperties = allNavigationProperties;
                    SelectAllDynamicProperties = true;
                    continue;
                }

                NamespaceQualifiedWildcardSelectItem wildCardActionSelection = selectItem as NamespaceQualifiedWildcardSelectItem;
                if (wildCardActionSelection != null)
                {
                    SelectedActions = allActions;
                    SelectedFunctions = allFunctions;
                    continue;
                }

                throw new ODataException(Error.Format(SRResources.SelectionTypeNotSupported, selectItem.GetType().Name));
            }

            foreach (var propertyToInclude in currentLevelPropertiesInclude)
            {
                IEdmStructuralProperty structuralProperty = propertyToInclude.Key;

                bool isComplexOrCollectComplex = IsComplexOrCollectionComplex(structuralProperty);

                if (!isComplexOrCollectComplex)
                {
                    // SelectedStructuralProperties.Add(structuralProperty);
                    SelectedStructuralProperties2[structuralProperty] = propertyToInclude.Value.ToPathSelectItem();
                }
                else
                {
                    SelectedComplexProperties2[structuralProperty] = propertyToInclude.Value.ToPathSelectItem();
                }
            }
        }

        /// <summary>
        /// For example: $select=NS.SubType1/abc/NS.SubType2/xyz
        /// => firstPropertySegment: "abc"
        /// => remainingSegments:  NS.SubType2/xyz
        /// </summary>
        public static ODataPathSegment ProcessSelectExpandPath(ODataPath selectExpandPath, out IList<ODataPathSegment> remainingSegments) // could be null
        {
            remainingSegments = null;
            ODataPathSegment firstPropertySegment = null;
            foreach (var segment in selectExpandPath)
            {
                if (firstPropertySegment != null)
                {
                    if (remainingSegments == null)
                    {
                        remainingSegments = new List<ODataPathSegment>();
                    }

                    remainingSegments.Add(segment);
                    continue;
                }

                if (segment is PropertySegment || segment is NavigationPropertySegment)
                {
                    firstPropertySegment = segment;
                    continue;
                }
            }

            return firstPropertySegment;
        }

        private void AddOperations(HashSet<IEdmAction> allActions, HashSet<IEdmFunction> allFunctions, OperationSegment operationSegment)
        {
            foreach (IEdmOperation operation in operationSegment.Operations)
            {
                IEdmAction action = operation as IEdmAction;
                if (action != null && allActions.Contains(action))
                {
                    SelectedActions.Add(action);
                }

                IEdmFunction function = operation as IEdmFunction;
                if (function != null && allFunctions.Contains(function))
                {
                    SelectedFunctions.Add(function);
                }
            }
        }

        // we only support paths of type 'cast/structuralOrNavPropertyOrAction' and 'structuralOrNavPropertyOrAction'.
        // It supports the path like
        // "{cast|StructuralProperty}/|{cast|StructuralProperty}|/{NavigationProperty|StructuralProperty|OperationSegment|DynamicPathSegment|
        internal static void ValidatePathIsSupportedForSelect(ODataPath path)
        {
            int segmentCount = path.Count();

            ODataPathSegment lastSegment = path.LastSegment;
            if (!(lastSegment is NavigationPropertySegment
                || lastSegment is PropertySegment
                || lastSegment is OperationSegment
                || lastSegment is DynamicPathSegment))
            {
                throw new ODataException(Error.Format(SRResources.InvalidLastSegmentInSelectExpandPath, lastSegment.Identifier));
            }

            for (int i = 0 ; i < segmentCount - 1; i++)
            {
                ODataPathSegment segment = path.ElementAt(i);
                if (!(segment is PropertySegment
                    || segment is TypeSegment))
                {
                    throw new ODataException(Error.Format(SRResources.InvalidSegmentInSelectExpandPath, segment.Identifier));
                }
            }

            /*
            if (segmentCount > 2)
            {
                throw new ODataException(SRResources.UnsupportedSelectExpandPath);
            }

            if (segmentCount == 2)
            {
                if (!(path.FirstSegment is TypeSegment))
                {
                    throw new ODataException(SRResources.UnsupportedSelectExpandPath);
                }
            }*/
        }

        // we support paths of type 'cast/structuralOrNavPropertyOrAction', 'ComplexObject/cast/StructuralOrNavPropertyOnAction', 'ComplexObject/structuralOrNavPropertyOnAction' and 'structuralOrNavPropertyOrAction'.
        internal static void ValidatePathIsSupportedForExpand(ODataPath path)
        {
            ODataPathSegment lastSegment = path.LastSegment;
            foreach (ODataPathSegment segment in path)
            {
                if (!(segment is TypeSegment || segment is PropertySegment || (segment == lastSegment)))
                {
                    throw new ODataException(SRResources.UnsupportedSelectExpandPath);
                }
            }

            if (!(lastSegment is NavigationPropertySegment
                  || lastSegment is PropertySegment
                  || lastSegment is OperationSegment
                  || lastSegment is DynamicPathSegment))
            {
                throw new ODataException(SRResources.UnsupportedSelectExpandPath);
            }
        }

        /// <summary>
        /// Separate the structural properties into two parts:
        /// 1. Complex and collection of complex are nested structural properties.
        /// 2. Others are non-nested structural properties.
        /// </summary>
        /// <param name="structuredType">The structural type of the resource.</param>
        /// <param name="structuralProperties">The non-nested structural properties of the structural type.</param>
        /// <param name="nestedStructuralProperties">The nested structural properties of the structural type.</param>
        public static void GetStructuralProperties(IEdmStructuredType structuredType, HashSet<IEdmStructuralProperty> structuralProperties,
            HashSet<IEdmStructuralProperty> nestedStructuralProperties)
        {
            if (structuredType == null)
            {
                throw Error.ArgumentNull("structuredType");
            }

            if (structuralProperties == null)
            {
                throw Error.ArgumentNull("structuralProperties");
            }

            if (nestedStructuralProperties == null)
            {
                throw Error.ArgumentNull("nestedStructuralProperties");
            }

            foreach (var edmStructuralProperty in structuredType.StructuralProperties())
            {
                if (edmStructuralProperty.Type.IsComplex())
                {
                    nestedStructuralProperties.Add(edmStructuralProperty);
                }
                else if (edmStructuralProperty.Type.IsCollection())
                {
                    if (edmStructuralProperty.Type.AsCollection().ElementType().IsComplex())
                    {
                        nestedStructuralProperties.Add(edmStructuralProperty);
                    }
                    else
                    {
                        structuralProperties.Add(edmStructuralProperty);
                    }
                }
                else
                {
                    structuralProperties.Add(edmStructuralProperty);
                }
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="edmStructuralProperty"></param>
        /// <returns></returns>
        public static bool IsComplexOrCollectionComplex(IEdmStructuralProperty edmStructuralProperty)
        {
            if (edmStructuralProperty.Type.IsComplex())
            {
                return true;
            }

            if (edmStructuralProperty.Type.IsCollection())
            {
                if (edmStructuralProperty.Type.AsCollection().ElementType().IsComplex())
                {
                    return true;
                }
            }

            return false;
        }
    }


    internal class IncludePropertySelectItem
    {
        public IncludePropertySelectItem(PropertySegment propertySegment)
        {
            PropertySegment = propertySegment;
            SubSelectItems = new List<SelectItem>();
        }

        public PropertySegment PropertySegment { get; }

        public IEdmNavigationSource NavigationSource { get; set; }

        public FilterClause FilterClause { get; set; }

        public OrderByClause OrderByClause { get; set; }

        public long? TopClause { get; set; }

        public long? SkipClause { get; set; }

        public bool? CountClause { get; set; }

        public SearchClause SearchClause { get; set; }

        public ComputeClause ComputeClause { get; set; }

        public IList<SelectItem> SubSelectItems { get; set; }

        public PathSelectItem ToPathSelectItem()
        {
            bool IsSelectAll = true;
            foreach (var item in SubSelectItems)
            {
                if (item is PathSelectItem)
                {
                    IsSelectAll = false;
                    break;
                }
            }

            SelectExpandClause subSelectExpandClause;
            if (SubSelectItems.Any())
            {
                subSelectExpandClause = new SelectExpandClause(SubSelectItems, IsSelectAll);
            }
            else
            {
                subSelectExpandClause = null;
            }
            return new PathSelectItem(new ODataSelectPath(PropertySegment), NavigationSource, subSelectExpandClause,
                FilterClause, OrderByClause, TopClause, SkipClause, CountClause, SearchClause, ComputeClause);
        }

        public void AddSubSelectItem(IList<ODataPathSegment> remainingSegments, PathSelectItem oldSelectItem)
        {
            if (remainingSegments == null)
            {
                NavigationSource = oldSelectItem.NavigationSource;
                FilterClause = oldSelectItem.FilterOption;
                OrderByClause = oldSelectItem.OrderByOption;
                TopClause = oldSelectItem.TopOption;
                SkipClause = oldSelectItem.SkipOption;
                CountClause = oldSelectItem.CountOption;
                SearchClause = oldSelectItem.SearchOption;
                ComputeClause = oldSelectItem.ComputeOption;
            }
            else
            {
                SubSelectItems.Add(new PathSelectItem(new ODataSelectPath(remainingSegments), oldSelectItem.NavigationSource,
                    oldSelectItem.SelectAndExpand, oldSelectItem.FilterOption,
                    oldSelectItem.OrderByOption, oldSelectItem.TopOption,
                    oldSelectItem.SkipOption, oldSelectItem.CountOption,
                    oldSelectItem.SearchOption, oldSelectItem.ComputeOption));
            }
        }

        public void AddSubExpandItem(IList<ODataPathSegment> remainingSegments, ExpandedReferenceSelectItem oldRefItem)
        {
            Contract.Assert(remainingSegments != null); // should never be null, because at least a navigation property segment in it.

            ExpandedNavigationSelectItem expandedNav = oldRefItem as ExpandedNavigationSelectItem;
            if (expandedNav != null)
            {
                SubSelectItems.Add(new ExpandedNavigationSelectItem(new ODataExpandPath(remainingSegments),
                    expandedNav.NavigationSource,
                    expandedNav.SelectAndExpand,
                    expandedNav.FilterOption,
                    expandedNav.OrderByOption,
                    expandedNav.TopOption,
                    expandedNav.SkipOption,
                    expandedNav.CountOption,
                    expandedNav.SearchOption,
                    expandedNav.LevelsOption,
                    expandedNav.ComputeOption,
                    expandedNav.ApplyOption));
            }
            else
            {
                SubSelectItems.Add(new ExpandedReferenceSelectItem(new ODataExpandPath(remainingSegments),
                    oldRefItem.NavigationSource,
                    oldRefItem.FilterOption,
                    oldRefItem.OrderByOption,
                    oldRefItem.TopOption,
                    oldRefItem.SkipOption,
                    oldRefItem.CountOption,
                    oldRefItem.SearchOption,
                    oldRefItem.ComputeOption,
                    oldRefItem.ApplyOption));
            }
        }
    }
}
