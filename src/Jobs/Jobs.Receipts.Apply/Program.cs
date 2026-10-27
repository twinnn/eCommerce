using Jobs.Receipts.Apply;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pohoda.Contract.Services;
using Pohoda.MServer;
using Pohoda.Server.Services;
using Receipts.Core;
using Receipts.Core.Persistence;

var command = CommandLine.Parse(args);
if (command.Kind == ReceiptsCommandKind.Usage)
{
    Console.WriteLine(CommandLine.Usage);
    return 1;
}

// The host is built WITHOUT `args`: the command-line configuration provider throws on a bare
// positional sub-command such as "apply". The command line is parsed above instead. The content root
// is the application directory so the built .exe finds its appsettings.json wherever it is launched
// from, while leaving user secrets and environment variables free to override it.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    ContentRootPath = AppContext.BaseDirectory
});

builder.Services.AddPohodaMServer(builder.Configuration, PohodaInstance.Key);
builder.Services.AddPohodaAgendaServices(PohodaInstance.Key);
builder.Services.AddOptions<WatchOptions>()
    .Bind(builder.Configuration.GetSection(WatchOptions.SectionName))
    .Validate(
        o => !string.IsNullOrWhiteSpace(o.InboxFolder)
             && !string.IsNullOrWhiteSpace(o.ArchiveFolder)
             && !string.IsNullOrWhiteSpace(o.PendingApprovalFolder)
             && !string.IsNullOrWhiteSpace(o.DatabasePath)
             && o.ScanInterval > TimeSpan.Zero,
        "Sekcia 'Watch' je neúplná alebo neplatná: vyžaduje InboxFolder, ArchiveFolder, PendingApprovalFolder, DatabasePath a kladný ScanInterval.");
builder.Services.Configure<MatchingOptions>(builder.Configuration.GetSection(MatchingOptions.SectionName));

using var host = builder.Build();

var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
var log = loggerFactory.CreateLogger("Jobs.Receipts.Apply");

if (command.Error is not null)
{
    log.LogError("{Error}", command.Error);
    return 1;
}

try
{
    // Reading IOptions<WatchOptions>.Value is where the Validate() check above actually fires — this
    // application never runs the host, so ValidateOnStart would never trigger. Everything downstream
    // of this read that could throw must stay inside this try so a bad 'Watch' section, or any other
    // startup failure, is reported the same way as a failure during the command itself.
    var watch = host.Services.GetRequiredService<IOptions<WatchOptions>>().Value;
    var matching = host.Services.GetRequiredService<IOptions<MatchingOptions>>();
    var launcher = host.Services.GetRequiredKeyedService<MServerLauncher>(PohodaInstance.Key);
    var receipts = host.Services.GetRequiredKeyedService<IReceiptService>(PohodaInstance.Key);
    var stockCards = host.Services.GetRequiredKeyedService<IStockCardService>(PohodaInstance.Key);

    var databasePath = ResolvePath(watch.DatabasePath);
    log.LogInformation(
        "Jobs.Receipts.Apply — mServer inštancia '{Instance}', archív {Archive}, databáza {Database}.",
        PohodaInstance.Key, watch.ArchiveFolder, databasePath);

    await using var store = await ReceiptsStateStore.OpenAsync(databasePath);

    var applier = new ProposalApplier(stockCards, receipts, store, matching, loggerFactory.CreateLogger<ProposalApplier>());
    var runner = new ApplyRunner(applier, store, watch, loggerFactory.CreateLogger<ApplyRunner>());

    return command.Kind switch
    {
        ReceiptsCommandKind.Status => await runner.StatusAsync(),
        ReceiptsCommandKind.Reset => await runner.ResetAsync(command.Id!),
        _ => await RunApplyAsync(launcher, runner)
    };
}
catch (OptionsValidationException ex)
{
    foreach (var failure in ex.Failures)
    {
        log.LogError("{Failure}", failure);
    }

    return 1;
}
catch (PohodaResponseException ex)
{
    log.LogError("Chyba pri komunikácii s mServer: {Message}", ex.Message);
    return 1;
}
catch (Exception ex)
{
    log.LogError(ex, "Neočakávaná chyba.");
    return 1;
}

// `status` and `reset` are local-only and never touch mServer, so only `apply` pays for starting it.
async Task<int> RunApplyAsync(MServerLauncher launcher, ApplyRunner runner)
{
    var availability = await launcher.EnsureRunningAsync();
    if (!availability.IsAvailable)
    {
        log.LogError(
            "mServer '{Instance}' nie je dostupný a automatický štart zlyhal. Skontroluj, či beží (Pohoda E1 → Databáze → POHODA mServer) a či je v appsettings.json nastavená správna 'Pohoda:{Instance}:PohodaExePath'.",
            PohodaInstance.Key, PohodaInstance.Key);
        return 1;
    }

    try
    {
        return await runner.ApplyAsync(command.Id, command.All);
    }
    finally
    {
        if (availability.StartedByUs)
        {
            await launcher.StopAsync();
        }
    }
}

static string ResolvePath(string path) =>
    Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);
