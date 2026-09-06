using System.Collections.Concurrent;
using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Http.Resilience;
using NeoSTP.Infrastructure;
using NeoSTP.Infrastructure.Billing;

namespace NeoSTP.Tests.Unit.Billing;

/// <summary>
/// Uses the production Wompi IHttpClientFactory/resilience registration. Only the primary
/// transport is replaced, after inspecting the actual redirect/cookie configuration.
/// No host, DbContext, workers, credentials or socket transport is started or resolved.
/// </summary>
public sealed class WompiTransportSafetyTests
{
    [Theory]
    [InlineData("https://api.example.invalid/EnlacePago")]
    [InlineData("https://identity.example.invalid/connect/token")]
    public async Task Real_registration_does_not_retry_checkout_or_oauth_post_on_503(string endpoint)
    {
        var transport = new ScriptedTransport(HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK);
        var filter = new CapturePrimaryTransport(transport);
        await using var services = BuildServices(filter);
        using var client = services.GetRequiredService<IHttpClientFactory>().CreateClient(WompiBillingProvider.HttpClientName);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var payload = new StringContent("synthetic-request-without-credentials");

        using var response = await client.PostAsync(endpoint, payload, deadline.Token);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable,
            "a later scripted success must not be reached by replaying an unsafe POST");
        transport.Attempts.Should().ContainSingle();
        transport.Attempts.Single().Should().Be(new Attempt("POST", endpoint, "synthetic-request-without-credentials"));
        AssertProductionHandler(filter);
    }

    [Fact]
    public async Task Real_registration_retries_read_only_get_after_transient_503()
    {
        const string endpoint = "https://api.example.invalid/EnlacePago/synthetic-reference";
        var transport = new ScriptedTransport(HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK);
        var filter = new CapturePrimaryTransport(transport);
        await using var services = BuildServices(filter);
        using var client = services.GetRequiredService<IHttpClientFactory>().CreateClient(WompiBillingProvider.HttpClientName);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        using var response = await client.GetAsync(endpoint, deadline.Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        transport.Attempts.Should().HaveCount(2, "the real retry predicate must still permit read-only verification");
        transport.Attempts.Should().OnlyContain(a => a.Method == "GET" && a.Url == endpoint && a.Body == string.Empty);
        AssertProductionHandler(filter);
    }

    private static ServiceProvider BuildServices(CapturePrimaryTransport filter)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            // Required only to register services. No DbContext or SQL client is ever resolved.
            ["ConnectionStrings:NeoStpDb"] = "Server=synthetic.invalid;Database=NeverOpened;Integrated Security=true;TrustServerCertificate=true",
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);
        // Reduce test duration only. Keep production retry predicate, number of attempts,
        // timeouts, circuit breaker and the actual delegating-handler pipeline unchanged.
        services.PostConfigureAll<HttpStandardResilienceOptions>(options =>
        {
            options.Retry.Delay = TimeSpan.FromMilliseconds(1);
            options.Retry.UseJitter = false;
        });
        services.AddSingleton<IHttpMessageHandlerBuilderFilter>(filter);
        return services.BuildServiceProvider();
    }

    private static void AssertProductionHandler(CapturePrimaryTransport filter)
    {
        filter.Inspections.Should().Be(1);
        filter.PrimaryHandlerType.Should().Be(typeof(HttpClientHandler));
        filter.AllowAutoRedirect.Should().BeFalse("a redirect is an independent way to retransmit a POST");
        filter.UseCookies.Should().BeFalse("the shared machine-to-machine client must not retain provider cookies");
    }

    private sealed class CapturePrimaryTransport(ScriptedTransport transport) : IHttpMessageHandlerBuilderFilter
    {
        public int Inspections { get; private set; }
        public Type? PrimaryHandlerType { get; private set; }
        public bool? AllowAutoRedirect { get; private set; }
        public bool? UseCookies { get; private set; }

        public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next)
            => builder =>
            {
                next(builder);
                if (builder.Name != WompiBillingProvider.HttpClientName) return;

                // Inspect AFTER production ConfigurePrimaryHttpMessageHandler runs. Replacing
                // through another builder configuration would otherwise erase the evidence.
                var primary = builder.PrimaryHandler;
                PrimaryHandlerType = primary.GetType();
                if (primary is HttpClientHandler actual)
                {
                    AllowAutoRedirect = actual.AllowAutoRedirect;
                    UseCookies = actual.UseCookies;
                }
                Inspections++;
                builder.PrimaryHandler = transport;
                primary.Dispose(); // It never sent a request; no socket can escape the fixture.
            };
    }

    private sealed record Attempt(string Method, string Url, string Body);

    private sealed class ScriptedTransport(params HttpStatusCode[] statuses) : HttpMessageHandler
    {
        private readonly ConcurrentQueue<HttpStatusCode> _statuses = new(statuses);
        public ConcurrentQueue<Attempt> Attempts { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(ct);
            Attempts.Enqueue(new(request.Method.Method, request.RequestUri!.AbsoluteUri, body));
            if (!_statuses.TryDequeue(out var status))
                throw new InvalidOperationException("Unexpected transport attempt beyond the synthetic response script.");
            return new HttpResponseMessage(status)
            {
                RequestMessage = request,
                Content = new StringContent("{\"synthetic\":true}"),
            };
        }
    }
}
