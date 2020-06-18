using Microsoft.AspNet.OData.Builder;
using Microsoft.OData.Edm;

namespace ODataRoutingSample.Models
{
    public static class EdmModelBuilder
    {
   //     private static IEdmModel _edmModel;

        public static IEdmModel GetEdmModel()
        {
            var builder = new ODataConventionModelBuilder();
            builder.EntitySet<Product>("Products");

            builder.Action("ResetData");

            return builder.GetEdmModel();
        }

        public static IEdmModel GetEdmModelV1()
        {
            var builder = new ODataConventionModelBuilder();
            builder.EntitySet<Customer>("Customers");

            var function = builder.Function("RateByOrder");
            function.Parameter<int>("order");
            function.Returns<int>();

            return builder.GetEdmModel();
        }

        public static IEdmModel GetEdmModelV2()
        {
            var builder = new ODataConventionModelBuilder();
            var order = builder.EntitySet<Order>("Orders");

            builder.Singleton<Order>("MeOrder");

            var functionWithComplexTypeParameter = builder.EntityType<Order>().Function("CanMoveToAddress").Returns<bool>();
            functionWithComplexTypeParameter.Parameter<Address>("address");

            // Function 1
            var function = builder.Function("RateByOrder");
            function.Parameter<int>("order");
            function.Returns<int>();

            // Function 2
            function = builder.Function("CalcByOrder");
            function.Parameter<string>("name");
            function.Parameter<int>("order");
            function.Returns<int>();

            return builder.GetEdmModel();
        }
    }
}
