using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pohoda.Contract.Services;
using Pohoda.MServer;

namespace Pohoda.Server.Services;

/// <summary>
/// Registers the Pohoda agenda services that sit on top of one mServer instance.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers keyed <see cref="IReceiptService"/> and <see cref="IStockCardService"/> implementations
    /// bound to the mServer instance registered under <paramref name="instanceKey"/>. Call
    /// <c>AddPohodaMServer(configuration, instanceKey)</c> for the same key first — this method resolves
    /// that instance's <see cref="MServerClient"/> and its named <see cref="MServerConfiguration"/>.
    /// Consumers resolve the instance they need with
    /// <c>GetRequiredKeyedService&lt;IReceiptService&gt;("AcEcom")</c> or the matching
    /// <c>[FromKeyedServices("AcEcom")]</c> constructor parameter.
    /// </summary>
    public static IServiceCollection AddPohodaAgendaServices(this IServiceCollection services, string instanceKey)
    {
        services.AddKeyedSingleton<IReceiptService>(instanceKey, (sp, key) =>
            new ReceiptService(
                sp.GetRequiredKeyedService<MServerClient>(key),
                sp.GetRequiredService<IOptionsMonitor<MServerConfiguration>>().Get((string)key!)));

        services.AddKeyedSingleton<IStockCardService>(instanceKey, (sp, key) =>
            new StockCardService(
                sp.GetRequiredKeyedService<MServerClient>(key),
                sp.GetRequiredService<IOptionsMonitor<MServerConfiguration>>().Get((string)key!)));

        return services;
    }
}
