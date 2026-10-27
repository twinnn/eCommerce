using System.Net;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Pohoda.MServer;

namespace Pohoda.MServer.Tests;

public class MServerClientTests
{
    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(respond(request));
    }

    private sealed class AsyncFakeHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            respond(request);
    }

    private static Encoding Windows1250()
    {
        Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1250);
    }

    private static HttpResponseMessage Windows1250Response(HttpStatusCode status, string xmlBody)
    {
        var bytes = Windows1250().GetBytes(xmlBody);
        return new HttpResponseMessage(status) { Content = new ByteArrayContent(bytes) };
    }

    private static MServerClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var httpClient = new HttpClient(new FakeHandler(respond)) { BaseAddress = new Uri("http://127.0.0.1:444/") };
        return new MServerClient(httpClient, NullLogger<MServerClient>.Instance);
    }

    [Fact]
    public async Task IsAvailableAsync_returns_true_when_status_endpoint_reports_mServer()
    {
        var client = CreateClient(req =>
        {
            Assert.Equal("status", req.RequestUri!.AbsolutePath.TrimStart('/'));
            return Windows1250Response(HttpStatusCode.OK, "<status><name>mServer</name></status>");
        });

        Assert.True(await client.IsAvailableAsync());
    }

    [Fact]
    public async Task IsAvailableAsync_returns_false_on_a_non_success_status_code()
    {
        var client = CreateClient(_ => Windows1250Response(HttpStatusCode.Unauthorized, ""));

        Assert.False(await client.IsAvailableAsync());
    }

    [Fact]
    public async Task SendAsync_returns_a_successful_result_for_an_ok_response()
    {
        var client = CreateClient(req =>
        {
            Assert.Equal("xml", req.RequestUri!.AbsolutePath.TrimStart('/'));
            return Windows1250Response(HttpStatusCode.OK, """
                <rsp:responsePack xmlns:rsp="http://www.stormware.cz/schema/version_2/response.xsd" state="ok">
                  <rsp:responsePackItem state="ok" />
                </rsp:responsePack>
                """);
        });

        var result = await client.SendAsync(new XDocument(new XElement("dummy")));

        Assert.True(result.Success);
    }

    [Fact]
    public async Task SendAsync_throws_PohodaResponseException_on_a_non_success_http_status()
    {
        var client = CreateClient(_ => Windows1250Response(HttpStatusCode.Unauthorized, ""));

        await Assert.ThrowsAsync<PohodaResponseException>(() => client.SendAsync(new XDocument(new XElement("dummy"))));
    }

    [Fact]
    public async Task SendOrThrowAsync_throws_when_the_parsed_result_is_not_successful()
    {
        var client = CreateClient(_ => Windows1250Response(HttpStatusCode.OK, """
            <rsp:responsePack xmlns:rsp="http://www.stormware.cz/schema/version_2/response.xsd" state="ok">
              <rsp:responsePackItem state="error" note="boom" />
            </rsp:responsePack>
            """));

        var ex = await Assert.ThrowsAsync<PohodaResponseException>(
            () => client.SendOrThrowAsync(new XDocument(new XElement("dummy"))));
        Assert.Contains("boom", ex.Message);
    }

    [Fact]
    public async Task SendAsync_round_trips_windows_1250_bytes_correctly()
    {
        // "Skladová karta" — the accented characters only round-trip correctly if the request/response
        // bytes are handled as Windows-1250, not UTF-8. This is the exact class of bug the previous
        // eCommerce integration had.

        // Verify OUTGOING request is Windows-1250-encoded by capturing the actual request bytes
        byte[]? capturedRequestBytes = null;

        var handler = new AsyncFakeHandler(async req =>
        {
            capturedRequestBytes = await req.Content!.ReadAsByteArrayAsync();

            // The outgoing XML should contain "Skladová karta" (accented characters must survive Windows-1250 encoding)
            var decodedRequest = Windows1250().GetString(capturedRequestBytes);
            Assert.Contains("Skladová karta", decodedRequest);

            return Windows1250Response(HttpStatusCode.OK, """
                <rsp:responsePack xmlns:rsp="http://www.stormware.cz/schema/version_2/response.xsd" state="ok">
                  <rsp:responsePackItem state="ok" note="Skladová karta zapísaná" />
                </rsp:responsePack>
                """);
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:444/") };
        var client = new MServerClient(httpClient, NullLogger<MServerClient>.Instance);

        var result = await client.SendAsync(new XDocument(new XElement("dummy", "Skladová karta")));

        Assert.True(result.Success);

        // Verify INCOMING response is decoded correctly by checking the parsed document contains the correct accented text
        var noteElement = result.Document!.Descendants()
            .First(e => e.Attribute("note") != null);
        var noteValue = noteElement.Attribute("note")!.Value;

        // This will only pass if the response bytes were actually decoded as Windows-1250,
        // not UTF-8 (which would corrupt the accented characters á, í, á)
        Assert.Equal("Skladová karta zapísaná", noteValue);
    }
}
