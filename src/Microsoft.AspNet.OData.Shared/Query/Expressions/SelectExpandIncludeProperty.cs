// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;

namespace Microsoft.AspNet.OData.Query.Expressions
{
    internal class SelectExpandIncludeProperty
    {
        /// <summary>
        /// Creates a new instance of the <see cref="SelectExpandIncludeProperty"/> class.
        /// </summary>
        /// <param name="propertySegment">The property segment that has this select expand item.</param>
        /// <param name="navigationSource">The targe navigation source of this property segment.</param>
        public SelectExpandIncludeProperty(PropertySegment propertySegment, IEdmNavigationSource navigationSource)
        {
            Contract.Assert(propertySegment != null);
            Contract.Assert(navigationSource != null);

            PropertySegment = propertySegment;
            NavigationSource = navigationSource;
            SubSelectItems = new List<SelectItem>();
        }

        /// <summary>
        /// Gets the corresponding property segment.
        /// </summary>
        public PropertySegment PropertySegment { get; }

        /// <summary>
        /// Gets the corresponding navigation source.
        /// </summary>
        public IEdmNavigationSource NavigationSource { get; }

        /// <summary>
        /// Gets the $filter for this property.
        /// </summary>
        public FilterClause FilterClause { get; private set; }

        /// <summary>
        /// Gets the $orderby for this property.
        /// </summary>
        public OrderByClause OrderByClause { get; private set; }

        /// <summary>
        /// Gets the $top for this property.
        /// </summary>
        public long? TopClause { get; private set; }

        /// <summary>
        /// Gets the $skip for this property.
        /// </summary>
        public long? SkipClause { get; private set; }

        /// <summary>
        /// Gets the $count for this property.
        /// </summary>
        public bool? CountClause { get; private set; }

        /// <summary>
        /// Gets the $search for this property.
        /// </summary>
        public SearchClause SearchClause { get; private set; }

        /// <summary>
        /// Gets the $compute for this property.
        /// </summary>
        public ComputeClause ComputeClause { get; private set; }

        /// <summary>
        /// Gets the $select and $expand for this property.
        /// </summary>
        public IList<SelectItem> SubSelectItems { get; private set; }

        /// <summary>
        /// Gets the merged path select item for this property, <see cref="PathSelectItem"/>.
        /// </summary>
        /// <returns>Null or the created <see cref="PathSelectItem"/>.</returns>
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

            if (subSelectExpandClause == null &&
                FilterClause == null &&
                OrderByClause == null &&
                TopClause == null &&
                SkipClause == null &&
                CountClause == null &&
                SearchClause == null &&
                ComputeClause == null)
            {
                return null;
            }

            return new PathSelectItem(new ODataSelectPath(PropertySegment), NavigationSource,
                subSelectExpandClause,
                FilterClause, OrderByClause, TopClause, SkipClause, CountClause, SearchClause, ComputeClause);
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

        /// <summary>
        /// Add sub $expand item for this include property.
        /// </summary>
        /// <param name="remainingSegments">The remaining segments star from this include property.</param>
        /// <param name="oldRefItem">The old $expand item.</param>
        public void AddSubExpandItem(IList<ODataPathSegment> remainingSegments, ExpandedReferenceSelectItem oldRefItem)
        {
            // remainingSegments should never be null, because at least a navigation property segment in it.
            Contract.Assert(remainingSegments != null);

            ODataExpandPath newPath = new ODataExpandPath(remainingSegments);
            ExpandedNavigationSelectItem expandedNav = oldRefItem as ExpandedNavigationSelectItem;
            if (expandedNav != null)
            {
                SubSelectItems.Add(new ExpandedNavigationSelectItem(newPath,
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
                SubSelectItems.Add(new ExpandedReferenceSelectItem(newPath,
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
