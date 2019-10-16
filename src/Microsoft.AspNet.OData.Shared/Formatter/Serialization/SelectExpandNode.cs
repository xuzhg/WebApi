// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNet.OData.Common;
using Microsoft.AspNet.OData.Query.Expressions;
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
        /// Creates a new instance of the <see cref="SelectExpandNode"/> class.
        /// </summary>
        /// <remarks>The default constructor is for unit testing only.</remarks>
        public SelectExpandNode()
        {
            SelectedComplexProperties = new HashSet<IEdmStructuralProperty>();
            ExpandedPropertiesOnSubChildren = new Dictionary<IEdmStructuralProperty, ExpandedNavigationSelectItem>();
            ExpandedProperties = new Dictionary<IEdmNavigationProperty, ExpandedNavigationSelectItem>();

            SelectedDynamicProperties = new HashSet<string>();

            SelectedComplexesWithPath = new Dictionary<IEdmStructuralProperty, PathSelectItem>();
            SelectedStructuralWithPath = new Dictionary<IEdmStructuralProperty, PathSelectItem>();
            ReferencedNavigationsWithPath = new Dictionary<IEdmNavigationProperty, ExpandedReferenceSelectItem>();
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

            SelectAllDynamicProperties = selectExpandNodeToCopy.SelectAllDynamicProperties;
            SelectedComplexProperties = new HashSet<IEdmStructuralProperty>(selectExpandNodeToCopy.SelectedComplexProperties);
            SelectedDynamicProperties = new HashSet<string>(selectExpandNodeToCopy.SelectedDynamicProperties);

            SelectedActions = selectExpandNodeToCopy.SelectedActions == null ? null : new HashSet<IEdmAction>(selectExpandNodeToCopy.SelectedActions);
            SelectedFunctions = selectExpandNodeToCopy.SelectedFunctions == null ? null : new HashSet<IEdmFunction>(selectExpandNodeToCopy.SelectedFunctions);

            // Selected navigation properties could be null.
            SelectedNavigationProperties = selectExpandNodeToCopy.SelectedNavigationProperties == null ?
                null :
                new HashSet<IEdmNavigationProperty>(selectExpandNodeToCopy.SelectedNavigationProperties);


            SelectedStructuralWithPath = new Dictionary<IEdmStructuralProperty, PathSelectItem>(selectExpandNodeToCopy.SelectedStructuralWithPath);
            SelectedComplexesWithPath = new Dictionary<IEdmStructuralProperty, PathSelectItem>(selectExpandNodeToCopy.SelectedComplexesWithPath);
            ReferencedNavigationsWithPath = new Dictionary<IEdmNavigationProperty, ExpandedReferenceSelectItem>(selectExpandNodeToCopy.ReferencedNavigationsWithPath);
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
            Initialize(selectExpandClause, structuredType, model, expandedReference);
        }

        /// <summary>
        /// Gets the list of EDM structural properties (primitive, enum or collection of them) to be included in the response.
        /// </summary>
        public ISet<IEdmStructuralProperty> SelectedStructuralProperties
        {
            get
            {
                return new HashSet<IEdmStructuralProperty>(SelectedStructuralWithPath.Keys);
            }
        }

        /// <summary>
        /// Gets the list of EDM navigation properties to be included as links in the response. It could be null.
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
        /// Gets the list of EDM navigation properties to be expand referenced in the response. It will never be null.
        /// </summary>
        public ISet<IEdmNavigationProperty> ReferencedNavigationProperties
        {
            get
            {
                return new HashSet<IEdmNavigationProperty>(ReferencedNavigationsWithPath.Keys);
            }
        }

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
        /// Gets the list of OData actions to be included in the response. It could be null.
        /// </summary>
        public ISet<IEdmAction> SelectedActions { get; private set; }

        /// <summary>
        /// Gets the list of OData functions to be included in the response. It could be null.
        /// </summary>
        public ISet<IEdmFunction> SelectedFunctions { get; private set; }

        /// <summary>
        /// Gets the path to property corresponding to the SelectExpandNode. Null for a top-level select expand.
        /// </summary>
        internal Queue<IEdmProperty> PropertiesInPath { get; private set; }

        /// <summary>
        /// Gets the list of Edm structural properties (primitive, enum of collection of them) to be included in the response.
        /// The key is the Edm structural property.
        /// The value is the potential sub select item, for example: $select=EMails($top=2)
        /// </summary>
        internal IDictionary<IEdmStructuralProperty, PathSelectItem> SelectedStructuralWithPath { get; private set; }

        /// <summary>
        /// Gets the list of Edm structural properties (complex or complex collection) to be included in the response.
        /// The key is the Edm structural property.
        /// The value is the potential sub select item.
        /// </summary>
        internal IDictionary<IEdmStructuralProperty, PathSelectItem> SelectedComplexesWithPath { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        internal IDictionary<IEdmNavigationProperty, ExpandedReferenceSelectItem> ReferencedNavigationsWithPath { get; private set; }

        /// <summary>
        /// Gets the property corresponding to the SelectExpandNode. Null for a top-level select expand.
        /// </summary>
        internal IEdmProperty Property { get; private set; }

        /// <summary>
        /// Initialize the <see cref="SelectExpandNode"/>.
        /// </summary>
        /// <param name="selectExpandClause">The input select expand clause.</param>
        /// <param name="structuredType">The related structural type.</param>
        /// <param name="model">The Edm model.</param>
        /// <param name="expandedReference">Is expanded reference.</param>
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

            IEdmEntityType entityType = structuredType as IEdmEntityType;
            if (expandedReference)
            {
                SelectAllDynamicProperties = false;
                if (entityType != null)
                {
                    // only need to include the key properties.
                    SelectedStructuralWithPath = entityType.Key().ToDictionary(e => e, e => (PathSelectItem)null);
                }
            }
            else
            {
                ISet<IEdmNavigationProperty> allNavigationProperties; // includes all navigation properties
                ISet<IEdmAction> allActions; // includes all bound actions
                ISet<IEdmFunction> allFunctions; // includes all bound functions
                ISet<IEdmStructuralProperty> allStructuralProperties; // includes primitive, enum, complex or collection of them

                allStructuralProperties = GetAllProperties(model, structuredType, out allNavigationProperties, out allActions, out allFunctions);

                if (selectExpandClause == null)
                {
                    if (allStructuralProperties != null)
                    {
                        foreach (var property in allStructuralProperties)
                        {
                            SetStructuralProperty(property);
                        }
                    }

                    SelectedNavigationProperties = allNavigationProperties;
                    SelectedActions = allActions;
                    SelectedFunctions = allFunctions;
                    SelectAllDynamicProperties = true;
                }
                else
                {
                    BuildSelectExpand(selectExpandClause, allStructuralProperties, allNavigationProperties, allActions, allFunctions);
                }

                if (SelectedNavigationProperties != null)
                {
                    // remove expanded navigation properties from the selected navigation properties.
                    SelectedNavigationProperties.ExceptWith(ExpandedProperties.Keys);

                    // remove referenced navigation properties from the selected navigation properties.
                    SelectedNavigationProperties.ExceptWith(ReferencedNavigationProperties);
                }
            }
        }

        /// <summary>
        /// Get all properties (structural properties, navigation properties, actions and functions).
        /// </summary>
        /// <param name="model">The Edm Model.</param>
        /// <param name="structuredType">The structural type.</param>
        /// <param name="allNavigationProperties">The navigation properties.</param>
        /// <param name="allActions">The bound actions.</param>
        /// <param name="allFunctions">The bound function.</param>
        /// <returns>The structural properties (primitive, enum, complex or collection of them.</returns>
        internal ISet<IEdmStructuralProperty> GetAllProperties(IEdmModel model,
            IEdmStructuredType structuredType,
            out ISet<IEdmNavigationProperty> allNavigationProperties,
            out ISet<IEdmAction> allActions,
            out ISet<IEdmFunction> allFunctions)
        {
            Contract.Assert(structuredType != null);

            ISet<IEdmStructuralProperty> allStructuralProperties = null;
            allNavigationProperties = null;

            foreach (var edmProperty in structuredType.Properties())
            {
                switch (edmProperty.PropertyKind)
                {
                    case EdmPropertyKind.Structural:
                        if (allStructuralProperties == null)
                        {
                            allStructuralProperties = new HashSet<IEdmStructuralProperty>();
                        }

                        allStructuralProperties.Add((IEdmStructuralProperty)edmProperty);
                        break;

                    case EdmPropertyKind.Navigation:
                        if (allNavigationProperties == null)
                        {
                            allNavigationProperties = new HashSet<IEdmNavigationProperty>();
                        }

                        allNavigationProperties.Add((IEdmNavigationProperty)edmProperty);
                        break;
                }
            }

            allActions = null;
            allFunctions = null;
            IEdmEntityType entityType = structuredType as IEdmEntityType;
            if (entityType != null)
            {
                allActions = new HashSet<IEdmAction>(model.GetAvailableActions(entityType));
                allFunctions = new HashSet<IEdmFunction>(model.GetAvailableFunctions(entityType));
            }

            return allStructuralProperties;
        }



        /// <summary>
        /// Build $select and $expand clause
        /// </summary>
        /// <param name="selectExpandClause">The select expand clause</param>
        /// <param name="allStructuralProperties">All structural properties (primitive, enum, complex or collection of them).</param>
        /// <param name="allNavigationProperties">All navigation properties</param>
        /// <param name="allActions">All bound actions.</param>
        /// <param name="allFunctions">All bound functions.</param>
        internal void BuildSelectExpand(SelectExpandClause selectExpandClause,
            ISet<IEdmStructuralProperty> allStructuralProperties,
            ISet<IEdmNavigationProperty> allNavigationProperties,
            ISet<IEdmAction> allActions,
            ISet<IEdmFunction> allFunctions)
        {
            Contract.Assert(selectExpandClause != null);

            var currentLevelPropertiesInclude = new Dictionary<IEdmStructuralProperty, IncludePropertySelectItem>();

            // Process the $expand=....
            foreach (ExpandedReferenceSelectItem expandReferenceItem in selectExpandClause.SelectedItems.OfType<ExpandedReferenceSelectItem>())
            {
                BuildExpandItem(expandReferenceItem, currentLevelPropertiesInclude);
            }

            if (selectExpandClause.AllSelected)
            {
                foreach (var property in allStructuralProperties)
                {
                    if (!currentLevelPropertiesInclude.ContainsKey(property))
                    {
                        // Set the value as null is safe, because this property should not further process.
                        currentLevelPropertiesInclude[property] = null;
                    }
                }

                SelectedNavigationProperties = allNavigationProperties;
                SelectedActions = allActions;
                SelectedFunctions = allFunctions;
                SelectAllDynamicProperties = true;
            }
            else
            {
                // Explicitly set SelectAllDynamicProperties as false,
                // Below will set it as true if it meets the select all condition.
                SelectAllDynamicProperties = false;
                foreach (SelectItem selectItem in selectExpandClause.SelectedItems)
                {
                    if (selectItem is ExpandedReferenceSelectItem)
                    {
                        // skip $expand=..., that will be processed later.
                        continue;
                    }

                    PathSelectItem pathSelectItem = selectItem as PathSelectItem;
                    if (pathSelectItem != null)
                    {
                        // $select=abc/.../xyz
                        BuildSelectItem(pathSelectItem, currentLevelPropertiesInclude, allActions, allFunctions);
                        continue;
                    }

                    WildcardSelectItem wildCardSelectItem = selectItem as WildcardSelectItem;
                    if (wildCardSelectItem != null)
                    {
                        // $select=*
                        foreach (var property in allStructuralProperties)
                        {
                            if (!currentLevelPropertiesInclude.ContainsKey(property))
                            {
                                // Set the value as null is safe, because this property should not further process.
                                // Besides, if there's "WildcardSelectItem", there's no other property selection items.
                                // That's guranteed in ODL.
                                currentLevelPropertiesInclude[property] = null;
                            }
                        }

                        SelectedNavigationProperties = allNavigationProperties;
                        SelectAllDynamicProperties = true;
                        continue;
                    }

                    NamespaceQualifiedWildcardSelectItem wildCardActionSelection = selectItem as NamespaceQualifiedWildcardSelectItem;
                    if (wildCardActionSelection != null)
                    {
                        // $select=NS.*
                        SelectedActions = allActions;
                        SelectedFunctions = allFunctions;
                        continue;
                    }

                    throw new ODataException(Error.Format(SRResources.SelectionTypeNotSupported, selectItem.GetType().Name));
                }
            }

            InitializeSelectProperties(currentLevelPropertiesInclude);
        }

        /// <summary>
        /// Build the $expand item, it maybe $expand=complex/nav, $expand=nav, $expand=nav/$ref, etc.
        /// </summary>
        /// <param name="expandReferenceItem">The expanded reference select item.</param>
        /// <param name="currentLevelPropertiesInclude">The current properties to include at current level.</param>
        internal void BuildExpandItem(ExpandedReferenceSelectItem expandReferenceItem, IDictionary<IEdmStructuralProperty, IncludePropertySelectItem> currentLevelPropertiesInclude)
        {
            Contract.Assert(expandReferenceItem != null && expandReferenceItem.PathToNavigationProperty != null);
            Contract.Assert(currentLevelPropertiesInclude != null);

            // Verify and process the $expand=abc/xyz/nav.
            IList<ODataPathSegment> remainingSegments;
            ODataPathSegment segment = expandReferenceItem.PathToNavigationProperty.ProcessExpandPath(out remainingSegments);

            PropertySegment firstPropertySegment = segment as PropertySegment;
            if (firstPropertySegment != null)
            {
                // for example: $expand=abc/xyz/nav
                Contract.Assert(remainingSegments != null);

                IncludePropertySelectItem newPropertySelectItem;
                if (!currentLevelPropertiesInclude.TryGetValue(firstPropertySegment.Property, out newPropertySelectItem))
                {
                    newPropertySelectItem = new IncludePropertySelectItem(firstPropertySegment);
                    currentLevelPropertiesInclude[firstPropertySegment.Property] = newPropertySelectItem;
                }

                Contract.Assert(newPropertySelectItem != null);
                newPropertySelectItem.AddSubExpandItem(remainingSegments, expandReferenceItem);
            }
            else
            {
                // for example: $expand=nav
                Contract.Assert(remainingSegments == null);

                NavigationPropertySegment firstNavigationSegment = segment as NavigationPropertySegment;
                Contract.Assert(firstNavigationSegment != null);

                // It's not allowed to have mulitple navigation expanded or referenced.
                // for example: "$expand=nav($top=2),nav($skip=3)" is not allowed and will be merged (or throw exception) at ODL side.
                ExpandedNavigationSelectItem expanded = expandReferenceItem as ExpandedNavigationSelectItem;
                if (expanded != null)
                {
                    ExpandedProperties[firstNavigationSegment.NavigationProperty] = expanded;
                }
                else
                {
                    ReferencedNavigationsWithPath[firstNavigationSegment.NavigationProperty] = expandReferenceItem;
                }
            }
        }

        /// <summary>
        /// Build the $select item, it maybe $select=complex/abc, $select=abc, $select=nav, etc.
        /// </summary>
        /// <param name="pathSelectItem">The expanded reference select item.</param>
        /// <param name="currentLevelPropertiesInclude">The current properties to include at current level.</param>
        /// <param name="allActions">The all actions.</param>
        /// <param name="allFunctions">The all functions.</param>
        private void BuildSelectItem(PathSelectItem pathSelectItem,
            IDictionary<IEdmStructuralProperty, IncludePropertySelectItem> currentLevelPropertiesInclude,
            ISet<IEdmAction> allActions,
            ISet<IEdmFunction> allFunctions)
        {
            Contract.Assert(pathSelectItem != null && pathSelectItem.SelectedPath != null);
            Contract.Assert(currentLevelPropertiesInclude != null);

            // Verify and process the $select=abc/xyz/....
            ODataSelectPath selectPath = pathSelectItem.SelectedPath;
            IList<ODataPathSegment> remainingSegments;
            ODataPathSegment segment = selectPath.ProcessSelectPath(out remainingSegments);

            PropertySegment firstPropertySegment = segment as PropertySegment;
            if (firstPropertySegment != null)
            {
                // $select=abc/xyz/...
                IncludePropertySelectItem newPropertySelectItem;
                if (!currentLevelPropertiesInclude.TryGetValue(firstPropertySegment.Property, out newPropertySelectItem))
                {
                    newPropertySelectItem = new IncludePropertySelectItem(firstPropertySegment);
                    currentLevelPropertiesInclude[firstPropertySegment.Property] = newPropertySelectItem;
                }

                newPropertySelectItem.AddSubSelectItem(remainingSegments, pathSelectItem);
                return;
            }

            // If the first segment is not a property segment,
            // this segment should be the last segment, so the remainging segments should be null.
            Contract.Assert(remainingSegments == null);

            NavigationPropertySegment navigationSegment = segment as NavigationPropertySegment;
            if (navigationSegment != null)
            {
                // for example: $select=NavigationProperty
                // or         : $select=NS.VipCustomer/VipNav
                if (SelectedNavigationProperties == null)
                {
                    SelectedNavigationProperties = new HashSet<IEdmNavigationProperty>();
                }

                SelectedNavigationProperties.Add(navigationSegment.NavigationProperty);
                return;
            }

            OperationSegment operationSegment = segment as OperationSegment;
            if (operationSegment != null)
            {
                AddOperations(allActions, allFunctions, operationSegment);
                return;
            }

            DynamicPathSegment dynamicPathSegment = segment as DynamicPathSegment;
            if (dynamicPathSegment != null)
            {
                SelectedDynamicProperties.Add(dynamicPathSegment.Identifier);
            }

            // In fact, we should never be here, because it's verified in ValidatePath()
            throw new ODataException(Error.Format(SRResources.SelectionTypeNotSupported, segment.GetType().Name));
        }

        private void InitializeSelectProperties(IDictionary<IEdmStructuralProperty, IncludePropertySelectItem> currentLevelPropertiesInclude)
        {
            if (currentLevelPropertiesInclude == null)
            {
                return;
            }

            foreach (var propertyToInclude in currentLevelPropertiesInclude)
            {
                IEdmStructuralProperty structuralProperty = propertyToInclude.Key;
                PathSelectItem pathSelectItem = propertyToInclude.Value == null ? null : propertyToInclude.Value.ToPathSelectItem();
                SetStructuralProperty(structuralProperty, pathSelectItem);
            }
        }

        private void SetStructuralProperty(IEdmStructuralProperty structuralProperty, PathSelectItem pathSelectItem = null)
        {
            bool isComplexOrCollectComplex = IsComplexOrCollectionComplex(structuralProperty);

            if (isComplexOrCollectComplex)
            {
                if (SelectedComplexesWithPath == null)
                {
                    SelectedComplexesWithPath = new Dictionary<IEdmStructuralProperty, PathSelectItem>();
                }

                SelectedComplexesWithPath[structuralProperty] = pathSelectItem;
            }
            else
            {
                if (SelectedStructuralWithPath == null)
                {
                    SelectedStructuralWithPath = new Dictionary<IEdmStructuralProperty, PathSelectItem>();
                }

                SelectedStructuralWithPath[structuralProperty] = pathSelectItem;
            }
        }

        private void AddOperations(ISet<IEdmAction> allActions, ISet<IEdmFunction> allFunctions, OperationSegment operationSegment)
        {
            foreach (IEdmOperation operation in operationSegment.Operations)
            {
                IEdmAction action = operation as IEdmAction;
                if (action != null && allActions.Contains(action))
                {
                    if (SelectedActions == null)
                    {
                        SelectedActions = new HashSet<IEdmAction>();
                    }

                    SelectedActions.Add(action);
                }

                IEdmFunction function = operation as IEdmFunction;
                if (function != null && allFunctions.Contains(function))
                {
                    if (SelectedFunctions == null)
                    {
                        SelectedFunctions = new HashSet<IEdmFunction>();
                    }

                    SelectedFunctions.Add(function);
                }
            }
        }

        /// <summary>
        /// Test whether the input structural property is complex property or collection of complex property.
        /// </summary>
        /// <param name="edmStructuralProperty">The test structural property.</param>
        /// <returns>True/false.</returns>
        internal static bool IsComplexOrCollectionComplex(IEdmStructuralProperty edmStructuralProperty)
        {
            Contract.Assert(edmStructuralProperty != null);

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

      //  private IEdmNavigationSource CurrentNavigationSource;
    }


    internal class IncludePropertySelectItem
    {
        public IncludePropertySelectItem(PropertySegment propertySegment)
        {
            PropertySegment = propertySegment;
            SubSelectItems = new List<SelectItem>();
        }

        public IncludePropertySelectItem(PropertySegment propertySegment, IEdmNavigationSource navigationSource)
        {
            PropertySegment = propertySegment;
            SubSelectItems = new List<SelectItem>();

            NavigationSource = navigationSource;
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
            SelectExpandClause subSelectExpandClause;
            if (SubSelectItems.Any())
            {
                bool IsSelectAll = true;
                foreach (var item in SubSelectItems)
                {
                    // only include $expand=...., means selectAll as true
                    if (!(item is ExpandedNavigationSelectItem || item is ExpandedReferenceSelectItem))
                    {
                        IsSelectAll = false;
                        break;
                    }
                }

                subSelectExpandClause = new SelectExpandClause(SubSelectItems, IsSelectAll);
            }
            else
            {
                subSelectExpandClause = null;
            }

            if (subSelectExpandClause == null && FilterClause == null &&
                OrderByClause == null && TopClause == null && SkipClause == null && CountClause == null &&
                SearchClause == null && ComputeClause == null)
            {
                return null;
            }

            return new PathSelectItem(new ODataSelectPath(PropertySegment), NavigationSource, subSelectExpandClause,
                FilterClause, OrderByClause, TopClause, SkipClause, CountClause, SearchClause, ComputeClause);
        }

        public void AddSubSelectItem(IList<ODataPathSegment> remainingSegments, PathSelectItem oldSelectItem)
        {
            if (remainingSegments == null)
            {
                if (oldSelectItem.NavigationSource != null)
                {
                    NavigationSource = oldSelectItem.NavigationSource; // from ODL, it's null?
                }

                // In ODL v7.6.1, it's not allowed duplicated properties in $select.
                // It's possibility to allow duplicated properties in $select.
                // It that's the case, please update the codes here otherwise the latter will win.
                FilterClause = oldSelectItem.FilterOption;
                OrderByClause = oldSelectItem.OrderByOption;
                TopClause = oldSelectItem.TopOption;
                SkipClause = oldSelectItem.SkipOption;
                CountClause = oldSelectItem.CountOption;
                SearchClause = oldSelectItem.SearchOption;
                ComputeClause = oldSelectItem.ComputeOption;

                if (oldSelectItem.SelectAndExpand != null)
                {
                    foreach (var selectItem in oldSelectItem.SelectAndExpand.SelectedItems)
                    {
                        SubSelectItems.Add(selectItem);
                    }
                }

                // Be noted: "$select=abc($top=2),abc($skip=2)" is not allowed in ODL library.
                // So, don't worry about the previous setting overrided by other same path.
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
            // remainingSegments should never be null, because at least a navigation property segment in it.
            Contract.Assert(remainingSegments != null);

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
