namespace Jobs.Receipts.Apply;

/// <summary>Which command the operator asked for. <see cref="Usage"/> means "print the help and stop".</summary>
public enum ReceiptsCommandKind
{
    Usage,
    Apply,
    Status,
    Reset
}

/// <summary>
/// A parsed command line. <see cref="Error"/> is a ready-to-log Slovak message set when the command
/// was recognized but its arguments were unusable — the caller logs it and exits with code 1.
/// </summary>
public readonly record struct ReceiptsCommand(
    ReceiptsCommandKind Kind,
    string? Id = null,
    bool All = false,
    string? Error = null);

/// <summary>
/// Parses this application's positional sub-commands. Deliberately hand-written and dependency-free:
/// the host's command-line configuration provider cannot be used here, because it rejects a bare
/// positional argument like "apply".
/// </summary>
public static class CommandLine
{
    public const string Usage = """
        Jobs.Receipts.Apply

        Príkazy:
          apply --id <id>      Zapíše konkrétny schválený proposal do Pohody.
          apply --all          Zapíše všetky proposaly čakajúce na schválenie.
          status               Vypíše čakajúce a zlyhané proposaly.
          reset --id <id>      Vráti proposal na PendingApproval a presunie PDF späť z archívu (na opakovaný pokus po oprave chyby).

        Skenovanie faktúr a tvorbu návrhov robí služba Jobs.Receipts.Scan, nie tento nástroj.
        """;

    public static ReceiptsCommand Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new ReceiptsCommand(ReceiptsCommandKind.Usage);
        }

        return args[0] switch
        {
            "apply" => ParseApply(args),
            "status" => new ReceiptsCommand(ReceiptsCommandKind.Status),
            "reset" => ParseReset(args),
            _ => new ReceiptsCommand(ReceiptsCommandKind.Usage)
        };
    }

    private static ReceiptsCommand ParseApply(string[] args)
    {
        if (args.Contains("--all"))
        {
            return new ReceiptsCommand(ReceiptsCommandKind.Apply, All: true);
        }

        var id = ReadId(args);
        return id is null
            ? new ReceiptsCommand(ReceiptsCommandKind.Apply, Error: "Použi 'apply --id <id>' alebo 'apply --all'.")
            : new ReceiptsCommand(ReceiptsCommandKind.Apply, Id: id);
    }

    private static ReceiptsCommand ParseReset(string[] args)
    {
        var id = ReadId(args);
        return id is null
            ? new ReceiptsCommand(ReceiptsCommandKind.Reset, Error: "Použi 'reset --id <id>'.")
            : new ReceiptsCommand(ReceiptsCommandKind.Reset, Id: id);
    }

    private static string? ReadId(string[] args)
    {
        var index = Array.IndexOf(args, "--id");
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
