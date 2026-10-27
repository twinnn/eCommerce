using Jobs.Receipts.Scan;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pohoda.MServer;
using Pohoda.Server.Services;
using Receipts.Contract;
using Receipts.Core;
using Receipts.Gabel;

// The content root is the application directory rather than the current working directory, so the
// built .exe finds its appsettings.json wherever it is launched from (a scheduled task, a service
// wrapper, or `dotnet run`). Setting it here keeps the default configuration order intact — user
// secrets and environment variables still override appsettings.json.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
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
        "Sekcia 'Watch' je neúplná alebo neplatná: vyžaduje InboxFolder, ArchiveFolder, PendingApprovalFolder, DatabasePath a kladný ScanInterval.")
    .ValidateOnStart();
builder.Services.Configure<MatchingOptions>(builder.Configuration.GetSection(MatchingOptions.SectionName));

// Stopping an mServer instance this service started can take the launcher's full retry ladder;
// the default 30s shutdown timeout would abort the process first and leave Pohoda running.
builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromMinutes(2));

// The supplier plugin. A second supplier later means registering a different IInvoiceParser /
// IVariantDetector pair here — Receipts.Core never names a supplier.
builder.Services.AddSingleton<IInvoiceParser, GabelInvoiceParser>();
builder.Services.AddSingleton<IVariantDetector, GabelVariantDetector>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
await host.RunAsync();
