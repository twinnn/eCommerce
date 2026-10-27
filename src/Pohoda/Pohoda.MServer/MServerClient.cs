using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace Pohoda.MServer;

/// <summary>
/// Thin HTTP client for POHODA mServer. Auth header and base address are configured by the caller
/// (see <see cref="ServiceCollectionExtensions.AddPohodaMServer"/>) via the standard
/// <c>IHttpClientFactory</c> named-client pattern. Every response is parsed for `state`/`error`
/// (via <see cref="PohodaResponseParser"/>) before being trusted.
/// </summary>
public sealed class MServerClient
{
    private static readonly Encoding Windows1250;
    private readonly HttpClient _http;
    private readonly ILogger<MServerClient> _logger;

    static MServerClient()
    {
        Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        Windows1250 = Encoding.GetEncoding(1250);
    }

    public MServerClient(HttpClient httpClient, ILogger<MServerClient> logger)
    {
        _http = httpClient;
        _logger = logger;
    }

    /// <summary>Non-throwing health-check against `GET /status`.</summary>
    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await _http.GetAsync("status", ct);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var body = await ReadWindows1250Async(response, ct);
            return body.Contains("mServer", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "mServer /status health-check zlyhal.");
            return false;
        }
    }

    /// <summary>Sends a dataPack request and returns the parsed result without throwing on a Pohoda-level error.</summary>
    public async Task<PohodaResult> SendAsync(XDocument requestDataPack, CancellationToken ct = default)
    {
        var bodyBytes = Windows1250.GetBytes(BuildXmlWithDeclaration(requestDataPack));
        using var content = new ByteArrayContent(bodyBytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = "windows-1250" };

        using var response = await _http.PostAsync("xml", content, ct);
        var responseText = await ReadWindows1250Async(response, ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new PohodaResponseException(
                $"mServer HTTP {(int)response.StatusCode} ({ExplainStatusCode(response.StatusCode)}).",
                [responseText]);
        }

        XDocument doc;
        try
        {
            doc = XDocument.Parse(responseText);
        }
        catch (Exception ex)
        {
            throw new PohodaResponseException("Odpoveď mServer sa nedala naparsovať ako XML.", [ex.Message]);
        }

        var result = PohodaResponseParser.Parse(doc);
        foreach (var warning in result.Warnings)
        {
            _logger.LogWarning("mServer warning: {Warning}", warning);
        }

        if (!result.Success)
        {
            _logger.LogWarning("mServer odpoveď so stavom error, raw XML: {RawResponse}", responseText);
        }
        else
        {
            _logger.LogDebug("mServer raw response: {RawResponse}", responseText);
        }

        return result;
    }

    /// <summary>Same as <see cref="SendAsync"/> but throws <see cref="PohodaResponseException"/> if the result is not successful.</summary>
    public async Task<PohodaResult> SendOrThrowAsync(XDocument requestDataPack, CancellationToken ct = default)
    {
        var result = await SendAsync(requestDataPack, ct);
        if (!result.Success)
        {
            throw new PohodaResponseException("mServer vrátil chybový stav.", result.Errors);
        }

        return result;
    }

    private static string BuildXmlWithDeclaration(XDocument doc)
    {
        var body = doc.Root!.ToString(SaveOptions.DisableFormatting);
        return "<?xml version=\"1.0\" encoding=\"Windows-1250\"?>" + body;
    }

    private static async Task<string> ReadWindows1250Async(HttpResponseMessage response, CancellationToken ct)
    {
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        return Windows1250.GetString(bytes);
    }

    private static string ExplainStatusCode(HttpStatusCode code) => code switch
    {
        HttpStatusCode.Unauthorized => "chýbajú alebo sú neplatné prihlasovacie údaje (STW-Authorization)",
        HttpStatusCode.Forbidden => "užívateľ nemá právo otvoriť účtovnú jednotku / POHODA mServer",
        HttpStatusCode.NotFound => "nesprávna URL cesta (chýba /xml)",
        HttpStatusCode.RequestTimeout => "vypršal čas na spracovanie požiadavky",
        _ => code.ToString()
    };
}
