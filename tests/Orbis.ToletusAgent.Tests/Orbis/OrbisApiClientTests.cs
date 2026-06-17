using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orbis.ToletusAgent.Configuration;
using Orbis.ToletusAgent.Health;
using Orbis.ToletusAgent.Orbis;

namespace Orbis.ToletusAgent.Tests.Orbis;

public class OrbisApiClientTests
{
    private static readonly AccessAttemptRequest SampleRequest = new(
        TransactionId: "toletus-7218-1739123456789",
        Direction: "IN",
        DeviceCode: "CATRACA-01",
        CredentialType: "ENROLLID",
        CredentialValue: "50",
        RawPayload: null);

    [Fact]
    public async Task ValidateAccessAsync_returns_decision_on_success()
    {
        var handler = new QueueHttpMessageHandler(_ =>
            JsonResponse(HttpStatusCode.OK, """
                {
                  "authorized": true,
                  "message": "Acesso liberado",
                  "student_name": "João",
                  "status_pagamento": "em_dia"
                }
                """));

        var client = CreateClient(handler);

        var decision = await client.ValidateAccessAsync(SampleRequest);

        Assert.True(decision.Authorized);
        Assert.Equal("João", decision.StudentName);
        Assert.Equal("/api/turnstile/access-attempt", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal("test-api-key", handler.Requests[0].Headers.GetValues("x-api-key").Single());
    }

    [Fact]
    public async Task ValidateAccessAsync_does_not_retry_on_401()
    {
        var handler = new QueueHttpMessageHandler(_ =>
            JsonResponse(HttpStatusCode.Unauthorized, """{ "error": "invalid key" }"""));

        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<OrbisApiException>(
            () => client.ValidateAccessAsync(SampleRequest));

        Assert.Equal(401, exception.StatusCode);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ValidateAccessAsync_retries_on_503()
    {
        var attempt = 0;
        var handler = new QueueHttpMessageHandler(_ =>
        {
            attempt++;
            return attempt == 1
                ? JsonResponse(HttpStatusCode.ServiceUnavailable, """{ "error": "busy" }""")
                : JsonResponse(HttpStatusCode.OK, """{ "authorized": true, "message": "ok" }""");
        });

        var client = CreateClient(handler, maxRetries: 1, retryBackoffMs: [1]);

        var decision = await client.ValidateAccessAsync(SampleRequest);

        Assert.True(decision.Authorized);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task ValidateAccessAsync_uses_legacy_endpoint_when_configured()
    {
        var handler = new QueueHttpMessageHandler(_ =>
            JsonResponse(HttpStatusCode.OK, """{ "autorizado": true, "mensagem": "ok" }"""));

        var client = CreateClient(handler, useV2Endpoint: false);

        await client.ValidateAccessAsync(SampleRequest);

        Assert.Equal("/api/verificar-acesso-catraca", handler.Requests[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task ValidateAccessAsync_throws_on_timeout_without_retry()
    {
        var handler = new DelayedHttpMessageHandler(TimeSpan.FromSeconds(5));

        var client = CreateClient(handler, maxRetries: 0, timeoutMs: 100);

        var exception = await Assert.ThrowsAsync<OrbisApiException>(
            () => client.ValidateAccessAsync(SampleRequest));

        Assert.Null(exception.StatusCode);
        Assert.Contains("timed out", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(handler.Requests);
    }

    private static OrbisApiClient CreateClient(
        HttpMessageHandler handler,
        bool useV2Endpoint = true,
        int maxRetries = 0,
        int[]? retryBackoffMs = null,
        int timeoutMs = 2000)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://orbisfit.test/")
        };

        return new OrbisApiClient(
            httpClient,
            Options.Create(new OrbisOptions
            {
                ApiBaseUrl = "https://orbisfit.test",
                ApiKey = "test-api-key",
                DeviceCode = "CATRACA-01",
                UseV2Endpoint = useV2Endpoint,
                TimeoutMs = timeoutMs,
                MaxRetries = maxRetries,
                RetryBackoffMs = retryBackoffMs ?? [1]
            }),
            Options.Create(new ToletusOptions
            {
                Ip = "192.168.0.220",
                SerialNumber = "7218"
            }),
            new AgentHealthState(),
            NullLogger<OrbisApiClient>.Instance);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class QueueHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public QueueHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_responder(request));
        }
    }

    private sealed class DelayedHttpMessageHandler : HttpMessageHandler
    {
        private readonly TimeSpan _delay;

        public DelayedHttpMessageHandler(TimeSpan delay) => _delay = delay;

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            await Task.Delay(_delay, cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "authorized": true }""", Encoding.UTF8, "application/json")
            };
        }
    }
}
