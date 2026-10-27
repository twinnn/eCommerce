namespace Pohoda.MServer;

/// <summary>
/// Thrown when POHODA mServer responds with a transport-level failure (bad HTTP status, unparsable XML)
/// or with a `responsePack`/`responsePackItem` state of "error".
/// </summary>
public sealed class PohodaResponseException : Exception
{
    public IReadOnlyList<string> Errors { get; }

    public PohodaResponseException(string message, IReadOnlyList<string> errors)
        : base(message + (errors.Count > 0 ? " " + string.Join(" | ", errors) : string.Empty))
    {
        Errors = errors;
    }
}
