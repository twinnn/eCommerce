namespace Jobs.Receipts.Apply;

/// <summary>
/// Which POHODA mServer instance this application writes to: "AcEcom", the Distributor's Pohoda
/// (port 444). The key selects both the config section (<c>Pohoda:AcEcom:*</c>) and the keyed DI
/// registrations made by <c>AddPohodaMServer</c> / <c>AddPohodaAgendaServices</c>. It is our own
/// name for the instance; <c>ConfigName</c> and <c>DefaultStorageCode</c> in that section stay
/// "GoNoow" because those are the real mServer configuration and warehouse names inside Pohoda.
/// </summary>
internal static class PohodaInstance
{
    public const string Key = "AcEcom";
}
