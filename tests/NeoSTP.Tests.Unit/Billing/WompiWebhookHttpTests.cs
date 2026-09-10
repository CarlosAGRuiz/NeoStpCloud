using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NeoSTP.Api.Controllers;
using NeoSTP.Application.Billing;
using NeoSTP.Application.Common;

namespace NeoSTP.Tests.Unit.Billing;

/// <summary>Only in-process TestServer/controller and a fake receiver. No Program, sockets or database.</summary>
public sealed class WompiWebhookHttpTests
{
    [Fact]
    public async Task Original_utf8_bytes_and_single_signature_reach_receiver_without_reserialization()
    {
        using var f = new Fixture();
        var body = Encoding.UTF8.GetBytes("{  \"Nombre\" : \"José\",\n \"Monto\":12.340 }");
        var receipt = Guid.NewGuid();
        f.Receiver.Response = Result<WompiWebhookReceipt>.Ok(new(receipt, "VERIFIED_SANDBOX"));

        using var response = await f.Post(body, ["ABCDEF0123456789"]);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        f.Receiver.Calls.Should().Be(1);
        f.Receiver.Body.Should().Equal(body);
        f.Receiver.Signature.Should().Be("ABCDEF0123456789");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("receiptId").GetGuid().Should().Be(receipt);
        json.RootElement.GetProperty("status").GetString().Should().Be("VERIFIED_SANDBOX");
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("blank")]
    [InlineData("multiple")]
    public async Task Missing_or_multiple_hash_headers_are_rejected_before_receiver(string scenario)
    {
        using var f = new Fixture();
        var hashes = scenario switch { "missing" => Array.Empty<string>(), "blank" => [" "], _ => new[] { "hash-a", "hash-b" } };
        using var response = await f.Post(Encoding.UTF8.GetBytes("{}"), hashes);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        f.Receiver.Calls.Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Oversized_original_body_is_rejected_for_known_and_unknown_content_length(bool unknownLength)
    {
        using var f = new Fixture();
        using var response = await f.Post(new byte[65537], ["synthetic-hash"], unknownLength);
        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        f.Receiver.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Body_at_exact_limit_is_forwarded_to_receiver()
    {
        using var f = new Fixture();
        var body = new byte[65536];
        using var response = await f.Post(body, ["synthetic-hash"], unknownLength: true);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        f.Receiver.Body.Should().Equal(body);
        f.Receiver.Calls.Should().Be(1);
    }

    [Theory]
    [InlineData("WOMPI_SIGNATURE_INVALID", HttpStatusCode.Unauthorized)]
    [InlineData("WOMPI_IDENTITY_MISMATCH", HttpStatusCode.Unauthorized)]
    [InlineData("WOMPI_PAYLOAD_INVALID", HttpStatusCode.BadRequest)]
    [InlineData("WOMPI_WEBHOOK_UNAVAILABLE", HttpStatusCode.ServiceUnavailable)]
    [InlineData("WOMPI_PAYLOAD_TOO_LARGE", HttpStatusCode.RequestEntityTooLarge)]
    [InlineData("WOMPI_EVENT_CONFLICT", HttpStatusCode.Conflict)]
    public async Task Receiver_failure_maps_to_http_status_without_echoing_sensitive_error(string code, HttpStatusCode status)
    {
        using var f = new Fixture();
        f.Receiver.Response = Result<WompiWebhookReceipt>.Fail("synthetic-sensitive-error-and-secret", code);
        using var response = await f.Post(Encoding.UTF8.GetBytes("synthetic-private-body"), ["synthetic-private-signature"]);
        response.StatusCode.Should().Be(status);
        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().Contain(code).And.NotContain("synthetic-sensitive").And.NotContain("synthetic-private");
    }

    [Fact]
    public async Task Pending_verification_returns_503_with_committed_receipt_reference()
    {
        using var f = new Fixture();
        var receipt = Guid.NewGuid();
        f.Receiver.Response = Result<WompiWebhookReceipt>.FailWithValue(new(receipt, "PROCESSING"), "Internal details", "WOMPI_VERIFICATION_PENDING");
        using var response = await f.Post(Encoding.UTF8.GetBytes("{}"), ["synthetic-hash"]);
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("receiptId").GetGuid().Should().Be(receipt);
        body.RootElement.GetProperty("code").GetString().Should().Be("WOMPI_VERIFICATION_PENDING");
    }

    [Fact]
    public async Task Durable_quarantine_returns_202_with_reconciliation_status()
    {
        using var f = new Fixture();
        f.Receiver.Response = Result<WompiWebhookReceipt>.Ok(new(Guid.NewGuid(), "REQUIRES_RECONCILIATION"));
        using var response = await f.Post(Encoding.UTF8.GetBytes("{}"), ["synthetic-hash"]);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await response.Content.ReadAsStringAsync()).Should().Contain("REQUIRES_RECONCILIATION");
    }

    [Fact]
    public async Task Unhandled_receiver_failure_does_not_acknowledge_or_expose_exception_content()
    {
        using var f = new Fixture();
        f.Receiver.Exception = new InvalidOperationException("synthetic-secret-stack-detail");
        using var response = await f.Post(Encoding.UTF8.GetBytes("{}"), ["synthetic-hash"]);
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).Should().Contain("WOMPI_VERIFICATION_PENDING").And.NotContain("synthetic-secret");
    }

    private sealed class Fixture : IDisposable
    {
        public FakeReceiver Receiver { get; } = new();
        private readonly IHost _host;
        private readonly HttpClient _client;
        public Fixture()
        {
            _host = new HostBuilder().ConfigureWebHost(web => web.UseTestServer().ConfigureServices(services =>
            {
                services.AddControllers().AddApplicationPart(typeof(WompiWebhookController).Assembly);
                services.AddSingleton<IWompiWebhookReceiver>(Receiver);
            }).Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            })).Start();
            _client = _host.GetTestClient();
        }
        public async Task<HttpResponseMessage> Post(byte[] body, string[] hashes, bool unknownLength = false)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/billing/webhooks/wompi")
            { Content = unknownLength ? new UnknownLengthContent(body) : new ByteArrayContent(body) };
            if (hashes.Length > 0) request.Headers.TryAddWithoutValidation("wompi_hash", hashes);
            return await _client.SendAsync(request);
        }
        public void Dispose() { _client.Dispose(); _host.Dispose(); }
    }
    private sealed class FakeReceiver : IWompiWebhookReceiver
    {
        public int Calls { get; private set; }
        public byte[] Body { get; private set; } = [];
        public string? Signature { get; private set; }
        public Exception? Exception { get; set; }
        public Result<WompiWebhookReceipt> Response { get; set; } = Result<WompiWebhookReceipt>.Ok(new(Guid.NewGuid(), "VERIFIED_SANDBOX"));
        public Task<Result<WompiWebhookReceipt>> ReceiveAsync(ReadOnlyMemory<byte> body, string signature, CancellationToken ct = default)
        {
            Calls++; Body = body.ToArray(); Signature = signature;
            if (Exception is not null) throw Exception;
            return Task.FromResult(Response);
        }
    }
    private sealed class UnknownLengthContent(byte[] body) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(body).AsTask();
    }
}
