namespace Pohoda.MServer;

/// <summary>Connection settings for one Pohoda mServer instance (e.g. "AcEcom").</summary>
public sealed class MServerConfiguration
{
    public const string SectionName = "Pohoda";

    public required string BaseUrl { get; init; } = "http://127.0.0.1";
    public required int Port { get; init; }

    /// <summary>Name of the mServer configuration as set up in Pohoda (Účetní jednotky → Databáze → POHODA mServer). Used for the auto-start via `pohoda.exe /http start|stop "name"`.</summary>
    public required string ConfigName { get; init; }

    /// <summary>Full path to Pohoda.exe, used only to auto-start mServer when it is not already running.</summary>
    public string? PohodaExePath { get; init; }

    public required string Username { get; init; }
    public required string Password { get; init; }

    /// <summary>IČO of the accounting unit this mServer instance serves.</summary>
    public required string Ico { get; init; }

    /// <summary>Default "storage" (členění skladu) code used when creating new příjemky/skladové karty.</summary>
    public required string DefaultStorageCode { get; init; }

    /// <summary>Default price-group ("cenová skupina") code used when auto-creating a new skladová karta.</summary>
    public required string DefaultTypePriceCode { get; init; }

    public Uri Endpoint => new($"{BaseUrl}:{Port}/");
}
