// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

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
        // we only support paths of type 'cast/structuralOrNavPropertyOrAction' and 'structuralOrNavPropertyOrAction'.
        // It supports the path like
        // "{cast|StructuralProperty}/|{cast|StructuralProperty}|/{NavigationProperty|StructuralProperty|OperationSegment|DynamicPathSegment|
        internal static void ValidatePath(this ODataSelectPath selectPath)
        {
            Contract.Assert(selectPath != null);

            int segmentCount = selectPath.Count();

            ODataPathSegment lastSegment = selectPath.LastSegment;
            if (!(lastSegment is NavigationPropertySegment
                || lastSegment is PropertySegment
                || lastSegment is OperationSegment
                || lastSegment is DynamicPathSegment))
            {
                throw new ODataException(Error.Format(SRResources.InvalidLastSegmentInSelectExpandPath, lastSegment.Identifier));
            }

            for (int i = 0; i < segmentCount - 1; i++)
            {
                ODataPathSegment segment = selectPath.ElementAt(i);
                if (!(segment is PropertySegment
                    || segment is TypeSegment))
                {
                    throw new ODataException(Error.Format(SRResources.InvalidSegmentInSelectExpandPath, segment.Identifier));
                }
            }
        }

        // We support paths as:
        // 'cast/structuralOrNavPropertyOrAction',
        // 'ComplexObject/cast/StructuralOrNavPropertyOnAction',
        // 'ComplexObject/structuralOrNavPropertyOnAction' 
        // 'structuralOrNavPropertyOrAction'.
        internal static void ValidatePath(this ODataExpandPath expandPath)
        {
            Contract.Assert(expandPath != null);

            ODataPathSegment lastSegment = expandPath.LastSegment;
            foreach (ODataPathSegment segment in expandPath)
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
        /// For example: $select=NS.SubType1/abc/NS.SubType2/xyz
        /// => firstPropertySegment: "abc"
        /// => remainingSegments:  NS.SubType2/xyz
        /// </summary>
        public static ODataPathSegment ProcessSelectPath(this ODataSelectPath selectPath, out IList<ODataPathSegment> remainingSegments) // could be null
        {
            return ProcessSelectExpandPath(selectPath, out remainingSegments);
        }

        public static ODataPathSegment ProcessExpandPath(this ODataExpandPath expandPath, out IList<ODataPathSegment> remainingSegments) // could be null
        {
            return ProcessSelectExpandPath(expandPath, out remainingSegments);
        }

        /// <summary>
        /// For example: $select=NS.SubType1/abc/NS.SubType2/xyz
        /// => firstPropertySegment: "abc"
        /// => remainingSegments:  NS.SubType2/xyz
        /// </summary>
        private static ODataPathSegment ProcessSelectExpandPath(ODataPath selectExpandPath, out IList<ODataPathSegment> remainingSegments) // could be null
        {
            Contract.Assert(selectExpandPath != null);

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
    }
}
