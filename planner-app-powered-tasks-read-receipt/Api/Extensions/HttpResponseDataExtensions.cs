using Microsoft.AspNetCore.Http;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace i365.ReadReceipt.Tasks.Extensions
{
    public static class HttpResponseDataExtensions
    {
        public async static Task<HttpResponseMessage> CreateOkJsonResponse<T>(this HttpRequest req, T data)
        {
            return await req.CreateStatusWithJsonResponse(HttpStatusCode.OK, data);
        }

        public async static Task<HttpResponseMessage> CreateStatusWithStringResponse(this HttpRequest req, HttpStatusCode statusCode, string message)
        {
            var response = new HttpResponseMessage(statusCode);
            response.Content = new StringContent(message, System.Text.Encoding.UTF8);
            return response;
        }

        private async static Task<HttpResponseMessage> CreateStatusWithJsonResponse<T>(this HttpRequest req, HttpStatusCode statusCode,
            T data)
        {
            var serialisationSettings = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            };
            var response = new HttpResponseMessage(statusCode);
            response.Content = JsonContent.Create(data, null, serialisationSettings);
            return response;
        }

    }
}
