using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Logging;
using Pohoda.Contract;
using Pohoda.Contract.Configuration;

namespace Pohoda.Server.MServer
{
    public class MServer : IStorageSystem, IDisposable
    {
        private readonly HttpClient _client;
        private readonly ILogger<MServer> _logger;
        private readonly IMServerConfiguration _mServerConfig;

        public MServer(ILogger<MServer> logger, HttpClient httpClient)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _mServerConfig = new MServerConfiguration
            {
                HttpClientName = "MServerHttpClient",
                PathToServer = "\"C:\\Program Files (x86)\\STORMWARE\\POHODA SK E1\"",
                RetryDelay = 2000,
                ServerName = "test"
            };

            _logger = logger;
            _client = httpClient;
        }

        public async Task<string> SendRequest(string body)
        {
            var response = await _client.PostAsync("/xml", new ByteArrayContent(Encoding.UTF8.GetBytes(body)));
            var bytes = await response.Content.ReadAsByteArrayAsync();
            var windows1250 = Encoding.GetEncoding("windows-1250");
            var utf8 = Encoding.UTF8;
            var utf8Bytes = Encoding.Convert(windows1250, utf8, bytes);
            return utf8.GetString(utf8Bytes);
        }

        public async Task StartServer()
        {
            var retries = 5;
            do
            {
                if (await IsConnectionAvailable())
                    break;

                var mServerStartCommand =
                    $"cd \"{_mServerConfig.PathToServer}\" & pohoda.exe /http start {_mServerConfig.ServerName}";

                ExecuteCliCommand(mServerStartCommand);

                await Task.Delay(retries * 1000);

                retries--;
            } while (retries == 0);

        }

        public async Task<bool> IsConnectionAvailable()
        {
            var responseCode = HttpStatusCode.BadRequest;
            try
            {
                using var response = await _client.GetAsync("/status");
                responseCode = response.StatusCode;
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    Console.WriteLine("Error: Couldn't connect to mServer \nResponseCode" + response.StatusCode);
                    return false;
                }

                var xmlDoc = new XmlDocument();
                var responseString = await response.Content.ReadAsStringAsync();
                xmlDoc.LoadXml(responseString);
                var actualServerName = xmlDoc.GetElementsByTagName("name")[0]?.InnerText;

                if (actualServerName != null && !actualServerName.Equals(_mServerConfig.ServerName)) return false;

                Console.WriteLine("mServer is running on " + _mServerConfig.ServerName);
                return true;
            }
            catch (HttpRequestException)
            {
                _logger.LogError("Couldn't connect to mServer.");
                return false;
            }
            catch (NullReferenceException)
            {
                _logger.LogError("MServer doesn't exist.");
                return false;
            }
        }

        public async Task StopServer()
        {
            _client.Dispose();

            if (await IsConnectionAvailable())
            {
                var mServerStopCommand =
                    $"cd \"{_mServerConfig.PathToServer}\" & pohoda.exe /http stop {_mServerConfig.ServerName}";
                ExecuteCliCommand(mServerStopCommand);
            }
        }

        private static void ExecuteCliCommand(string command)
        {
            var cmd = new Process();
            cmd.StartInfo.FileName = "cmd.exe";
            cmd.StartInfo.RedirectStandardInput = true;
            cmd.StartInfo.RedirectStandardOutput = false;
            cmd.StartInfo.CreateNoWindow = true;
            cmd.StartInfo.UseShellExecute = false;
            cmd.Start();

            cmd.StandardInput.WriteLine(command);
            cmd.StandardInput.Flush();
            cmd.StandardInput.Close();
            cmd.WaitForExit();
        }

        public void Dispose()
        {
            Task.Run(() => StopServer());
        }
    }
}