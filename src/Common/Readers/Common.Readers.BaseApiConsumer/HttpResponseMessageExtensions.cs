using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Common.Readers.BaseApiConsumer
{
    public static class HttpResponseMessageExtensions
    {
        public static async Task<T> GetObjectFromResponse<T>(this HttpResponseMessage message)
        {
            var content = await message.Content.ReadAsStringAsync();
            return string.IsNullOrEmpty(content)
                ? default(T)
                : JsonConvert.DeserializeObject<T>(content);
        
        }

        public static async Task<IEnumerable<T>> GetEnumerableOfObjectFromResponse<T>(this HttpResponseMessage message)
        {
            var content = await message.Content.ReadAsStringAsync();
            return string.IsNullOrEmpty(content)
                ? default(List<T>)
                : JsonConvert.DeserializeObject<List<T>>(content);
        }
    }
}
