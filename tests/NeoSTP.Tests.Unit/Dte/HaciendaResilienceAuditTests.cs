using System.Collections.Concurrent;
using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Infrastructure;
using NeoSTP.Infrastructure.Dte;
using Polly.Timeout;
using Xunit.Abstractions;

namespace NeoSTP.Tests.Unit.Dte;

/// <summary>
/// GL1D acceptance: fiscal POSTs are not retried automatically. Uses real registration and
/// IHttpClientFactory, replacing only the primary handler (no sockets, DB or host).
/// </summary>
public class HaciendaResilienceAuditTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("503")]
    [InlineData("network")]
    [InlineData("polly-timeout")]
    public async Task RealRegistration_DoesNotReplayReceptionPost(string failure)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            // AddInfrastructure requires this value; no DbContext is resolved or opened.
            ["ConnectionStrings:NeoStpDb"] = "Server=synthetic.invalid;Database=AuditNeverOpened;Integrated Security=true;TrustServerCertificate=true",
            ["Hacienda:Client"] = "Http",
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);
        services.Configure<HaciendaOptions>(options =>
        {
            options.PruebasBaseUrl = "https://synthetic.invalid";
            options.ProduccionBaseUrl = "https://synthetic.invalid";
            options.TimeoutSeconds = 55;
        });
        var transport = new SyntheticTransport(failure);
        // Preserve every production resilience option and handler; change transport only.
        services.AddHttpClient(HttpHaciendaReceptionClient.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => transport);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var reception = scope.ServiceProvider.GetRequiredService<IHaciendaReceptionClient>();
        reception.Should().BeOfType<HttpHaciendaReceptionClient>();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var document = DteFiscalIsolationTests.Documento();

        var result = await reception.EnviarAsync(new HaciendaReceptionRequest
        {
            AmbienteCodigo = DteAmbientes.Pruebas,
            Ambiente = "00",
            TipoDte = "01",
            IdEnvio = 123,
            Version = 1,
            CodigoGeneracion = document.CodigoGeneracion,
            Documento = DteFiscalIsolationTests.Jws(DteFiscalIsolationTests.Payload(document)),
            Token = "synthetic",
        }, deadline.Token);

        var attempts = transport.Attempts.ToArray();
        output.WriteLine($"GL1D: one EnviarAsync produced {attempts.Length} primary POST attempts; failure={failure}.");
        attempts.Should().ContainSingle("an uncertain fiscal POST must not be replayed automatically");
        attempts.Should().OnlyContain(attempt => attempt.Method == "POST"
            && attempt.Uri == "https://synthetic.invalid/fesv/recepciondte");
        attempts.Select(attempt => attempt.Body).Distinct().Should().ContainSingle("each attempt retransmits the same DTE payload");
        attempts[0].Body.Should().Contain(document.CodigoGeneracion);
        result.Success.Should().BeFalse();
        result.CodigoHttp.Should().Be(failure == "503" ? 503 : 0);
        result.ClasificaMsg.Should().Be(failure switch {
            "network" => "NETWORK_ERROR", "polly-timeout" => "TIMEOUT", _ => "ERROR_SERVIDOR"
        });
        if (failure == "polly-timeout") result.Estado.Should().Be("ENVIADO");
    }

    [Theory]
    [InlineData(HttpHaciendaReceptionClient.HttpClientName, "503")]
    [InlineData(HttpHaciendaReceptionClient.HttpClientName, "network")]
    [InlineData(HttpHaciendaReceptionClient.HttpClientName, "polly-timeout")]
    [InlineData(HttpHaciendaContingenciaClient.HttpClientName, "503")]
    [InlineData(HttpHaciendaContingenciaClient.HttpClientName, "network")]
    [InlineData(HttpHaciendaContingenciaClient.HttpClientName, "polly-timeout")]
    [InlineData(HttpHaciendaEventoClient.HttpClientName, "503")]
    [InlineData(HttpHaciendaEventoClient.HttpClientName, "network")]
    [InlineData(HttpHaciendaEventoClient.HttpClientName, "polly-timeout")]
    [InlineData(HttpHaciendaLoteClient.HttpClientName, "503")]
    [InlineData(HttpHaciendaLoteClient.HttpClientName, "network")]
    [InlineData(HttpHaciendaLoteClient.HttpClientName, "polly-timeout")]
    public async Task EveryFiscalNamedClient_DisablesRetry_ForResponseNetworkAndPolicyTimeout(string clientName, string failure)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:NeoStpDb"] = "Server=synthetic.invalid;Database=AuditNeverOpened;Integrated Security=true;TrustServerCertificate=true",
            ["Hacienda:Client"] = "Http"
        }).Build();
        var services = new ServiceCollection(); services.AddLogging(); services.AddInfrastructure(config);
        var transport = new SyntheticTransport(failure);
        services.AddHttpClient(clientName).ConfigurePrimaryHttpMessageHandler(() => transport);
        await using var provider = services.BuildServiceProvider();
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(clientName);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://synthetic.invalid/acceptance-only") {
            Content = new StringContent("{\"synthetic\":true}")
        };
        HttpResponseMessage? response = null;
        var exception = await Record.ExceptionAsync(async () => response = await client.SendAsync(request, deadline.Token));
        try {
            output.WriteLine($"{clientName}/{failure}: {transport.Attempts.Count} primary attempts, exception={exception?.GetType().Name ?? "none"}.");
            transport.Attempts.Should().ContainSingle("fiscal POSTs must not be replayed by resilience middleware");
            if (failure == "503") {
                exception.Should().BeNull(); response!.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            } else if (failure == "network") exception.Should().BeOfType<HttpRequestException>();
            else exception.Should().BeOfType<TimeoutRejectedException>();
        } finally { response?.Dispose(); }
    }

    private sealed record Attempt(string Method, string Uri, string Body);

    private sealed class SyntheticTransport(string failure) : HttpMessageHandler
    {
        internal ConcurrentQueue<Attempt> Attempts { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            Attempts.Enqueue(new Attempt(request.Method.Method, request.RequestUri!.AbsoluteUri, body));
            // Capture occurs before failure to represent an ambiguous post-transmission failure.
            // This handler has no inner/socket transport and cannot contact any endpoint.
            if (failure == "network") throw new HttpRequestException("Synthetic response lost after request capture.");
            // Simulates the exception emitted by a Polly timeout strategy immediately; no delay/socket.
            if (failure == "polly-timeout") throw new TimeoutRejectedException("Synthetic policy timeout after request capture.");
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("{\"descripcionMsg\":\"Synthetic service unavailable\"}"),
            };
        }
    }
}
