using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Pohoda.MServer;

namespace Pohoda.MServer.Tests;

public class MServerLauncherTests
{
    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(respond(request));
    }

    private static MServerConfiguration Config(string? pohodaExePath) => new()
    {
        BaseUrl = "http://127.0.0.1",
        Port = 444,
        ConfigName = "GoNoow",
        PohodaExePath = pohodaExePath,
        Username = "user",
        Password = "pass",
        Ico = "53870441",
        DefaultStorageCode = "GoNoow",
        DefaultTypePriceCode = "0"
    };

    private static MServerClient AlwaysUnavailableClient()
    {
        var httpClient = new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)))
        {
            BaseAddress = new Uri("http://127.0.0.1:444/")
        };
        return new MServerClient(httpClient, NullLogger<MServerClient>.Instance);
    }

    private static MServerClient AlwaysAvailableClient()
    {
        var httpClient = new HttpClient(new FakeHandler(_ =>
        {
            var bytes = Encoding.UTF8.GetBytes("<status><name>mServer</name></status>");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        }))
        { BaseAddress = new Uri("http://127.0.0.1:444/") };
        return new MServerClient(httpClient, NullLogger<MServerClient>.Instance);
    }

    [Fact]
    public async Task EnsureRunningAsync_returns_available_without_starting_anything_when_mServer_already_responds()
    {
        var launcher = new MServerLauncher(AlwaysAvailableClient(), Config(pohodaExePath: null), NullLogger<MServerLauncher>.Instance);

        var result = await launcher.EnsureRunningAsync();

        Assert.True(result.IsAvailable);
        Assert.False(result.StartedByUs);
    }

    [Fact]
    public async Task EnsureRunningAsync_skips_auto_start_and_returns_unavailable_when_no_exe_path_is_configured()
    {
        var launcher = new MServerLauncher(AlwaysUnavailableClient(), Config(pohodaExePath: null), NullLogger<MServerLauncher>.Instance);

        var result = await launcher.EnsureRunningAsync();

        Assert.False(result.IsAvailable);
        Assert.False(result.StartedByUs);
    }

    [Fact]
    public async Task EnsureRunningAsync_skips_auto_start_and_returns_unavailable_when_the_configured_exe_path_does_not_exist()
    {
        var launcher = new MServerLauncher(
            AlwaysUnavailableClient(), Config(pohodaExePath: @"C:\does\not\exist\Pohoda.exe"), NullLogger<MServerLauncher>.Instance);

        var result = await launcher.EnsureRunningAsync();

        Assert.False(result.IsAvailable);
        Assert.False(result.StartedByUs);
    }

    [Fact]
    public async Task StopAsync_does_nothing_when_mServer_is_already_unavailable()
    {
        var launcher = new MServerLauncher(AlwaysUnavailableClient(), Config(pohodaExePath: null), NullLogger<MServerLauncher>.Instance);

        // Must not throw even with no exe path configured and nothing running.
        await launcher.StopAsync();
    }
}
