using System.Net;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Pohoda.MServer;

namespace Pohoda.Server.Tests;

/// <summary>
/// Stands in for POHODA mServer: records every dataPack the service under test sends (decoded from
/// the Windows-1250 wire bytes) and replies with canned responsePack XML, in FIFO order — an OK
/// response with an empty item when nothing was enqueued.
/// </summary>
internal sealed class FakeMServer
{
    private static readonly Encoding Windows1250 = GetWindows1250();
    private readonly Queue<string> _responses = new();

    public List<XDocument> Requests { get; } = [];

    public MServerClient Client { get; }

    public MServerConfiguration Config { get; } = new()
    {
        BaseUrl = "http://127.0.0.1",
        Port = 444,
        ConfigName = "GoNoow",
        Username = "user",
        Password = "secret",
        Ico = "53870441",
        DefaultStorageCode = "GoNoow",
        DefaultTypePriceCode = "Cenova skupina 1"
    };

    public FakeMServer()
    {
        var http = new HttpClient(new Handler(this)) { BaseAddress = Config.Endpoint };
        Client = new MServerClient(http, NullLogger<MServerClient>.Instance);
    }

    public FakeMServer Enqueue(string responseXml)
    {
        _responses.Enqueue(responseXml);
        return this;
    }

    /// <summary>The single dataPackItem payload of the N-th captured request (default: the first).</summary>
    public XElement RequestPayload(int index = 0) =>
        Requests[index].Root!.Elements().Single().Elements().Single();

    public static string OkResponse(string innerXml = "") => $"""
        <rsp:responsePack xmlns:rsp="http://www.stormware.cz/schema/version_2/response.xsd"
                          xmlns:lStk="http://www.stormware.cz/schema/version_2/list_stock.xsd"
                          xmlns:stk="http://www.stormware.cz/schema/version_2/stock.xsd"
                          xmlns:lst="http://www.stormware.cz/schema/version_2/list.xsd"
                          xmlns:pri="http://www.stormware.cz/schema/version_2/prijemka.xsd"
                          xmlns:typ="http://www.stormware.cz/schema/version_2/type.xsd"
                          xmlns:rdc="http://www.stormware.cz/schema/version_2/documentresponse.xsd"
                          version="2.0" state="ok">
          <rsp:responsePackItem version="2.0" id="item1" state="ok">{innerXml}</rsp:responsePackItem>
        </rsp:responsePack>
        """;

    public static string ErrorResponse(string note) => $"""
        <rsp:responsePack xmlns:rsp="http://www.stormware.cz/schema/version_2/response.xsd" version="2.0" state="ok">
          <rsp:responsePackItem version="2.0" id="item1" state="error" note="{note}" />
        </rsp:responsePack>
        """;

    private static Encoding GetWindows1250()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1250);
    }

    private sealed class Handler(FakeMServer owner) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var bytes = await request.Content!.ReadAsByteArrayAsync(ct);
            owner.Requests.Add(XDocument.Parse(Windows1250.GetString(bytes)));

            var body = owner._responses.Count > 0 ? owner._responses.Dequeue() : OkResponse();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Windows1250.GetBytes(body)) };
        }
    }
}
