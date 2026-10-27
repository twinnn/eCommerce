using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Pohoda.MServer;

/// <summary>Result of <see cref="MServerLauncher.EnsureRunningAsync"/>.</summary>
public readonly record struct MServerAvailability(bool IsAvailable, bool StartedByUs);

/// <summary>
/// Ensures POHODA mServer is running before the client makes a request, starting it via
/// `pohoda.exe /http start "&lt;ConfigName&gt;"` if it isn't — and can stop it again afterwards via
/// `pohoda.exe /http stop "&lt;ConfigName&gt;"`. Invokes Pohoda.exe directly via
/// <see cref="ProcessStartInfo.ArgumentList"/> (no `cmd.exe` in between).
///
/// Callers should only call <see cref="StopAsync"/> when <see cref="MServerAvailability.StartedByUs"/>
/// was true — i.e. only stop an mServer instance this application itself started, never one that was
/// already running (it may be serving other clients, e.g. Pohoda E1 itself or another integration).
/// </summary>
public sealed class MServerLauncher
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)
    ];

    private readonly MServerClient _client;
    private readonly MServerConfiguration _config;
    private readonly ILogger<MServerLauncher> _logger;

    public MServerLauncher(MServerClient client, MServerConfiguration config, ILogger<MServerLauncher> logger)
    {
        _client = client;
        _config = config;
        _logger = logger;
    }

    /// <summary>Checks `/status` and starts mServer if needed. <see cref="MServerAvailability.StartedByUs"/> tells the caller whether it should later call <see cref="StopAsync"/>.</summary>
    public async Task<MServerAvailability> EnsureRunningAsync(CancellationToken ct = default)
    {
        if (await _client.IsAvailableAsync(ct))
        {
            return new MServerAvailability(true, StartedByUs: false);
        }

        if (!TryGetExePath(out var exePath))
        {
            _logger.LogWarning(
                "mServer neodpovedá a 'Pohoda:PohodaExePath' nie je nastavená na existujúci Pohoda.exe — automatický štart preskočený.");
            return new MServerAvailability(false, StartedByUs: false);
        }

        _logger.LogInformation("mServer neodpovedá, spúšťam konfiguráciu '{ConfigName}' cez {ExePath}.", _config.ConfigName, exePath);

        if (!RunPohodaHttpCommand(exePath, "start"))
        {
            return new MServerAvailability(false, StartedByUs: false);
        }

        foreach (var delay in RetryDelays)
        {
            await Task.Delay(delay, ct);
            if (await _client.IsAvailableAsync(ct))
            {
                _logger.LogInformation("mServer '{ConfigName}' beží.", _config.ConfigName);
                return new MServerAvailability(true, StartedByUs: true);
            }
        }

        _logger.LogError("mServer '{ConfigName}' neodpovedal ani po pokuse o automatický štart.", _config.ConfigName);
        return new MServerAvailability(false, StartedByUs: false);
    }

    /// <summary>Stops the mServer configuration via `pohoda.exe /http stop`. Only call this for an instance this application started itself.</summary>
    public async Task StopAsync(CancellationToken ct = default)
    {
        if (!TryGetExePath(out var exePath))
        {
            return;
        }

        if (!await _client.IsAvailableAsync(ct))
        {
            return;
        }

        _logger.LogInformation("Zastavujem mServer '{ConfigName}'.", _config.ConfigName);
        RunPohodaHttpCommand(exePath, "stop");

        foreach (var delay in RetryDelays)
        {
            await Task.Delay(delay, ct);
            if (!await _client.IsAvailableAsync(ct))
            {
                _logger.LogInformation("mServer '{ConfigName}' zastavený.", _config.ConfigName);
                return;
            }
        }

        _logger.LogWarning("mServer '{ConfigName}' po pokuse o zastavenie stále odpovedá.", _config.ConfigName);
    }

    private bool TryGetExePath(out string exePath)
    {
        exePath = _config.PohodaExePath ?? string.Empty;
        return !string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath);
    }

    private bool RunPohodaHttpCommand(string exePath, string action)
    {
        try
        {
            using var process = new Process();
            process.StartInfo.FileName = exePath;
            process.StartInfo.ArgumentList.Add("/http");
            process.StartInfo.ArgumentList.Add(action);
            process.StartInfo.ArgumentList.Add(_config.ConfigName);
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pohoda.exe /http {Action} '{ConfigName}' zlyhalo.", action, _config.ConfigName);
            return false;
        }
    }
}
