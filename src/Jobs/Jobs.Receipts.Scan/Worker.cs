using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pohoda.Contract.Services;
using Pohoda.MServer;
using Receipts.Contract;
using Receipts.Core;
using Receipts.Core.Persistence;

namespace Jobs.Receipts.Scan;

/// <summary>
/// Sweeps the invoice inbox on a schedule. Each cycle starts mServer if it is not already running,
/// opens the local state store, runs one <see cref="ScanRunner"/> sweep and stops mServer again — but
/// only if this process was the one that started it, so a cycle never shuts an instance out from under
/// Pohoda E1 or another integration.
///
/// A failing cycle is logged and the loop continues: a scan is read-only against Pohoda and fully
/// repeatable, so a transient mServer or file-system fault must not take the service down.
/// </summary>
public sealed class Worker : BackgroundService
{
    private readonly MServerLauncher _launcher;
    private readonly IReceiptService _receipts;
    private readonly IStockCardService _stockCards;
    private readonly IInvoiceParser _parser;
    private readonly IVariantDetector _variantDetector;
    private readonly WatchOptions _watch;
    private readonly MatchingOptions _matching;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<Worker> _logger;

    public Worker(
        [FromKeyedServices(PohodaInstance.Key)] MServerLauncher launcher,
        [FromKeyedServices(PohodaInstance.Key)] IReceiptService receipts,
        [FromKeyedServices(PohodaInstance.Key)] IStockCardService stockCards,
        IInvoiceParser parser,
        IVariantDetector variantDetector,
        IOptions<WatchOptions> watch,
        IOptions<MatchingOptions> matching,
        ILoggerFactory loggerFactory,
        ILogger<Worker> logger)
    {
        _launcher = launcher;
        _receipts = receipts;
        _stockCards = stockCards;
        _parser = parser;
        _variantDetector = variantDetector;
        _watch = watch.Value;
        _matching = matching.Value;
        _loggerFactory = loggerFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Jobs.Receipts.Scan spustený. mServer inštancia '{Instance}', interval {Interval}, inbox {Inbox}, na schválenie {Pending}, archív {Archive}, databáza {Database}.",
            PohodaInstance.Key, _watch.ScanInterval, _watch.InboxFolder, _watch.PendingApprovalFolder, _watch.ArchiveFolder, ResolvePath(_watch.DatabasePath));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cyklus skenovania zlyhal, skúsim znova o {Interval}.", _watch.ScanInterval);
            }

            try
            {
                await Task.Delay(_watch.ScanInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        var availability = await _launcher.EnsureRunningAsync(ct);
        if (!availability.IsAvailable)
        {
            _logger.LogError(
                "mServer pre Pohoda inštanciu '{Instance}' nie je dostupný a automatický štart zlyhal. Skontroluj, či beží (Pohoda E1 → Databáze → POHODA mServer) a či je v appsettings.json nastavená správna 'Pohoda:{Instance}:PohodaExePath'.",
                PohodaInstance.Key, PohodaInstance.Key);
            return;
        }

        try
        {
            await using var store = await ReceiptsStateStore.OpenAsync(ResolvePath(_watch.DatabasePath), ct);

            var builder = new ProposalBuilder(
                _stockCards, _variantDetector, store.StockMappings, _loggerFactory.CreateLogger<ProposalBuilder>());
            var runner = new ScanRunner(
                _receipts, _parser, builder, store, _watch, _matching, _loggerFactory.CreateLogger<ScanRunner>());

            await runner.RunOnceAsync(ct);
        }
        finally
        {
            if (availability.StartedByUs)
            {
                // Not the caller's cancellation token: the instance we started must be stopped even
                // when the sweep was cancelled by a shutdown.
                await _launcher.StopAsync(CancellationToken.None);
            }
        }
    }

    /// <summary>Relative configured paths resolve against the application directory, not the current working directory.</summary>
    private static string ResolvePath(string path) =>
        Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);
}
