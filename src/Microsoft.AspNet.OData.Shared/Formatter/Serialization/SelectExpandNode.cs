// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
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
        }

        /// <summary>
        /// Creates a new instance of the <see cref="SelectExpandNode"/> class by copying the state of another instance. This is
        /// intended for scenarios that wish to modify state without updating the values cached within ODataResourceSerializer.
        /// </summary>
        /// <param name="selectExpandNodeToCopy">The instance from which the state for the new instance will be copied.</param>
        public SelectExpandNode(SelectExpandNode selectExpandNodeToCopy)
        {
            SelectedStructuralProperties = selectExpandNodeToCopy.SelectedStructuralProperties == null ?
                null : new HashSet<IEdmStructuralProperty>(selectExpandNodeToCopy.SelectedStructuralProperties);

            SelectedComplexesWithPath = selectExpandNodeToCopy.SelectedComplexesWithPath == null ?
                null : new Dictionary<IEdmStructuralProperty, PathSelectItem>(selectExpandNodeToCopy.SelectedComplexesWithPath);

            SelectedNavigationProperties = selectExpandNodeToCopy.SelectedNavigationProperties == null ?
                null : new HashSet<IEdmNavigationProperty>(selectExpandNodeToCopy.SelectedNavigationProperties);

            ExpandedProperties = selectExpandNodeToCopy.ExpandedProperties == null ?
                null : new Dictionary<IEdmNavigationProperty, ExpandedNavigationSelectItem>(selectExpandNodeToCopy.ExpandedProperties);

            ReferencedNavigationsWithPath = selectExpandNodeToCopy.ReferencedNavigationsWithPath == null ?
                null : new Dictionary<IEdmNavigationProperty, ExpandedReferenceSelectItem>();

            SelectAllDynamicProperties = selectExpandNodeToCopy.SelectAllDynamicProperties;

            SelectedDynamicProperties = selectExpandNodeToCopy.SelectedDynamicProperties == null ?
                null : new HashSet<string>(selectExpandNodeToCopy.SelectedDynamicProperties);

            SelectedActions = selectExpandNodeToCopy.SelectedActions == null ?
                null : new HashSet<IEdmAction>(selectExpandNodeToCopy.SelectedActions);

            SelectedFunctions = selectExpandNodeToCopy.SelectedFunctions == null ?
                null : new HashSet<IEdmFunction>(selectExpandNodeToCopy.SelectedFunctions);
        }

        /// <summary>
        /// Creates a new instance of the <see cref="SelectExpandNode"/> class describing the set of structural properties,
        /// nested properties, navigation properties, and actions to select and expand for the given <paramref name="writeContext"/>.
        /// </summary>
        /// <param name="structuredType">The structural type of the resource that would be written.</param>
        /// <param name="writeContext">The serializer context to be used while creating the collection.</param>
        /// <remarks>The default constructor is for unit testing only.</remarks>
        public SelectExpandNode(IEdmStructuredType structuredType, ODataSerializerContext writeContext)
        {
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
        /// Gets the list of EDM navigation properties to be expand referenced in the response.
        /// </summary>
        public ISet<IEdmNavigationProperty> ReferencedNavigationProperties
        {
            get
            {
                if (ReferencedNavigationsWithPath == null)
                {
                    return null;
                }

                return new HashSet<IEdmNavigationProperty>(ReferencedNavigationsWithPath.Keys);
            }
        }

        /// <summary>
        /// Gets the list of EDM nested properties (complex or collection of complex) to be included in the response.
        /// </summary>
        public ISet<IEdmStructuralProperty> SelectedComplexProperties
        {
            get
            {
                if (SelectedComplexesWithPath == null)
                {
                    return null;
                }

                return new HashSet<IEdmStructuralProperty>(SelectedComplexesWithPath.Keys);
            }
        }

        /// <summary>
        /// Gets the list of EDM navigation properties to be expanded in the response.
        /// </summary>
        [Obsolete("This property is deprecated in favor of ExpandedProperties as this property only contains a subset of the information.")]
        public IDictionary<IEdmNavigationProperty, SelectExpandClause> ExpandedNavigationProperties
        {
            get
            {
                if (ExpandedProperties == null)
                {
                    return null;
                }

                return ExpandedProperties.ToDictionary(e => e.Key, e => e.Value.SelectAndExpand);
            }
        }

        /// <summary>
        /// Gets the list of EDM structural properties (primitive, enum or collection of them) to be included in the response.
        /// It could be null if there's no property selected.
        /// </summary>
        public ISet<IEdmStructuralProperty> SelectedStructuralProperties { get; internal set; }

        /// <summary>
        /// Gets the list of Edm structural properties (complex or complex collection) to be included in the response.
        /// The key is the Edm structural property.
        /// The value is the potential sub select item.
        /// </summary>
        internal IDictionary<IEdmStructuralProperty, PathSelectItem> SelectedComplexesWithPath { get; set; }

        /// <summary>
        /// Gets the list of EDM navigation properties to be included as links in the response. It could be null.
        /// </summary>
        public ISet<IEdmNavigationProperty> SelectedNavigationProperties { get; internal set; }

        /// <summary>
        /// Gets the list of EDM navigation properties to be expanded in the response along with the nested query options embedded in the expand.
        /// It could be null if no navigation property to expand.
        /// </summary>
        public IDictionary<IEdmNavigationProperty, ExpandedNavigationSelectItem> ExpandedProperties { get; internal set; }

        /// <summary>
        /// Gets the list of EDM navigation properties to be referenced in the response along with the nested query options embedded in the expand.
        /// It could be null if no navigation property to reference.
        /// </summary>
        internal IDictionary<IEdmNavigationProperty, ExpandedReferenceSelectItem> ReferencedNavigationsWithPath { get; set; }

        /// <summary>s
        /// Gets the list of dynamic properties to select. It could be null.
        /// </summary>
        public ISet<string> SelectedDynamicProperties { get; internal set; }

        /// <summary>
        /// Gets the flag to indicate the dynamic property to be included in the response or not.
        /// </summary>
        public bool SelectAllDynamicProperties { get; internal set; }

        /// <summary>
        /// Gets the list of OData actions to be included in the response. It could be null.
        /// </summary>
        public ISet<IEdmAction> SelectedActions { get; internal set; }

        /// <summary>
        /// Gets the list of OData functions to be included in the response. It could be null.
        /// </summary>
        public ISet<IEdmFunction> SelectedFunctions { get; internal set; }

        /// <summary>
        /// Initialize the Node from <see cref="SelectExpandNode"/>.
        /// </summary>
        /// <param name="selectExpandClause">The input select and expand clause.</param>
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
                    SelectedStructuralProperties = new HashSet<IEdmStructuralProperty>(entityType.Key());
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
                            AddStructuralProperty(property);
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

                AdjustSelectNavigationProperties();
            }
        }

        /// <summary>
        /// Build $select and $expand clause
        /// </summary>
        /// <param name="selectExpandClause">The select expand clause</param>
        /// <param name="allStructuralProperties">All structural properties (primitive, enum, complex or collection of them).</param>
        /// <param name="allNavigationProperties">All navigation properties</param>
        /// <param name="allActions">All bound actions.</param>
        /// <param name="allFunctions">All bound functions.</param>
        private void BuildSelectExpand(SelectExpandClause selectExpandClause,
            ISet<IEdmStructuralProperty> allStructuralProperties,
            ISet<IEdmNavigationProperty> allNavigationProperties,
            ISet<IEdmAction> allActions,
            ISet<IEdmFunction> allFunctions)
        {
            Contract.Assert(selectExpandClause != null);

            var currentLevelPropertiesInclude = new Dictionary<IEdmStructuralProperty, SelectExpandIncludeProperty>();

            // Explicitly set SelectAllDynamicProperties as false,
            // Below will set it as true if it meets the select all condition.
            SelectAllDynamicProperties = false;
            foreach (SelectItem selectItem in selectExpandClause.SelectedItems)
            {
                // $expand=...
                ExpandedReferenceSelectItem expandReferenceItem = selectItem as ExpandedReferenceSelectItem;
                if (expandReferenceItem != null)
                {
                    BuildExpandItem(expandReferenceItem, currentLevelPropertiesInclude);
                    continue;
                }

                PathSelectItem pathSelectItem = selectItem as PathSelectItem;
                if (pathSelectItem != null)
                {
                    // $select=abc/.../xyz
                    BuildSelectItem(pathSelectItem, currentLevelPropertiesInclude, allStructuralProperties, allActions, allFunctions);
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

                    MergeSelectedNavigationProperties(allNavigationProperties);
                    SelectAllDynamicProperties = true;
                    continue;
                }

                NamespaceQualifiedWildcardSelectItem wildCardActionSelection = selectItem as NamespaceQualifiedWildcardSelectItem;
                if (wildCardActionSelection != null)
                {
                    // $select=NS.*
                    AddNamespaceWildcardOperation(wildCardActionSelection, allActions, allFunctions);
                    continue;
                }

                throw new ODataException(Error.Format(SRResources.SelectionTypeNotSupported, selectItem.GetType().Name));
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

                MergeSelectedNavigationProperties(allNavigationProperties);
                MergeSelectedAction(allActions);
                MergeSelectedFunction(allFunctions);
                SelectAllDynamicProperties = true;
            }

            foreach (var propertyToInclude in currentLevelPropertiesInclude)
            {
                IEdmStructuralProperty structuralProperty = propertyToInclude.Key;
                PathSelectItem pathSelectItem = propertyToInclude.Value == null ? null : propertyToInclude.Value.ToPathSelectItem();
                AddStructuralProperty(structuralProperty, pathSelectItem);
            }
        }

        /// <summary>
        /// Build the $expand item, it maybe $expand=complex/nav, $expand=nav, $expand=nav/$ref, etc.
        /// </summary>
        /// <param name="expandReferenceItem">The expanded reference select item.</param>
        /// <param name="currentLevelPropertiesInclude">The current properties to include at current level.</param>
        private void BuildExpandItem(ExpandedReferenceSelectItem expandReferenceItem, IDictionary<IEdmStructuralProperty, SelectExpandIncludeProperty> currentLevelPropertiesInclude)
        {
            Contract.Assert(expandReferenceItem != null && expandReferenceItem.PathToNavigationProperty != null);
            Contract.Assert(currentLevelPropertiesInclude != null);

            // Verify and process the $expand=abc/xyz/nav.
            ODataExpandPath expandPath = expandReferenceItem.PathToNavigationProperty;
            IList<ODataPathSegment> remainingSegments, leadingSegments;
            ODataPathSegment segment = expandPath.GetFirstNonTypeCastSegment(out remainingSegments, out leadingSegments);

            PropertySegment firstPropertySegment = segment as PropertySegment;
            if (firstPropertySegment != null)
            {
                // for example: $expand=abc/xyz/nav, the remaining segment can't be null because at least the last navigation
                // property segment is there.
                Contract.Assert(remainingSegments != null);

                SelectExpandIncludeProperty newPropertySelectItem;
                if (!currentLevelPropertiesInclude.TryGetValue(firstPropertySegment.Property, out newPropertySelectItem))
                {
                    newPropertySelectItem = new SelectExpandIncludeProperty(firstPropertySegment, null, leadingSegments);
                    currentLevelPropertiesInclude[firstPropertySegment.Property] = newPropertySelectItem;
                }
                else
                {
                    Contract.Assert(newPropertySelectItem != null);
                    newPropertySelectItem.VerifyTheLeadingSegments(leadingSegments);
                }

                newPropertySelectItem.AddSubExpandItem(remainingSegments, expandReferenceItem);
            }
            else
            {
                // for example: $expand=nav, the navigation property segment should be the last segment.
                // So, the remaining segments should be null.
                Contract.Assert(remainingSegments == null);

                NavigationPropertySegment firstNavigationSegment = segment as NavigationPropertySegment;
                Contract.Assert(firstNavigationSegment != null);

                // It's not allowed to have mulitple navigation expanded or referenced.
                // for example: "$expand=nav($top=2),nav($skip=3)" is not allowed and will be merged (or throw exception) at ODL side.
                ExpandedNavigationSelectItem expanded = expandReferenceItem as ExpandedNavigationSelectItem;
                if (expanded != null)
                {
                    if (ExpandedProperties == null)
                    {
                        ExpandedProperties = new Dictionary<IEdmNavigationProperty, ExpandedNavigationSelectItem>();
                    }

                    ExpandedProperties[firstNavigationSegment.NavigationProperty] = expanded;
                }
                else
                {
                    // $expand=..../nav/$ref
                    if (ReferencedNavigationsWithPath == null)
                    {
                        ReferencedNavigationsWithPath = new Dictionary<IEdmNavigationProperty, ExpandedReferenceSelectItem>();
                    }

                    ReferencedNavigationsWithPath[firstNavigationSegment.NavigationProperty] = expandReferenceItem;
                }
            }
        }

        /// <summary>
        /// Build the $select item, it maybe $select=complex/abc, $select=abc, $select=nav, etc.
        /// </summary>
        /// <param name="pathSelectItem">The expanded reference select item.</param>
        /// <param name="currentLevelPropertiesInclude">The current properties to include at current level.</param>
        /// <param name="allStructuralProperties">The all structural properties.</param>
        /// <param name="allActions">The all actions.</param>
        /// <param name="allFunctions">The all functions.</param>
        private void BuildSelectItem(PathSelectItem pathSelectItem,
            IDictionary<IEdmStructuralProperty, SelectExpandIncludeProperty> currentLevelPropertiesInclude,
            ISet<IEdmStructuralProperty> allStructuralProperties,
            ISet<IEdmAction> allActions,
            ISet<IEdmFunction> allFunctions)
        {
            Contract.Assert(pathSelectItem != null && pathSelectItem.SelectedPath != null);
            Contract.Assert(currentLevelPropertiesInclude != null);

            // Verify and process the $select=abc/xyz/....
            ODataSelectPath selectPath = pathSelectItem.SelectedPath;
            IList<ODataPathSegment> remainingSegments, leadingSegments;
            ODataPathSegment segment = selectPath.GetFirstNonTypeCastSegment(out remainingSegments, out leadingSegments);

            PropertySegment firstPropertySegment = segment as PropertySegment;
            if (firstPropertySegment != null)
            {
                if (leadingSegments == null)
                {
                    if (!allStructuralProperties.Contains(firstPropertySegment.Property))
                    {
                        return;
                    }
                }

                // $select=abc/xyz/...
                SelectExpandIncludeProperty newPropertySelectItem;
                if (!currentLevelPropertiesInclude.TryGetValue(firstPropertySegment.Property, out newPropertySelectItem))
                {
                    newPropertySelectItem = new SelectExpandIncludeProperty(firstPropertySegment, null, leadingSegments);
                    currentLevelPropertiesInclude[firstPropertySegment.Property] = newPropertySelectItem;
                }
                else
                {
                    newPropertySelectItem.VerifyTheLeadingSegments(leadingSegments);
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
                if (SelectedDynamicProperties == null)
                {
                    SelectedDynamicProperties = new HashSet<string>();
                }

                SelectedDynamicProperties.Add(dynamicPathSegment.Identifier);
                return;
            }

            // In fact, we should never be here, because it's verified above
            throw new ODataException(Error.Format(SRResources.SelectionTypeNotSupported, segment.GetType().Name));
        }

        private void MergeSelectedNavigationProperties(ISet<IEdmNavigationProperty> allNavigationProperties)
        {
            if (allNavigationProperties == null)
            {
                return;
            }

            if (SelectedNavigationProperties == null)
            {
                SelectedNavigationProperties = allNavigationProperties;
            }
            else
            {
                SelectedNavigationProperties.UnionWith(allNavigationProperties);
            }
        }

        private void MergeSelectedAction(ISet<IEdmAction> allActions)
        {
            if (allActions == null)
            {
                return;
            }

            if (SelectedActions == null)
            {
                SelectedActions = allActions;
            }
            else
            {
                SelectedActions.UnionWith(allActions);
            }
        }

        private void MergeSelectedFunction(ISet<IEdmFunction> allFunctions)
        {
            if (allFunctions == null)
            {
                return;
            }

            if (SelectedFunctions == null)
            {
                SelectedFunctions = allFunctions;
            }
            else
            {
                SelectedFunctions.UnionWith(allFunctions);
            }
        }

        private void AddStructuralProperty(IEdmStructuralProperty structuralProperty, PathSelectItem pathSelectItem = null)
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
                if (SelectedStructuralProperties == null)
                {
                    SelectedStructuralProperties = new HashSet<IEdmStructuralProperty>();
                }

                // for primitive, enum and collection them, needn't care about the nested query options now.
                // So, skip the path select item.
                SelectedStructuralProperties.Add(structuralProperty);
            }
        }

        private void AddNamespaceWildcardOperation(NamespaceQualifiedWildcardSelectItem namespaceSelectItem, ISet<IEdmAction> allActions,
            ISet<IEdmFunction> allFunctions)
        {
            if (allActions == null)
            {
                SelectedActions = null;
            }
            else
            {
                SelectedActions = new HashSet<IEdmAction>(allActions.Where(a => a.Namespace == namespaceSelectItem.Namespace));
            }

            if (allFunctions == null)
            {
                SelectedFunctions = null;
            }
            else
            {
                SelectedFunctions = new HashSet<IEdmFunction>(allFunctions.Where(a => a.Namespace == namespaceSelectItem.Namespace));
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

        private void AdjustSelectNavigationProperties()
        {
            if (SelectedNavigationProperties != null)
            {
                // remove expanded navigation properties from the selected navigation properties.
                if (ExpandedProperties != null)
                {
                    SelectedNavigationProperties.ExceptWith(ExpandedProperties.Keys);
                }

                // remove referenced navigation properties from the selected navigation properties.
                if (ReferencedNavigationsWithPath != null)
                {
                    SelectedNavigationProperties.ExceptWith(ReferencedNavigationsWithPath.Keys);
                }
            }

            if (SelectedNavigationProperties != null && !SelectedNavigationProperties.Any())
            {
                SelectedNavigationProperties = null;
            }
        }

        /// <summary>
        /// Test whether the input structural property is complex property or collection of complex property.
        /// </summary>
        /// <param name="edmStructuralProperty">The test structural property.</param>
        /// <returns>True/false.</returns>
        internal static bool IsComplexOrCollectionComplex(IEdmStructuralProperty edmStructuralProperty)
        {
            if (edmStructuralProperty == null)
            {
                return false;
            }

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

        /// <summary>
        /// Get all properties (structural properties, navigation properties, actions and functions).
        /// </summary>
        /// <param name="model">The Edm Model.</param>
        /// <param name="structuredType">The structural type.</param>
        /// <param name="allNavigationProperties">out, The navigation properties.</param>
        /// <param name="allActions">out, The bound actions.</param>
        /// <param name="allFunctions">out, The bound function.</param>
        /// <returns>The structural properties (primitive, enum, complex or collection of them.</returns>
        internal static ISet<IEdmStructuralProperty> GetAllProperties(IEdmModel model,
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
                var actions = model.GetAvailableActions(entityType);
                allActions = actions.Any() ? new HashSet<IEdmAction>(actions) : null;

                var functions = model.GetAvailableFunctions(entityType);
                allFunctions = functions.Any() ? new HashSet<IEdmFunction>(functions) : null;
            }

            return allStructuralProperties;
        }
    }
}
