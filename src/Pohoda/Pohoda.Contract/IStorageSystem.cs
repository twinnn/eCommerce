using System.Threading.Tasks;

namespace Pohoda.Contract
{
    public interface IStorageSystem
    {
        public Task<string> SendRequest(string body);
        public Task StartServer();

        public Task StopServer();
    }
}