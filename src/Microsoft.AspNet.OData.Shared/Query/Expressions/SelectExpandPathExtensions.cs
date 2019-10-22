// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.AspNet.OData.Common;
using Microsoft.OData;
using Microsoft.OData.UriParser;

namespace Microsoft.AspNet.OData.Query.Expressions
{
    internal static class SelectExpandPathExtensions
    {
        /// <summary>
        /// we support $select paths as:
        /// The last segment could be "NavigationPropertySegment, PropertySegment, OperationSegment, DynamicPathSegment"
        /// The middle segment should be "TypeSegment" or "PropertySegment".
        /// Others are invalid segment in $select path.
        /// </summary>
        /// <param name="selectPath">The input $select path.</param>
        public static void ValidatePath(this ODataSelectPath selectPath)
        {
            Contract.Assert(selectPath != null);

            int lastIndex = selectPath.Count() - 1;
            int index = 0;
            foreach (var segment in selectPath)
            {
                if (index == lastIndex)
                {
                    // Last segment
                    if (!(segment is NavigationPropertySegment
                        || segment is PropertySegment
                        || segment is OperationSegment
                        || segment is DynamicPathSegment))
                    {
                        throw new ODataException(Error.Format(SRResources.InvalidLastSegmentInSelectExpandPath, segment.Identifier));
                    }
                }
                else
                {
                    // middle segment
                    if (!(segment is PropertySegment || segment is TypeSegment))
                    {
                        throw new ODataException(Error.Format(SRResources.InvalidSegmentInSelectExpandPath, segment.Identifier));
                    }
                }

                index++;
            }
        }

        /// <summary>
        /// We support expand path as:
        /// The last segment should be "NavigationPropertySegment".
        /// The middle segment should be "TypeSegment" or "PropertySegment".
        /// Others are invalid segment in $expand path.
        /// </summary>
        /// <param name="expandPath">The input $expand path.</param>
        public static void ValidatePath(this ODataExpandPath expandPath)
        {
            Contract.Assert(expandPath != null);

            int lastIndex = expandPath.Count() - 1;
            int index = 0;
            foreach (var segment in expandPath)
            {
                if (index == lastIndex)
                {
                    // Last segment
                    if (!(segment is NavigationPropertySegment))
                    {
                        throw new ODataException(Error.Format(SRResources.InvalidLastSegmentInSelectExpandPath, segment.Identifier));
                    }
                }
                else
                {
                    // middle segment
                    if (!(segment is PropertySegment || segment is TypeSegment))
                    {
                        throw new ODataException(Error.Format(SRResources.InvalidSegmentInSelectExpandPath, segment.Identifier));
                    }
                }

                index++;
            }
        }

        public static ODataPathSegment GetFirstNonTypeCastSegment(this ODataSelectPath selectPath, out IList<ODataPathSegment> remainingSegments,
            out IList<ODataPathSegment> leadingSegments)
        {
            return GetFirstNonTypeCastSegment(selectPath,
                //The middle segment should be "TypeSegment" or "PropertySegment".
                m => m is PropertySegment || m is TypeSegment,
                // The last segment could be "NavigationPropertySegment, PropertySegment, OperationSegment, DynamicPathSegment"
                s => s is NavigationPropertySegment || s is PropertySegment || s is OperationSegment || s is DynamicPathSegment,
                out remainingSegments,
                out leadingSegments);
        }

        public static ODataPathSegment GetFirstNonTypeCastSegment(this ODataExpandPath expandPath, out IList<ODataPathSegment> remainingSegments,
            out IList<ODataPathSegment> leadingSegments)
        {
            return GetFirstNonTypeCastSegment(expandPath,
                //The middle segment should be "TypeSegment" or "PropertySegment".
                m => m is PropertySegment || m is TypeSegment,
                // The last segment could be "NavigationPropertySegment"
                s => s is NavigationPropertySegment,
                out remainingSegments,
                out leadingSegments);
        }

        private static ODataPathSegment GetFirstNonTypeCastSegment(ODataPath path, Func<ODataPathSegment, bool> middleSegmentPredict,
            Func<ODataPathSegment, bool> lastSegmentPredict,
            out IList<ODataPathSegment> remainingSegments, // could be null
            out IList<ODataPathSegment> leadingSegments) // could be null
        {
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            remainingSegments = null;
            leadingSegments = null;
            ODataPathSegment firstNonTypeSegment = null;

            int lastIndex = path.Count() - 1;
            int index = 0;
            foreach (var segment in path)
            {
                if (index == lastIndex)
                {
                    // Last segment
                    if (!lastSegmentPredict(segment))
                    {
                        throw new ODataException(Error.Format(SRResources.InvalidLastSegmentInSelectExpandPath, segment.Identifier));
                    }
                }
                else
                {
                    // middle segment
                    if (!middleSegmentPredict(segment))
                    {
                        throw new ODataException(Error.Format(SRResources.InvalidSegmentInSelectExpandPath, segment.Identifier));
                    }
                }

                index++;

                if (firstNonTypeSegment != null)
                {
                    if (remainingSegments == null)
                    {
                        remainingSegments = new List<ODataPathSegment>();
                    }

                    remainingSegments.Add(segment);
                    continue;
                }

                if (segment is TypeSegment)
                {
                    if (leadingSegments == null)
                    {
                        leadingSegments = new List<ODataPathSegment>();
                    }

                    leadingSegments.Add(segment);
                }
                else
                {
                    firstNonTypeSegment = segment;
                }
            }

            return firstNonTypeSegment;
        }

        /// <summary>
        /// For example: $select=NS.SubType1/abc/NS.SubType2/xyz
        /// => firstPropertySegment: "abc"
        /// => remainingSegments:  NS.SubType2/xyz
        /// </summary>
        public static ODataPathSegment ProcessSelectPath(this ODataSelectPath selectPath, out IList<ODataPathSegment> remainingSegments, // could be null
            out IList<ODataPathSegment> leadingSegments)
        {
            ValidatePath(selectPath);

            return ProcessSelectExpandPath(selectPath, out remainingSegments, out leadingSegments);
        }

        public static ODataPathSegment ProcessExpandPath(this ODataExpandPath expandPath, out IList<ODataPathSegment> remainingSegments, // could be null
            out IList<ODataPathSegment> leadingSegments)
        {
            ValidatePath(expandPath);

            return ProcessSelectExpandPath(expandPath, out remainingSegments, out leadingSegments);
        }

        /// <summary>
        /// For example: $select=NS.SubType1/abc/NS.SubType2/xyz
        /// => firstPropertySegment: "abc"
        /// => remainingSegments:  NS.SubType2/xyz
        /// </summary>
        /// <param name="selectExpandPath">The input $select and $expand path.</param>
        /// <param name="remainingSegments">The remaining segments, it could be null if we can't find a Property segment or Navigation property segment.</param>
        /// <param name="leadingSegments">The leading segments before the found Property segment or Navigation property segment.</param>
        /// <returns>The null or <see cref="PropertySegment"/> or <see cref="NavigationPropertySegment"/>.</returns>
        private static ODataPathSegment ProcessSelectExpandPath(ODataPath selectExpandPath, out IList<ODataPathSegment> remainingSegments, // could be null
            out IList<ODataPathSegment> leadingSegments) // could be null
        {
            Contract.Assert(selectExpandPath != null);

            remainingSegments = null;
            leadingSegments = null;
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

                if (leadingSegments == null)
                {
                    leadingSegments = new List<ODataPathSegment>();
                }

                leadingSegments.Add(segment);

                // we ignore other segment types, for example: TypeSegment, OperationSegment, DynamicSegment, etc.
            }

            return firstPropertySegment;
        }
    }
}
