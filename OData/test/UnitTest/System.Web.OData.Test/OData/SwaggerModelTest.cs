// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using Microsoft.TestCommon;

namespace System.Web.OData
{
    public class SwaggerModelTest
    {
        [Fact]
        public void Ctor_ThrowsArgumentNull_EdmModel()
        {
            Assert.ThrowsArgumentNull(() => new SwaggerModel(edmModel: null), "edmModel");
        }
    }
}
