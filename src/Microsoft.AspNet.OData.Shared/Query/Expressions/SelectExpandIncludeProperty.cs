// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.AspNet.OData.Common;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;

namespace Microsoft.AspNet.OData.Query.Expressions
{
    internal class SelectExpandIncludeProperty
    {
        /// <summary>
        /// the corresponding property segment.
        /// </summary>
        private PropertySegment _propertySegment;

        /// <summary>
        /// the corresponding leading segments before this property segment.
        /// </summary>
        private IList<ODataPathSegment> _leadingSegments;

        /// <summary>
        /// the corresponding navigation source. maybe useless
        /// </summary>
        private IEdmNavigationSource _navigationSource;

        /// <summary>
        /// the path select item for this property.
        /// for example: $select=abc or $select=NS.Type/abc
        /// </summary>
        private PathSelectItem _propertySelectItem;

        /// <summary>
        /// the sub $select and $expand for this property.
        /// </summary>
        private IList<SelectItem> _subSelectItems;

        /// <summary>
        /// Creates a new instance of the <see cref="SelectExpandIncludeProperty"/> class.
        /// </summary>
        /// <param name="propertySegment">The property segment that has this select expand item.</param>
        /// <param name="navigationSource">The targe navigation source of this property segment.</param>
        public SelectExpandIncludeProperty(PropertySegment propertySegment, IEdmNavigationSource navigationSource)
        {
            if (propertySegment == null)
            {
                throw Error.ArgumentNull("propertySegment");
            }

            // Contract.Assert(navigationSource != null);

            _propertySegment = propertySegment;
            _navigationSource = navigationSource;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="SelectExpandIncludeProperty"/> class.
        /// </summary>
        /// <param name="propertySegment">The property segment that has this select expand item.</param>
        /// <param name="navigationSource">The targe navigation source of this property segment.</param>
        /// <param name="leadingSegments">The leading segments before the input property segment.</param>
        public SelectExpandIncludeProperty(PropertySegment propertySegment, IEdmNavigationSource navigationSource, IList<ODataPathSegment> leadingSegments)
        {
            if (propertySegment == null)
            {
                throw Error.ArgumentNull("propertySegment");
            }
            // Contract.Assert(navigationSource != null);

            _propertySegment = propertySegment;
            _navigationSource = navigationSource;
            _leadingSegments = leadingSegments;
        }

        /// <summary>
        /// Gets the merged path select item for this property, <see cref="PathSelectItem"/>.
        /// </summary>
        /// <returns>Null or the created <see cref="PathSelectItem"/>.</returns>
        public PathSelectItem ToPathSelectItem()
        {
            if (_subSelectItems == null)
            {
                return _propertySelectItem;
            }

            bool IsSelectAll = false;
            if (_propertySelectItem != null && _propertySelectItem.SelectAndExpand != null)
            {
                IsSelectAll = this._propertySelectItem.SelectAndExpand.AllSelected;
                foreach (var selectItem in this._propertySelectItem.SelectAndExpand.SelectedItems)
                {
                    if (_subSelectItems == null)
                    {
                        _subSelectItems = new List<SelectItem>();
                    }

                    _subSelectItems.Add(selectItem);
                }
            }

            SelectExpandClause subSelectExpandClause = null;
            if (_subSelectItems != null && _subSelectItems.Count > 0)
            {
                if (IsSelectAll)
                {
                    // We do nothing here, because the property itself tells us to select all.
                    // besides, ODL doesn't allow $select=abc,abc(...)
                }
                else
                {
                    // Mark selectall equals "true" if only include $expand
                    // So, if only "$expand=abc/nav", it means to select all for "abc" and expand "nav" to "abc".
                    IsSelectAll = true;
                    foreach (var item in _subSelectItems)
                    {
                        // only include $expand=...., means selectAll as true
                        if (!(item is ExpandedNavigationSelectItem || item is ExpandedReferenceSelectItem))
                        {
                            IsSelectAll = false;
                            break;
                        }
                    }
                }

                subSelectExpandClause = new SelectExpandClause(_subSelectItems, IsSelectAll);
            }

            if (_propertySelectItem == null && subSelectExpandClause == null)
            {
                return null;
            }
            else if (_propertySelectItem == null)
            {
                return new PathSelectItem(new ODataSelectPath(_propertySegment), _navigationSource, subSelectExpandClause,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null);
            }
            else
            {
                return new PathSelectItem(new ODataSelectPath(_propertySegment), _navigationSource, subSelectExpandClause,
                    _propertySelectItem.FilterOption,
                    _propertySelectItem.OrderByOption,
                    _propertySelectItem.TopOption,
                    _propertySelectItem.SkipOption,
                    _propertySelectItem.CountOption,
                    _propertySelectItem.SearchOption,
                    _propertySelectItem.ComputeOption);
            }
        }

        public void VerifyTheLeadingSegments(IList<ODataPathSegment> toCompareleadingSegments)
        {
            if (toCompareleadingSegments == null && this._leadingSegments == null)
            {
                return;
            }

            if (toCompareleadingSegments == null || this._leadingSegments == null)
            {
                throw new ODataException("TODO: We found a new path with different leading segments. That's not valid.");
            }

            int existingCount = this._leadingSegments.Count;
            int newCount = toCompareleadingSegments.Count;

            if (existingCount != newCount)
            {
                throw new ODataException("TODO: We found a new path with different leading segments. That's not valid.");
            }

            for (int i = 0; i < existingCount; i++)
            {
                // Only supports the type cast segment in the leading segments.
                TypeSegment existingType = this._leadingSegments[i] as TypeSegment;
                TypeSegment newType = toCompareleadingSegments[i] as TypeSegment;

                if (existingType != null && newType != null)
                {
                    if (existingType.EdmType != newType.EdmType ||
                        existingType.ExpectedType != newType.ExpectedType)
                    {
                        throw new ODataException("TODO: We found a new path with different leading segments. That's not valid.");
                    }
                }
            }
        }

        /// <summary>
        /// Add sub $select item for this include property.
        /// </summary>
        /// <param name="remainingSegments">The remaining segments star from this include property.</param>
        /// <param name="oldSelectItem">The old $select item.</param>
        public void AddSubSelectItem(IList<ODataPathSegment> remainingSegments, PathSelectItem oldSelectItem)
        {
            if (remainingSegments == null)
            {
                // Be noted: In ODL v7.6.1, it's not allowed duplicated properties in $select.
                // for example: "$select=abc($top=2),abc($skip=2)" is not allowed in ODL library.
                // So, don't worry about the previous setting overrided by other same path.
                // However, it's possibility in later ODL version (>=7.6.2) to allow duplicated properties in $select.
                // It that case, please update the codes here otherwise the latter will win.
                
                // Besides, $select=abc,abc($top=2) is not allowed in ODL 7.6.1.
                Contract.Assert(_propertySelectItem == null);
                _propertySelectItem = oldSelectItem;
            }
            else
            {
                if (_subSelectItems == null)
                {
                    _subSelectItems = new List<SelectItem>();
                }

                _subSelectItems.Add(new PathSelectItem(new ODataSelectPath(remainingSegments), oldSelectItem.NavigationSource,
                    oldSelectItem.SelectAndExpand, oldSelectItem.FilterOption,
                    oldSelectItem.OrderByOption, oldSelectItem.TopOption,
                    oldSelectItem.SkipOption, oldSelectItem.CountOption,
                    oldSelectItem.SearchOption, oldSelectItem.ComputeOption));
            }
        }

        /// <summary>
        /// Add sub $expand item for this include property.
        /// </summary>
        /// <param name="remainingSegments">The remaining segments star from this include property.</param>
        /// <param name="oldRefItem">The old $expand item.</param>
        public void AddSubExpandItem(IList<ODataPathSegment> remainingSegments, ExpandedReferenceSelectItem oldRefItem)
        {
            // remainingSegments should never be null, because at least a navigation property segment in it.
            Contract.Assert(remainingSegments != null);

            if (_subSelectItems == null)
            {
                _subSelectItems = new List<SelectItem>();
            }

            ODataExpandPath newPath = new ODataExpandPath(remainingSegments);
            ExpandedNavigationSelectItem expandedNav = oldRefItem as ExpandedNavigationSelectItem;
            if (expandedNav != null)
            {
                _subSelectItems.Add(new ExpandedNavigationSelectItem(newPath,
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
                _subSelectItems.Add(new ExpandedReferenceSelectItem(newPath,
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
