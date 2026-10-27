using Common.Readers.BaseApiConsumer;
using Nop.Contract.Configuration;
using static Nop.Contract.Poco.TokenResponse;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Nop.ApiConsumer
{
    public interface INopBaseApiConsumer<T, R> : IBaseApiConsumer<T, R> where T : class
    {
        Task InitAsync();
    }

    public class NopBaseApiConsumer<T, R> : BaseApiConsumer<T, R>, INopBaseApiConsumer<T, R> where T : class
    {
        private readonly NopCommerceSettings _settings;
        protected bool Initialized { get; set; }
        protected string Token { get; set; }
        public NopBaseApiConsumer(NopCommerceSettings settings, IHttpClientFactory factory, ILogger<NopBaseApiConsumer<T, R>> logger) : base(factory, logger)
        {
            _settings = settings;
            Initialized = false;
            Token = string.Empty;
        }

        public async Task InitAsync()
        {
            if (!Initialized) 
            {
                var request = @"{""guest"": false, ""username"":""" + _settings.Username + @""",""password"":""" + _settings.Password + @""",""remember_me"": true}";

                var authRequest = new HttpRequestMessage(HttpMethod.Post, "/token");
                authRequest.Content = new StringContent(request, Encoding.UTF8, "application/json");
                var authResponse = await _client.SendAsync(authRequest);

                if (!authResponse.IsSuccessStatusCode)
                {
                    throw new Exception("Couldn't create token for nopCommerce. Http response: " + authResponse);
                }

                Response? deserializedResult;

                try
                {
                    deserializedResult = JsonSerializer.Deserialize<Response>(await authResponse.Content.ReadAsStreamAsync());
                    Token = deserializedResult!.access_token;

                }
                catch (Exception e)
                {
                    throw new Exception("Couldn't deserialize token response", e);
                }

                AddBearerToken(Token);

                Initialized = true;
            }
        }            
    }
}
