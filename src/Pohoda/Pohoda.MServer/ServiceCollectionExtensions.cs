using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Pohoda.MServer;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers one Pohoda mServer instance under <paramref name="instanceKey"/>: bound
    /// <see cref="MServerConfiguration"/> (from config section <c>Pohoda:&lt;instanceKey&gt;</c>), a
    /// named <see cref="HttpClient"/> (base address + STW-Authorization header), and keyed
    /// <see cref="MServerClient"/>/<see cref="MServerLauncher"/> singletons. Call once per mServer
    /// instance eCommerce needs to talk to (e.g. "StwTest" for the existing Jobs, "GoNoow" for Receipts)
    /// — each instance is independently resolvable via keyed DI.
    /// </summary>
    public static IServiceCollection AddPohodaMServer(
        this IServiceCollection services, IConfiguration configuration, string instanceKey)
    {
        services.AddOptions<MServerConfiguration>(instanceKey)
            .Bind(configuration.GetSection($"{MServerConfiguration.SectionName}:{instanceKey}"));

        services.AddHttpClient(instanceKey, (sp, http) =>
        {
            var config = sp.GetRequiredService<IOptionsMonitor<MServerConfiguration>>().Get(instanceKey);

            http.BaseAddress = config.Endpoint;

            // POHODA mServer does NOT use the standard HTTP `Authorization` header — it requires a
            // custom `STW-Authorization` header. Setting `http.DefaultRequestHeaders.Authorization`
            // instead is silently wrong: .NET sends it as `Authorization`, which mServer ignores, and
            // the request fails with HTTP 401.
            http.DefaultRequestHeaders.Add(
                "STW-Authorization",
                "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{config.Username}:{config.Password}")));
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));
        });

        services.AddKeyedSingleton<MServerClient>(instanceKey, (sp, key) =>
            new MServerClient(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient((string)key!),
                sp.GetRequiredService<ILogger<MServerClient>>()));

        services.AddKeyedSingleton<MServerLauncher>(instanceKey, (sp, key) =>
            new MServerLauncher(
                sp.GetRequiredKeyedService<MServerClient>(key),
                sp.GetRequiredService<IOptionsMonitor<MServerConfiguration>>().Get((string)key!),
                sp.GetRequiredService<ILogger<MServerLauncher>>()));

        return services;
    }
}
