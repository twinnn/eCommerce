using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Common.Readers.BaseApiConsumer
{
    public class BaseApiConsumer<T, R> : IBaseApiConsumer<T, R> where T : class
    {
        private readonly ILogger<BaseApiConsumer<T, R>> _logger;
        protected readonly HttpClient _client;
        protected const string MEDIA_TYPE = "application/json";

        public BaseApiConsumer(IHttpClientFactory factory, ILogger<BaseApiConsumer<T, R>> logger)
        {
            _logger = logger;
            _client = factory.CreateClient("NopCommerce");
            SetupHeaders();
        }
        protected virtual void SetupHeaders()
        {
            _client.DefaultRequestHeaders.Clear();

            //Define request data format  
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(MEDIA_TYPE));
        }

        public virtual void AddBearerToken(string token)
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        public virtual void AddCredentials(string username, string password)
        {
            var byteArray = Encoding.ASCII.GetBytes($"{username}:{password}");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
        }


        public async Task<T> GetAsync(string requestUri)
        {
            var response = await _client.GetAsync(requestUri);
            await EnsureSuccess(response);
            return await response.GetObjectFromResponse<T>();
        }

        
        public async Task<T> GetAsync(string requestUri, string id)
        {
            var response = await _client.GetAsync(requestUri);
            await EnsureSuccess(response);
            return await response.GetObjectFromResponse<T>();
        }

        public async Task<T> PostAsync(string requestUri, T content)
        {
            var body = GetBodyContent(GetSerializedObject(content));
            using var message = await _client.PostAsync(requestUri, body);
            await EnsureSuccess(message);

            return await message.GetObjectFromResponse<T>();

        }

        public async Task<R> PostTAsync(string requestUri, T content, bool removeId = false)
        {
            var body = GetBodyContent(GetSerializedObject(content, removeId));
            using var message = await _client.PostAsync(requestUri, body);
            await EnsureSuccess(message);

            return await message.GetObjectFromResponse<R>();
        }

        public async Task<T> PutAsync(string requestUri, T content)
        {
            var body = GetBodyContent(GetSerializedObject(content));
            using var message = await _client.PutAsync(requestUri, body);
            await EnsureSuccess(message);

            return await message.GetObjectFromResponse<T>();
        }

        public async Task<R> PutTAsync(string requestUri, T content)
        {
            var body = GetBodyContent(GetSerializedObject(content));
            using var message = await _client.PutAsync(requestUri, body);
            await EnsureSuccess(message);

            return await message.GetObjectFromResponse<R>();
        }

        public async Task<T> DeleteAsync(string requestUri)
        {
            using var message = await _client.DeleteAsync(requestUri);
            await EnsureSuccess(message);

            return await message.GetObjectFromResponse<T>();
        }

        protected virtual string GetSerializedObject(object obj, bool removeId = false)
        {
            var settings = new JsonSerializerSettings { ContractResolver = new JsonIgnoreResolver(new List<string> { "id"}) };

            var serializedJson = (removeId) ? JsonConvert.SerializeObject(obj, settings) : JsonConvert.SerializeObject(obj);

            return serializedJson;
        }

        protected virtual StringContent GetBodyContent(string serializedJson)
        {
            var bodyContent = new StringContent
                (serializedJson, Encoding.UTF8, "application/json");

            return bodyContent;
        }

        private async Task EnsureSuccess(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
                return;

            _logger.LogError("Error: {0}", await response.Content.ReadAsStringAsync());
            throw new HttpRequestException($"Request failed with status code: {response.StatusCode}");
        }

    }
}
