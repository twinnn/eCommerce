using System.Threading.Tasks;

public interface IBaseApiConsumer<T, R> where T : class
{
    Task<T> GetAsync(string requestUri);
    Task<T> GetAsync(string requestUri, string id);
    Task<T> PostAsync(string requestUri, T content);
    Task<R> PostTAsync(string requestUri, T content, bool removeId = false);

    Task<T> PutAsync(string requestUri, T content);
    Task<R> PutTAsync(string requestUri, T content);
    Task<T> DeleteAsync(string requestUri);
}
