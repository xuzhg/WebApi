// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using Microsoft.AspNet.OData.Builder;
using Microsoft.OData.Edm;

namespace AspNetCore3xEndpointSample.Web.Models
{
    public static class EdmModelBuilder
    {
        private static IEdmModel _edmModel;

        public static IEdmModel GetEdmModel()
        {
            if (_edmModel == null)
            {
                var builder = new ODataConventionModelBuilder();
                builder.EntitySet<Customer>("Customers");
                builder.EntitySet<Order>("Orders");
                _edmModel = builder.GetEdmModel();
            }

            return _edmModel;
        }

        public static IEdmModel GetEdmModel2()
        {
            ODataConventionModelBuilder builder = new ODataConventionModelBuilder();

            var policy = builder.EntitySet<UpdatePolicy>("UpdatePolicies").EntityType;
            policy.Filter("PolicyType");

            policy.Function("Devices").ReturnsFromEntitySet<DeviceAssets>("Devices");
            policy.Function("DeviceDetails").ReturnsCollectionFromEntitySet<DeviceAsset>("DeviceDetails");

            var tenants = builder.EntitySet<Tenant>("Tenants").EntityType;

            var addDevicesAction = builder.EntityType<UpdatePolicy>().Action("AddDevices");
            addDevicesAction.CollectionParameter<string>("DeviceIds");

            return builder.GetEdmModel();
        }
    }
}