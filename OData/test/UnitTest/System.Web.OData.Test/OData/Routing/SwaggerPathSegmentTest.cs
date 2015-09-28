// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.TestCommon;

namespace System.Web.OData.Routing
{
    public class SwaggerPathSegmentTest
    {
        [Fact]
        public void Property_SegmentKind_IsSwagger()
        {
            // Arrange
            SwaggerPathSegment segment = new SwaggerPathSegment();

            // Act & Assert
            Assert.Equal(ODataSegmentKinds.Swagger, segment.SegmentKind);
        }

        [Fact]
        public void GetEdmType_ReturnsNull()
        {
            // Arrange
            SwaggerPathSegment segment = new SwaggerPathSegment();

            // Act & Assert
            Assert.Null(segment.GetEdmType(previousEdmType: null));
        }

        [Fact]
        public void GetNavigationSource_ReturnsNull()
        {
            // Arrange
            SwaggerPathSegment segment = new SwaggerPathSegment();

            // Act & Assert
            Assert.Null(segment.GetNavigationSource(previousNavigationSource: null));
        }

        [Fact]
        public void ToString_Returns_SwaggerName()
        {
            // Arrange
            SwaggerPathSegment segment = new SwaggerPathSegment();

            // Act & Assert
            Assert.Equal("$swagger", segment.ToString());
        }

        [Fact]
        public void TryMatch_ReturnsTrue_IfMatchingSwagger()
        {
            // Arrange
            ODataPathSegmentTemplate template = new SwaggerPathSegment();
            SwaggerPathSegment segment = new SwaggerPathSegment();

            // Act
            Dictionary<string, object> values = new Dictionary<string, object>();
            bool result = template.TryMatch(segment, values);

            // Assert
            Assert.True(result);
            Assert.Empty(values);
        }
    }
}
