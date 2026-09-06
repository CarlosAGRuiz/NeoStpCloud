using NeoSTP.Application.Comunicaciones;
using NeoSTP.Application.Connect;
using NeoSTP.Application.Dte.Abstractions;

namespace ClientCertificationRunner;

public sealed class PilotTransport(Func<CancellationToken, Task> preflight) : IHttpClientFactory, IDisposable
{
    private readonly SocketsHttpHandler transport = new() { AllowAutoRedirect = false, UseCookies = false };
    private int receptions;
    private int authentications;
    public int ReceptionAttempts => Volatile.Read(ref receptions);
    public int AuthenticationAttempts => Volatile.Read(ref authentications);
    public HttpClient CreateClient(string name)
    {
        RunnerPolicy.Require(name is "HaciendaAuth" or "HaciendaReception", "HTTP_CLIENT_REJECTED");
        return new HttpClient(new Guard(this, preflight, transport), true);
    }
    public void Dispose() => transport.Dispose();
    private sealed class Guard(PilotTransport owner, Func<CancellationToken, Task> preflight, HttpMessageHandler inner) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            RunnerPolicy.Require(RunnerPolicy.AllowedEndpoint(request.Method, request.RequestUri), "HTTP_ENDPOINT_REJECTED");
            await preflight(ct);
            var count = request.RequestUri!.AbsolutePath == "/fesv/recepciondte" ? Interlocked.Increment(ref owner.receptions) : Interlocked.Increment(ref owner.authentications);
            RunnerPolicy.Require(count == 1, "HTTP_REPEAT_REJECTED");
            using var invoker = new HttpMessageInvoker(inner, false);
            return await invoker.SendAsync(request, ct);
        }
    }
}
public sealed class BlockedTenantEmail : ITenantEmailSender
{
    public Task<EmailSendResult> EnviarAsync(int empresaId, EmailMessage message, CancellationToken ct = default)
        => throw new RunnerRejected("PILOT_EMAIL_FORBIDDEN");
}
public sealed class BlockedWebhooks : IConnectWebhookDispatcher
{
    public Task DispatchAsync(ConnectDteEventoPayload payload, CancellationToken ct = default) => Task.CompletedTask;
    public Task DispatchNegocioAsync(ConnectEventoNegocioPayload payload, CancellationToken ct = default) => throw new RunnerRejected("PILOT_BUSINESS_WEBHOOK_FORBIDDEN");
    public Task<int> ProcesarPendientesAsync(CancellationToken ct = default) => throw new RunnerRejected("PILOT_WEBHOOK_DELIVERY_FORBIDDEN");
}
