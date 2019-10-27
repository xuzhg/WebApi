// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNet.OData.Extensions;
using Microsoft.Test.E2E.AspNet.OData.Common.Execution;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Test.E2E.AspNet.OData.NavigationPropertyOnComplexType
{
    public class SelectImprovementOnComplexTypeTests : WebHostTestBase
    {
        private const string PeopleBaseUrl = "{0}/odata/People";

        public SelectImprovementOnComplexTypeTests(WebHostTestFixture fixture)
            : base(fixture)
        {
        }

        protected override void UpdateConfiguration(WebRouteConfiguration configuration)
        {
            configuration.AddControllers(typeof(PeopleController));
            configuration.JsonReferenceLoopHandling =
                Newtonsoft.Json.ReferenceLoopHandling.Ignore;
            configuration.MaxTop(2).Expand().Select().OrderBy().Filter();
            configuration.MapODataServiceRoute("odata", "odata", ModelGenerator.GetConventionalEdmModel());
        }

        [Theory]
        [InlineData("HomeLocation/Street,HomeLocation/TaxNo")]
        [InlineData("HomeLocation($select=Street,TaxNo)")]
        public void QueryEntityWithSelectOnSubPrimitivePropertyOfComplexTypeProperty(string select)
        {
            // Arrange
            string requestUri = string.Format(PeopleBaseUrl, BaseAddress) + "(1)?$select=" + select;

            string expects;
            if (select.Contains("$select="))
            {
                expects = "{\"@odata.context\":\"BASE_ADDRESS/odata/$metadata#People(HomeLocation)/$entity\"," +
                    "\"HomeLocation\":{\"Street\":\"110th\",\"TaxNo\":19}}";
            }
            else
            {
                expects = "{\"@odata.context\":\"BASE_ADDRESS/odata/$metadata#People(HomeLocation/Street,HomeLocation/TaxNo)/$entity\"," +
                    "\"HomeLocation\":{\"Street\":\"110th\",\"TaxNo\":19}}";
            }

            string equals = expects.Replace("BASE_ADDRESS", BaseAddress);

            // Act & Assert
            ExecuteAndVerifyQueryRequest(requestUri, contains: null, equals: equals);
        }

        [Theory]
        [InlineData("HomeLocation/Emails")]
        [InlineData("HomeLocation($select=Emails)")]
        public void QueryEntityWithSelectOnSubCollectionPrimitivePropertyOfComplexTypeProperty(string select)
        {
            // Arrange
            string requestUri = string.Format(PeopleBaseUrl, BaseAddress) + "(1)?$select=" + select;

            string expects;
            if (select.Contains("$select="))
            {
                expects = "{\"@odata.context\":\"BASE_ADDRESS/odata/$metadata#People(HomeLocation)/$entity\"," +
                    "\"HomeLocation\":{\"Emails\":[\"E1\",\"E3\",\"E2\"]}}";
            }
            else
            {
                expects = "{\"@odata.context\":\"BASE_ADDRESS/odata/$metadata#People(HomeLocation/Emails)/$entity\"," +
                    "\"HomeLocation\":{\"Emails\":[\"E1\",\"E3\",\"E2\"]}}";
            }

            string equals = expects.Replace("BASE_ADDRESS", BaseAddress);

            // Act & Assert
            ExecuteAndVerifyQueryRequest(requestUri, contains: null, equals: equals);
        }

        [Theory]
        [InlineData("Taxes", "\"Taxes\":[7,5,9]")]
        [InlineData("Taxes($filter=$it eq 5)", "\"Taxes\":[5]")]
        [InlineData("Taxes($filter=$it le 8)", "\"Taxes\":[7,5]")]
        public void QueryEntityWithSelectOnCollectionPrimitivePropertyWithNestedFilter(string select, string value)
        {
            // Arrange
            string requestUri = string.Format(PeopleBaseUrl, BaseAddress) + "(1)?$select=" + select;
            string equals = string.Format("{{\"@odata.context\":\"{0}/odata/$metadata#People(Taxes)/$entity\",{1}}}", BaseAddress, value);

            // Act & Assert
            ExecuteAndVerifyQueryRequest(requestUri, contains: null, equals: equals);
        }

        [Theory]
        [InlineData("Taxes($orderby=$it)", "\"Taxes\":[5,7,9]")]
        [InlineData("Taxes($orderby =$it desc)", "\"Taxes\":[9,7,5]")]
        public void QueryEntityWithSelectOnCollectionPrimitivePropertyWithNestedOrderby(string select, string value)
        {
            // Arrange
            string requestUri = string.Format(PeopleBaseUrl, BaseAddress) + "(1)?$select=" + select;
            string equals = string.Format("{{\"@odata.context\":\"{0}/odata/$metadata#People(Taxes)/$entity\",{1}}}", BaseAddress, value);

            // Act & Assert
            ExecuteAndVerifyQueryRequest(requestUri, contains: null, equals: equals);
        }

        [Theory]
        [InlineData("HomeLocation/Emails($filter=$it eq 'E3')")]
        [InlineData("HomeLocation($select=Emails($filter=$it eq 'E3'))")]
        public void QueryEntityWithSelectOnSubCollectionPrimitivePropertyOfComplexTypePropertyWithNestedFilter(string select)
        {
            // Arrange
            string requestUri = string.Format(PeopleBaseUrl, BaseAddress) + "(1)?$select=" + select;

            string expects;
            if (select.Contains("$select="))
            {
                expects = "{\"@odata.context\":\"BASE_ADDRESS/odata/$metadata#People(HomeLocation)/$entity\"," +
                    "\"HomeLocation\":{\"Emails\":[\"E3\"]}}";
            }
            else
            {
                expects = "{\"@odata.context\":\"BASE_ADDRESS/odata/$metadata#People(HomeLocation/Emails)/$entity\"," +
                    "\"HomeLocation\":{\"Emails\":[\"E3\"]}}";
            }

            string equals = expects.Replace("BASE_ADDRESS", BaseAddress);

            // Act & Assert
            ExecuteAndVerifyQueryRequest(requestUri, contains: null, equals: equals);
        }

        [Theory]
        [InlineData("HomeLocation/Emails($filter=$it eq 'E3')")]
        [InlineData("HomeLocation($select=Emails($filter=$it eq 'E3'))")]
        public void QueryEntityWithSelectOnSubCollectionPrimitivePropertyOfComplexTypePropertyWithNestedFilter2(string select)
        {
            // Arrange
            string requestUri = string.Format(PeopleBaseUrl, BaseAddress) + "(1)?$select=" + select;

            string expects;
            if (select.Contains("$select="))
            {
                expects = "{\"@odata.context\":\"BASE_ADDRESS/odata/$metadata#People(HomeLocation)/$entity\"," +
                    "\"HomeLocation\":{\"Emails\":[\"E3\"]}}";
            }
            else
            {
                expects = "{\"@odata.context\":\"BASE_ADDRESS/odata/$metadata#People(HomeLocation/Emails)/$entity\"," +
                    "\"HomeLocation\":{\"Emails\":[\"E3\"]}}";
            }

            string equals = expects.Replace("BASE_ADDRESS", BaseAddress);

            // Act & Assert
            ExecuteAndVerifyQueryRequest(requestUri, contains: null, equals: equals);
        }

        [Fact]
        public void QueryNavigationPropertyOnComplexTypeProperty()
        {
            // Arrange : GET ~/People(1)/HomeLocation/ZipCode
            string requestUri = string.Format(PeopleBaseUrl, BaseAddress) + "(1)/HomeLocation/ZipCode";

            string expects = "{\"@odata.context\":\"BASE_ADDRESS/odata/$metadata#ZipCodes/$entity\"," +
                "\"Zip\":98052,\"City\":\"Redmond\",\"State\":\"Washington\"}";

            string equals = expects.Replace("BASE_ADDRESS", BaseAddress);

            // Act & Assert
            ExecuteAndVerifyQueryRequest(requestUri, contains: null, equals: equals);
        }

        [Fact]
        public void QueryComplexTypePropertyWithSelectAndExpand()
        {
            // Arrange
            string requestUri = string.Format(PeopleBaseUrl, BaseAddress) + "(1)/HomeLocation?$select=Street&$expand=ZipCode";

            string expects = "{\"@odata.context\":\"BASE_ADDRESS/odata/$metadata#People(1)/HomeLocation(Street,ZipCode())\"," +
                "\"Street\":\"110th\"," +
                "\"ZipCode\":{\"Zip\":98052,\"City\":\"Redmond\",\"State\":\"Washington\"}}";

            string equals = expects.Replace("BASE_ADDRESS", BaseAddress);

            // Act & Assert
            ExecuteAndVerifyQueryRequest(requestUri, contains: null, equals: equals);
        }

        [Fact]
        public void QueryCollectionComplexTypePropertyWithSelectAndExpand()
        {
            // Arrange
            string requestUri = string.Format(PeopleBaseUrl, BaseAddress) + "(1)/RepoLocations?$select=Street&$expand=ZipCode";

            // Act
            string result = ExecuteAndVerifyQueryRequest(requestUri);

            // Assert
            JObject jObj = JObject.Parse(result);
            Assert.Equal("BASE_ADDRESS/odata/$metadata#People(1)/RepoLocations(Street,ZipCode())".Replace("BASE_ADDRESS", BaseAddress),
                jObj["@odata.context"]);

            var array = jObj["value"] as JArray;
            Assert.Equal(3, array.Count);

            for (int i = 0; i < 3; i++)
            {
                JObject item = array[i] as JObject;
                Assert.Equal(new[] { "Street", "ZipCode" },
                    item.Properties().Where(p => !p.Name.StartsWith("@")).Select(p => p.Name));
                string street = "1" + (1 + i) + "0th"; // 110th, 120th, 130th
                Assert.Equal(street, array[i]["Street"].ToString());
            }
        }

        [Fact]
        public void QueryEntityWithExpandOnNavigationPropertyOnComplexTypeProperty()
        {
            // Arrange
            string requestUri = string.Format(PeopleBaseUrl, BaseAddress) + "(1)?$expand=HomeLocation/ZipCode";

            // Includes all properties of People(1) and expand the ZipCode on HomeLocation
            string contains = ",\"Id\":1," +
                "\"Name\":\"Kate\"," +
                "\"Age\":5," +
                "\"Taxes\":[7,5,9]," +
                "\"HomeLocation\":{\"Street\":\"110th\",\"Emails\":[\"E1\",\"E3\",\"E2\"],\"ZipCode\":{\"Zip\":98052,\"City\":\"Redmond\",\"State\":\"Washington\"}}," +
                "\"RepoLocations\":[{\"Stre";

            // Act & Assert
            ExecuteAndVerifyQueryRequest(requestUri, contains);
        }

        [Fact]
        public void QueryEntityWithExpandOnNavigationPropertyOnComplexTypePropertyAndSelectOnOtherProperty()
        {
            // Arrange
            string requestUri = string.Format(PeopleBaseUrl, BaseAddress) + "(1)?$expand=HomeLocation/ZipCode&$select=Name";

            // only includes Name, HomeLocation and expand ZipCode on HomeLocation
            // Be noted: The output should not include "Primitive properties" in "HomeLocation".
            // The issue is form ODL, it includes an "PathSelectItem (HomeLocation)" in the SelectExpandClause.
            // See detail at: https://github.com/OData/odata.net/issues/1574
            string contains = "\"Name\":\"Kate\"," +
                "\"HomeLocation\":{\"Street\":\"110th\",\"Emails\":[\"E1\",\"E3\",\"E2\"],\"ZipCode\":{\"Zip\":98052,\"City\":\"Redmond\",\"State\":\"Washington\"}}";

            // Act & Assert
            ExecuteAndVerifyQueryRequest(requestUri, contains);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        public void QueryEntityWithExpandOnMultipleNavigationPropertiesOnComplexTypeProperty(int key)
        {
            // Arrange
            string requestUri = string.Format(PeopleBaseUrl, BaseAddress) + "(" + key + ")?$expand=HomeLocation/ZipCode,PreciseLocation/ZipCode&$select=Name";

            // only includes Name, HomeLocation, PreciseLocation and expand ZipCode on HomeLocation
            string contains;
            if (key == 1)
            {
                contains = "\"Name\":\"Kate\"," +
                    "\"HomeLocation\":{\"Street\":\"110th\",\"Emails\":[\"E1\",\"E3\",\"E2\"],\"ZipCode\":{\"Zip\":98052,\"City\":\"Redmond\",\"State\":\"Washington\"}}," +
                    "\"PreciseLocation\":null";
            }
            else
            {
                contains = "\"Name\":\"Carlos\"," +
                    "\"HomeLocation\":null," +
                    "\"PreciseLocation\":{\"Street\":\"50th\",\"Emails\":[],\"Latitude\":\"12\",\"Longitude\":\"22\",\"ZipCode\":{\"Zip\":35816,\"City\":\"Huntsville\",\"State\":\"Alabama\"}}}";
            }

            // Act & Assert
            ExecuteAndVerifyQueryRequest(requestUri, contains);
        }

        [Fact]
        public void QueryEntityWithExpandOnNavigationPropertiesOnDeepComplexTypeProperty()
        {
            // Arrange
            string requestUri = string.Format(PeopleBaseUrl, BaseAddress) + "(1)?$expand=Order/BillLocation/ZipCode&$select=Order";

            string contains =
              "\"Order\":{" +
                "\"BillLocation\":{" +
                  "\"Street\":\"110th\"," +
                  "\"Emails\":[\"E1\",\"E3\",\"E2\"]," +
                  "\"ZipCode\":{" +
                    "\"Zip\":98052," +
                    "\"City\":\"Redmond\"," +
                    "\"State\":\"Washington\"" +
                  "}" +
                "}," +
                "\"SubInfo\":null" +
              "}";

            // Act & Assert
            ExecuteAndVerifyQueryRequest(requestUri, contains);
        }

        [Fact]
        public void SerializingExpandOnNavigationPropertyOnComplexTypePropertyWithSelectOnComplexProperty()
        {
            // Arrange
            string requestUri = string.Format(PeopleBaseUrl, BaseAddress) + "(1)?$expand=Location/ZipCode&$select=Location";

            string expects = "{\"@odata.context\":\"BASE_ADDRESS/odata/$metadata#People(Location,Location/ZipCode())/$entity\"," +
                "\"Location\":{\"Street\":\"110th\",\"ZipCode\":{\"Zip\":98030,\"City\":\"Kent\",\"State\":\"Washington\"}}}";

            string equals = expects.Replace("BASE_ADDRESS", BaseAddress);

            // Act & Assert
            ExecuteAndVerifyQueryRequest(requestUri, contains: null, equals: equals);
        }

        private static string ExecuteAndVerifyQueryRequest(string requestUri, string contains = null, string equals = null)
        {
            // Arrange
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/json"));
            HttpClient client = new HttpClient();

            // Act
            HttpResponseMessage response = client.SendAsync(request).Result;

            // Assert
            string result = response.Content.ReadAsStringAsync().Result;

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            if (contains != null)
            {
                Assert.Contains(contains, result);
            }

            if (equals != null)
            {
                Assert.Equal(equals, result);
            }

            return result;
        }



        [Fact]
        public async Task DeserializingNavigationPropertyOnComplexType()
        {
            string url =  PeopleBaseUrl + "(1)/Location/ZipCode/$ref";
            string payload = "{\"Zip\":98038,\"City\":\"Redmond\",\"State\":\"Washington\"}";
            HttpContent content = new StringContent(payload, Encoding.UTF8, mediaType: "application/json");
            string queryUrl =
                string.Format(
                    url,
                    BaseAddress);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, queryUrl);
            request.Content = content;
            request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/json;odata.metadata=minimal"));
            HttpClient client = new HttpClient();

            HttpResponseMessage response = await client.SendAsync(request);
            string result = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(payload.TrimStart('{'), result);
        }

        [Theory]
        [InlineData("Location?$expand=Microsoft.Test.E2E.AspNet.OData.NavigationPropertyOnComplexType.Address/ZipCode", "\"Street\":\"110th\",\"Latitude\":\"12.211\",\"Longitude\":\"231.131\",\"ZipCode\":{\"Zip\":98030,\"City\":\"Kent\",\"State\":\"Washington\"}}")]
        [InlineData("Location/Microsoft.Test.E2E.AspNet.OData.NavigationPropertyOnComplexType.Address?$expand=ZipCode", "\"Street\":\"110th\",\"Latitude\":\"12.211\",\"Longitude\":\"231.131\",\"ZipCode\":{\"Zip\":98030,\"City\":\"Kent\",\"State\":\"Washington\"}}")]
        [InlineData("?$expand=Location/Microsoft.Test.E2E.AspNet.OData.NavigationPropertyOnComplexType.Address/ZipCode", "\"@odata.type\":\"#Microsoft.Test.E2E.AspNet.OData.NavigationPropertyOnComplexType.GeoLocation\",\"ZipCode\":{\"Zip\":98030,\"City\":\"Kent\",\"State\":\"Washington\"}}")]
        [InlineData("?$expand=Location/ZipCode", "\"@odata.type\":\"#Microsoft.Test.E2E.AspNet.OData.NavigationPropertyOnComplexType.GeoLocation\",\"ZipCode\":{\"Zip\":98030,\"City\":\"Kent\",\"State\":\"Washington\"}}")]
        public async Task ExpandOnDerivedType(string query, string expected)
        {
            string url = PeopleBaseUrl + "(2)/" + query;
            string queryUrl =
                string.Format(
                    url,
                    BaseAddress);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, queryUrl);
            request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/json;odata.metadata=minimal"));
            HttpClient client = new HttpClient();

            HttpResponseMessage response = await client.SendAsync(request);
            string result = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(expected, result);
        }

        [Theory]
        [InlineData("?$expand=PreciseLocation/Area", "\"Id\":3,\"FirstName\":\"Carlos\",\"LastName\":\"Park\",\"Age\":7,\"Location\":{\"Street\":\"110th\"},\"Home\":{\"Street\":\"110th\"},\"Order\":{\"Zip\":{\"Street\":\"110th\"},\"Order\":null},\"PreciseLocation\":{\"Area\":{\"Zip\":98004,\"City\":\"Bellevue\",\"State\":\"Washington\"}}}")]
        [InlineData("?$expand=PreciseLocation/ZipCode", "\"Id\":3,\"FirstName\":\"Carlos\",\"LastName\":\"Park\",\"Age\":7,\"Location\":{\"Street\":\"110th\"},\"Home\":{\"Street\":\"110th\"},\"Order\":{\"Zip\":{\"Street\":\"110th\"},\"Order\":null},\"PreciseLocation\":{\"ZipCode\":{\"Zip\":98030,\"City\":\"Kent\",\"State\":\"Washington\"}}}")]
        public async Task ExpandOnDeclaredAndInheritedProperties(string queryOption, string expected)
        {
            string resourcePath = PeopleBaseUrl + "(3)";
            string queryUrl =
                string.Format(
                    resourcePath + queryOption,
                    BaseAddress);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, queryUrl);
            request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/json"));
            HttpClient client = new HttpClient();

            HttpResponseMessage response = await client.SendAsync(request);
            string result = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(expected, result);
        }

        [Theory]
        [InlineData("?$expand=Order/Zip/ZipCode", "\"Order\":{\"Zip\":{\"ZipCode\":{\"Zip\":98052,\"City\":\"Redmond\",\"State\":\"Washington\"}}}}")]
        [InlineData("?$expand=Order/Order/Zip/ZipCode", "\"Order\":{\"Order\":{\"Zip\":{\"ZipCode\":{\"Zip\":98030,\"City\":\"Kent\",\"State\":\"Washington\"}}}}}")]
        public async Task RecursiveExpandOnOrders(string queryOption, string expected)
        {
            string resourcePath = PeopleBaseUrl + "(4)";
            string queryUrl =
                string.Format(
                    resourcePath + queryOption,
                    BaseAddress);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, queryUrl);
            request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/json"));
            HttpClient client = new HttpClient();

            HttpResponseMessage response = await client.SendAsync(request);
            string result = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(expected, result);
        }

        [Theory]
        [InlineData("?$select=key", "key\":{\"@odata.type\":\"#Microsoft.Test.E2E.AspNet.OData.NavigationPropertyOnComplexType.ZipCode\",\"Zip\":98030,\"City\":\"Kent\",\"State\":\"Washington\"")]
        public async Task SelectOnOpenType(string queryOption, string expected)
        {
            string resourcePath = PeopleBaseUrl + "(5)/Order";
            string queryUrl =
                string.Format(
                    resourcePath + queryOption,
                    BaseAddress);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, queryUrl);
            request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/json"));
            HttpClient client = new HttpClient();

            HttpResponseMessage response = await client.SendAsync(request);
            string result = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(expected, result);
        }
    }
}

