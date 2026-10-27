namespace Jobs.Receipts.Scan;

/// <summary>
/// Which POHODA mServer instance this application talks to. "GoNoow" is the Receipts instance
/// (port 444, its own warehouse); the existing Jobs use "StwTest" on port 5336 against the same
/// company. The key selects both the config section (<c>Pohoda:GoNoow:*</c>) and the keyed DI
/// registrations made by <c>AddPohodaMServer</c> / <c>AddPohodaAgendaServices</c>.
/// </summary>
internal static class PohodaInstance
{
    public const string Key = "GoNoow";
}
