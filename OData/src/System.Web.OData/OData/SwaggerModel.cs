// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Web.Http;
using Microsoft.OData.Edm;

namespace System.Web.OData
{
    /// <summary>
    /// Wrapper of EdmModel.
    /// </summary>
    public class SwaggerModel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SwaggerModel" /> class.
        /// </summary>
        /// <param name="edmModel">The Edm model.</param>
        public SwaggerModel(IEdmModel edmModel)
        {
            if (edmModel == null)
            {
                throw Error.ArgumentNull("edmModel");
            }

            EdmModel = edmModel;
        }

        /// <summary>
        /// Gets the Edm model embedded in.
        /// </summary>
        public IEdmModel EdmModel { get; private set; }
    }
}
