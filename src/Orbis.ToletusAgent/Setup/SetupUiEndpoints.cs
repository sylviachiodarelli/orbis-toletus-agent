using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Orbis.ToletusAgent.Configuration;
using Orbis.ToletusAgent.Core;
using Orbis.ToletusAgent.Health;
using Orbis.ToletusAgent.Status;
using Orbis.ToletusAgent.Toletus;

namespace Orbis.ToletusAgent.Setup;

public static class SetupUiEndpoints
{
    public const string SessionCookieName = "orbis_agent_session";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static void MapSetupUi(this WebApplication app)
    {
        var agentOptions = app.Services.GetRequiredService<IOptions<AgentOptions>>().Value;
        if (!agentOptions.StatusUiEnabled)
        {
            return;
        }

        app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments("/api") || IsPublicApiPath(context.Request.Path))
            {
                await next().ConfigureAwait(false);
                return;
            }

            var sessions = context.RequestServices.GetRequiredService<SetupSessionService>();
            var signed = context.Request.Cookies[SessionCookieName];
            if (sessions.TryValidateSignedCookie(signed, out _))
            {
                await next().ConfigureAwait(false);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "unauthorized" }).ConfigureAwait(false);
        });

        app.MapGet("/login", () => Results.Content(AppPageHtml, "text/html; charset=utf-8"));
        app.MapGet("/", () => Results.Content(AppPageHtml, "text/html; charset=utf-8"));
        app.MapGet("/setup", () => Results.Content(AppPageHtml, "text/html; charset=utf-8"));
        app.MapGet("/dashboard", () => Results.Content(AppPageHtml, "text/html; charset=utf-8"));

        app.MapGet("/api/setup/state", GetSetupState);
        app.MapPost("/api/setup/create-password", CreatePassword);
        app.MapPost("/api/setup/login", Login);
        app.MapPost("/api/setup/logout", Logout);
        app.MapGet("/api/setup/config", GetConfig);
        app.MapPost("/api/setup/config", SaveConfig);
        app.MapPost("/api/setup/test-orbis", TestOrbis);
        app.MapPost("/api/setup/test-toletus", TestToletus);
        app.MapGet("/api/status", BuildStatusResponse);
        app.MapPost("/api/agent/reconnect-turnstile", ReconnectTurnstile);
        app.MapPost("/api/agent/restart", RestartAgent);
    }

    private static bool IsPublicApiPath(PathString path) =>
        path.StartsWithSegments("/api/setup/state")
        || path.StartsWithSegments("/api/setup/create-password")
        || path.StartsWithSegments("/api/setup/login");

    private static IResult GetSetupState(
        SetupAuthStore authStore,
        IAgentConfigurationService configurationService)
    {
        return Results.Json(new
        {
            hasPassword = authStore.HasPassword(),
            isConfigured = configurationService.IsConfigured
        }, JsonOptions);
    }

    private static IResult CreatePassword(
        HttpContext context,
        SetupAuthStore authStore,
        SetupSessionService sessions,
        CreatePasswordRequest request)
    {
        if (authStore.HasPassword())
        {
            return Results.BadRequest(new { error = "Senha de administrador já configurada." });
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            return Results.BadRequest(new { error = "A senha deve ter pelo menos 8 caracteres." });
        }

        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
        {
            return Results.BadRequest(new { error = "As senhas não coincidem." });
        }

        authStore.CreatePassword(request.Password);
        SetSessionCookie(context, sessions);
        return Results.Ok(new { ok = true });
    }

    private static IResult Login(
        HttpContext context,
        SetupAuthStore authStore,
        SetupSessionService sessions,
        LoginRequest request)
    {
        if (!authStore.HasPassword())
        {
            return Results.BadRequest(new { error = "Crie a senha de administrador primeiro." });
        }

        if (!authStore.VerifyPassword(request.Password))
        {
            return Results.Unauthorized();
        }

        SetSessionCookie(context, sessions);
        return Results.Ok(new { ok = true });
    }

    private static IResult Logout(HttpContext context, SetupSessionService sessions)
    {
        var signed = context.Request.Cookies[SessionCookieName];
        if (sessions.TryValidateSignedCookie(signed, out var token))
        {
            sessions.RevokeSession(token);
        }

        context.Response.Cookies.Delete(SessionCookieName);
        return Results.Ok(new { ok = true });
    }

    private static IResult GetConfig(IAgentConfigurationService configurationService)
    {
        var setup = configurationService.GetCurrentSetup();
        return Results.Json(new
        {
            apiBaseUrl = setup.ApiBaseUrl,
            apiKey = setup.ApiKey,
            deviceCode = setup.DeviceCode,
            toletusIp = setup.ToletusIp,
            toletusSerialNumber = setup.ToletusSerialNumber ?? string.Empty
        }, JsonOptions);
    }

    private static async Task<IResult> SaveConfig(
        IAgentConfigurationService configurationService,
        AgentSetupDto setup,
        CancellationToken cancellationToken)
    {
        var validation = configurationService.Validate(setup);
        if (!validation.Success)
        {
            return Results.BadRequest(new { errors = validation.Errors });
        }

        await configurationService.SaveAsync(setup, cancellationToken).ConfigureAwait(false);
        return Results.Ok(new
        {
            ok = true,
            message = "Configuração salva. A conexão com a catraca será atualizada automaticamente em alguns segundos."
        });
    }

    private static async Task<IResult> TestOrbis(
        SetupConnectionTester tester,
        AgentSetupDto setup,
        CancellationToken cancellationToken)
    {
        var result = await tester.TestOrbisAsync(
            setup.ApiBaseUrl,
            setup.ApiKey,
            setup.DeviceCode,
            setup.ToletusIp,
            cancellationToken).ConfigureAwait(false);

        return Results.Json(new { success = result.Success, message = result.Message }, JsonOptions);
    }

    private static async Task<IResult> TestToletus(
        SetupConnectionTester tester,
        TestToletusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await tester.TestToletusAsync(request.ToletusIp, request.Port, cancellationToken)
            .ConfigureAwait(false);

        return Results.Json(new { success = result.Success, message = result.Message }, JsonOptions);
    }

    private static IResult BuildStatusResponse(
        IToletusDeviceService deviceService,
        IAgentHealthState healthState,
        IOfflinePolicyCache offlinePolicyCache,
        IAgentActivityStore activityStore,
        IAgentConfigurationService configurationService,
        AgentRecoveryState recoveryState,
        IOptions<AgentOptions> agentOptions,
        IOptions<ToletusOptions> toletusOptions)
    {
        var agent = agentOptions.Value;
        var toletus = toletusOptions.Value;
        var (disconnectedSince, reconnectAttempts) = recoveryState.Snapshot();

        var payload = new
        {
            agentVersion = AgentVersion.Get(),
            isConfigured = configurationService.IsConfigured,
            turnstileIp = string.IsNullOrWhiteSpace(toletus.Ip) ? "não configurado" : toletus.Ip,
            sdkConnected = deviceService.IsConnected,
            firmwareVersion = deviceService.FirmwareVersion,
            serialNumber = deviceService.SerialNumber ?? toletus.SerialNumber,
            lastSuccessfulValidationAt = healthState.LastSuccessfulValidationAt,
            offlineMode = offlinePolicyCache.GetEffectiveOfflineMode(),
            healthFilePath = agent.HealthFilePath,
            selfHealingEnabled = agent.SelfHealingEnabled,
            lastRecoveryMessage = recoveryState.LastRecoveryMessage,
            lastRecoveryAt = recoveryState.LastRecoveryAt,
            sdkDisconnectedSince = disconnectedSince,
            reconnectAttempts,
            timestamp = DateTimeOffset.UtcNow,
            recentAccess = activityStore.GetRecent(50)
        };

        return Results.Json(payload, JsonOptions);
    }

    private static async Task<IResult> ReconnectTurnstile(
        IAgentRecoveryService recoveryService,
        CancellationToken cancellationToken)
    {
        var result = await recoveryService.ReconnectTurnstileAsync(cancellationToken).ConfigureAwait(false);
        return Results.Json(
            new { ok = result.Success, message = result.Message, sdkConnected = result.SdkConnected },
            JsonOptions);
    }

    private static IResult RestartAgent(
        IAgentRecoveryService recoveryService,
        IHostApplicationLifetime lifetime)
    {
        var result = recoveryService.ScheduleApplicationRestart();
        _ = Task.Run(async () =>
        {
            await Task.Delay(750).ConfigureAwait(false);
            lifetime.StopApplication();
        });

        return Results.Json(new { ok = true, message = result.Message }, JsonOptions);
    }

    private static void SetSessionCookie(HttpContext context, SetupSessionService sessions)
    {
        var token = sessions.CreateSession();
        var signed = sessions.SignSessionCookie(token);
        context.Response.Cookies.Append(SessionCookieName, signed, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = false,
            MaxAge = TimeSpan.FromHours(8),
            Path = "/"
        });
    }

    private sealed record CreatePasswordRequest(string Password, string ConfirmPassword);

    private sealed record LoginRequest(string Password);

    private sealed record TestToletusRequest(string ToletusIp, int Port);

    private const string AppPageHtml = """
        <!DOCTYPE html>
        <html lang="pt-BR">
        <head>
          <meta charset="utf-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1" />
          <title>Orbis Toletus Agent</title>
          <style>
            :root { color-scheme: dark; font-family: Segoe UI, system-ui, sans-serif; }
            body { margin: 0; background: #0f1419; color: #e7ecf3; }
            main { max-width: 720px; margin: 0 auto; padding: 24px; }
            .wide { max-width: 1100px; }
            h1 { margin: 0 0 8px; font-size: 1.6rem; }
            h2 { margin-top: 28px; font-size: 1.1rem; }
            p.muted, .muted { color: #8b98a5; font-size: 0.92rem; }
            .card { background: #1a2332; border: 1px solid #2f3b4d; border-radius: 12px; padding: 18px; margin-top: 16px; }
            label { display: block; margin: 12px 0 6px; color: #b7c3d0; font-size: 0.85rem; }
            input { width: 100%; box-sizing: border-box; padding: 10px 12px; border-radius: 8px; border: 1px solid #2f3b4d; background: #0f1419; color: #e7ecf3; }
            button, .btn { margin-top: 14px; margin-right: 8px; padding: 10px 16px; border: 0; border-radius: 8px; cursor: pointer; font-weight: 600; }
            .primary { background: #3b82f6; color: white; }
            .secondary { background: #243041; color: #dbe7f5; }
            .danger { background: #5b2b2b; color: #ffd4d4; }
            .ok { color: #3dd68c; }
            .bad { color: #ff6b6b; }
            .hidden { display: none; }
            .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 12px; margin: 20px 0; }
            .metric { background: #1a2332; border: 1px solid #2f3b4d; border-radius: 10px; padding: 14px; }
            .label { color: #8b98a5; font-size: 0.75rem; text-transform: uppercase; letter-spacing: 0.04em; }
            .value { margin-top: 6px; font-size: 1.05rem; font-weight: 600; }
            table { width: 100%; border-collapse: collapse; margin-top: 12px; font-size: 0.9rem; }
            th, td { text-align: left; padding: 10px 8px; border-bottom: 1px solid #2f3b4d; vertical-align: top; }
            th { color: #8b98a5; }
            .pill { display: inline-block; padding: 2px 8px; border-radius: 999px; font-size: 0.75rem; font-weight: 700; }
            .pill.granted { background: #163d2d; color: #3dd68c; }
            .pill.denied { background: #402020; color: #ff8f8f; }
            .pill.offline { background: #3a2f14; color: #f5c842; }
            .pill.error { background: #3a2030; color: #ff9ad5; }
            .toolbar { display: flex; gap: 8px; flex-wrap: wrap; margin-top: 12px; }
            .message { margin-top: 12px; padding: 10px 12px; border-radius: 8px; background: #243041; }
            .message.error { background: #402020; color: #ffc9c9; }
            .message.success { background: #163d2d; color: #b7f5d5; }
          </style>
        </head>
        <body>
          <main id="container" class="wide">
            <h1>Orbis Toletus Agent</h1>
            <p class="muted" id="subtitle">Carregando...</p>

            <section id="view-bootstrap" class="hidden">
              <div class="card">
                <h2>Primeiro acesso</h2>
                <p class="muted">Crie uma senha local para proteger a configuração deste computador.</p>
                <label for="bootstrap-password">Senha de administrador</label>
                <input id="bootstrap-password" type="password" autocomplete="new-password" />
                <label for="bootstrap-confirm">Confirmar senha</label>
                <input id="bootstrap-confirm" type="password" autocomplete="new-password" />
                <button class="primary" id="bootstrap-submit">Criar senha e continuar</button>
                <div id="bootstrap-message" class="message hidden"></div>
              </div>
            </section>

            <section id="view-login" class="hidden">
              <div class="card">
                <h2>Entrar</h2>
                <label for="login-password">Senha de administrador</label>
                <input id="login-password" type="password" autocomplete="current-password" />
                <button class="primary" id="login-submit">Entrar</button>
                <div id="login-message" class="message hidden"></div>
              </div>
            </section>

            <section id="view-setup" class="hidden">
              <div class="toolbar" id="setup-nav-toolbar">
                <button class="secondary hidden" id="back-from-setup">← Voltar</button>
              </div>
              <div class="card">
                <h2>Configuração</h2>
                <p class="muted">Copie a API key e o código do dispositivo em Orbisfit → Integrações → Catracas. O IP da catraca é local (ex.: 192.168.0.220).</p>
                <label for="api-base-url">URL da API Orbisfit</label>
                <input id="api-base-url" type="url" placeholder="https://orbisfit.com" />
                <label for="api-key">API key</label>
                <input id="api-key" type="password" autocomplete="off" />
                <label for="device-code">Código do dispositivo</label>
                <input id="device-code" type="text" placeholder="CATRACA-01" />
                <label for="toletus-ip">IP da catraca</label>
                <input id="toletus-ip" type="text" placeholder="192.168.0.220" />
                <label for="toletus-serial">Número de série (opcional)</label>
                <input id="toletus-serial" type="text" placeholder="Preenchido automaticamente após conectar" />
                <div class="toolbar">
                  <button class="secondary" id="test-orbis">Testar Orbisfit</button>
                  <button class="secondary" id="test-toletus">Testar catraca</button>
                  <button class="primary" id="save-config">Salvar configuração</button>
                </div>
                <div id="setup-message" class="message hidden"></div>
              </div>
            </section>

            <section id="view-dashboard" class="hidden">
              <div class="toolbar">
                <button class="secondary" id="goto-setup">Configuração</button>
                <button class="secondary" id="reconnect-turnstile">Reconectar catraca</button>
                <button class="secondary" id="restart-agent">Reiniciar aplicativo</button>
                <button class="danger" id="logout">Sair</button>
              </div>
              <div id="dashboard-message" class="message hidden"></div>
              <div class="grid" id="metrics"></div>
              <h2>Tentativas recentes</h2>
              <table>
                <thead>
                  <tr>
                    <th>Horário</th>
                    <th>Resultado</th>
                    <th>Credencial</th>
                    <th>Aluno</th>
                    <th>Transação</th>
                  </tr>
                </thead>
                <tbody id="access-rows">
                  <tr><td colspan="5" class="muted">Nenhuma tentativa registrada ainda.</td></tr>
                </tbody>
              </table>
            </section>
          </main>
          <script>
            const views = ["view-bootstrap", "view-login", "view-setup", "view-dashboard"];
            let setupFromDashboard = false;

            function showView(id) {
              views.forEach(name => document.getElementById(name).classList.toggle("hidden", name !== id));
            }

            function updateSetupBackButton() {
              document.getElementById("back-from-setup").classList.toggle("hidden", !setupFromDashboard);
            }

            async function goToDashboard() {
              setupFromDashboard = false;
              updateSetupBackButton();
              document.getElementById("setup-message").classList.add("hidden");
              showView("view-dashboard");
              window.history.replaceState(null, "", "/dashboard");
              await refreshDashboard();
            }

            function openSetupFromDashboard() {
              setupFromDashboard = true;
              updateSetupBackButton();
              showView("view-setup");
              window.history.replaceState(null, "", "/setup");
            }

            function showMessage(elementId, text, type) {
              const el = document.getElementById(elementId);
              el.textContent = text;
              el.classList.remove("hidden", "error", "success");
              if (type) el.classList.add(type);
            }

            async function api(path, options = {}) {
              const response = await fetch(path, {
                credentials: "same-origin",
                headers: { "Content-Type": "application/json", ...(options.headers || {}) },
                ...options
              });
              const data = response.status === 204 ? null : await response.json().catch(() => ({}));
              if (!response.ok) {
                const message = data?.error || data?.errors?.join(" ") || "Falha na requisição.";
                throw new Error(message);
              }
              return data;
            }

            function readSetupForm() {
              return {
                apiBaseUrl: document.getElementById("api-base-url").value.trim(),
                apiKey: document.getElementById("api-key").value.trim(),
                deviceCode: document.getElementById("device-code").value.trim(),
                toletusIp: document.getElementById("toletus-ip").value.trim(),
                toletusSerialNumber: document.getElementById("toletus-serial").value.trim()
              };
            }

            function fillSetupForm(config) {
              document.getElementById("api-base-url").value = config.apiBaseUrl || "";
              document.getElementById("api-key").value = config.apiKey || "";
              document.getElementById("device-code").value = config.deviceCode || "";
              document.getElementById("toletus-ip").value = config.toletusIp || "";
              document.getElementById("toletus-serial").value = config.toletusSerialNumber || "";
            }

            async function routeAfterAuth(state) {
              document.getElementById("subtitle").textContent = "Painel local do agente · v" + (window.__agentVersion || "");
              if (!state.isConfigured) {
                const config = await api("/api/setup/config");
                fillSetupForm(config);
                setupFromDashboard = false;
                updateSetupBackButton();
                showView("view-setup");
                return;
              }
              await goToDashboard();
            }

            async function bootstrapApp() {
              const state = await api("/api/setup/state");
              if (!state.hasPassword) {
                document.getElementById("subtitle").textContent = "Configure a senha de administrador para começar.";
                showView("view-bootstrap");
                return;
              }

              const path = window.location.pathname;
              if (path === "/login") {
                document.getElementById("subtitle").textContent = "Entre com a senha de administrador.";
                showView("view-login");
                return;
              }

              try {
                await routeAfterAuth(state);
              } catch (error) {
                document.getElementById("subtitle").textContent = "Entre com a senha de administrador.";
                showView("view-login");
              }
            }

            function fmtTime(value) {
              if (!value) return "—";
              return new Date(value).toLocaleString("pt-BR");
            }

            function pillClass(outcome) {
              if (outcome === "granted") return "granted";
              if (outcome === "denied") return "denied";
              if (outcome === "offline") return "offline";
              if (outcome === "error") return "error";
              return "debounced";
            }

            function showDashboardMessage(text, type) {
              showMessage("dashboard-message", text, type);
            }

            async function refreshDashboard() {
              const data = await api("/api/status");
              window.__agentVersion = data.agentVersion;
              document.getElementById("subtitle").textContent =
                "Atualizado em " + fmtTime(data.timestamp) + " · v" + data.agentVersion;
              const healing = data.selfHealingEnabled
                ? "automática ativa"
                : "desligada";
              document.getElementById("metrics").innerHTML = `
                <div class="metric"><div class="label">Catraca</div><div class="value">${data.turnstileIp}</div></div>
                <div class="metric"><div class="label">SDK</div><div class="value ${data.sdkConnected ? "ok" : "bad"}">${data.sdkConnected ? "Conectado" : "Desconectado"}</div></div>
                <div class="metric"><div class="label">Firmware</div><div class="value">${data.firmwareVersion || "n/a"}</div></div>
                <div class="metric"><div class="label">Serial</div><div class="value">${data.serialNumber || "n/a"}</div></div>
                <div class="metric"><div class="label">Offline</div><div class="value">${data.offlineMode}</div></div>
                <div class="metric"><div class="label">Recuperação</div><div class="value">${healing}</div></div>
                <div class="metric"><div class="label">Última validação OK</div><div class="value">${fmtTime(data.lastSuccessfulValidationAt)}</div></div>
              `;
              if (data.lastRecoveryMessage) {
                showDashboardMessage(
                  (data.lastRecoveryAt ? fmtTime(data.lastRecoveryAt) + " — " : "") + data.lastRecoveryMessage,
                  data.sdkConnected ? "success" : ""
                );
              }
              const rows = data.recentAccess || [];
              const tbody = document.getElementById("access-rows");
              if (!rows.length) {
                tbody.innerHTML = '<tr><td colspan="5" class="muted">Nenhuma tentativa registrada ainda.</td></tr>';
                return;
              }
              tbody.innerHTML = rows.map(item => `
                <tr>
                  <td>${fmtTime(item.timestamp)}</td>
                  <td><span class="pill ${pillClass(item.outcome)}">${item.outcome}</span><div class="muted">${item.message || ""}</div></td>
                  <td>${item.credentialType}<br><span class="muted">${item.credentialValueMasked}</span></td>
                  <td>${item.studentName || "—"}</td>
                  <td class="muted">${item.transactionId}</td>
                </tr>
              `).join("");
            }

            document.getElementById("bootstrap-submit").addEventListener("click", async () => {
              try {
                await api("/api/setup/create-password", {
                  method: "POST",
                  body: JSON.stringify({
                    password: document.getElementById("bootstrap-password").value,
                    confirmPassword: document.getElementById("bootstrap-confirm").value
                  })
                });
                const state = await api("/api/setup/state");
                await routeAfterAuth(state);
              } catch (error) {
                showMessage("bootstrap-message", error.message, "error");
              }
            });

            document.getElementById("login-submit").addEventListener("click", async () => {
              try {
                await api("/api/setup/login", {
                  method: "POST",
                  body: JSON.stringify({ password: document.getElementById("login-password").value })
                });
                const state = await api("/api/setup/state");
                await routeAfterAuth(state);
              } catch (error) {
                showMessage("login-message", error.message, "error");
              }
            });

            document.getElementById("test-orbis").addEventListener("click", async () => {
              try {
                const result = await api("/api/setup/test-orbis", {
                  method: "POST",
                  body: JSON.stringify(readSetupForm())
                });
                showMessage("setup-message", result.message, result.success ? "success" : "error");
              } catch (error) {
                showMessage("setup-message", error.message, "error");
              }
            });

            document.getElementById("test-toletus").addEventListener("click", async () => {
              try {
                const form = readSetupForm();
                const result = await api("/api/setup/test-toletus", {
                  method: "POST",
                  body: JSON.stringify({ toletusIp: form.toletusIp, port: 0 })
                });
                showMessage("setup-message", result.message, result.success ? "success" : "error");
              } catch (error) {
                showMessage("setup-message", error.message, "error");
              }
            });

            document.getElementById("save-config").addEventListener("click", async () => {
              try {
                await api("/api/setup/config", {
                  method: "POST",
                  body: JSON.stringify(readSetupForm())
                });
                await goToDashboard();
              } catch (error) {
                showMessage("setup-message", error.message, "error");
              }
            });

            document.getElementById("back-from-setup").addEventListener("click", async () => {
              await goToDashboard();
            });

            document.getElementById("goto-setup").addEventListener("click", async () => {
              const config = await api("/api/setup/config");
              fillSetupForm(config);
              openSetupFromDashboard();
            });

            document.getElementById("logout").addEventListener("click", async () => {
              await api("/api/setup/logout", { method: "POST", body: "{}" });
              window.location.href = "/login";
            });

            document.getElementById("reconnect-turnstile").addEventListener("click", async () => {
              try {
                const result = await api("/api/agent/reconnect-turnstile", { method: "POST", body: "{}" });
                showDashboardMessage(result.message, result.sdkConnected ? "success" : "error");
                await refreshDashboard();
              } catch (error) {
                showDashboardMessage(error.message, "error");
              }
            });

            document.getElementById("restart-agent").addEventListener("click", async () => {
              if (!window.confirm("Reiniciar o aplicativo? A tela pode ficar indisponível por até 1 minuto.")) {
                return;
              }

              showDashboardMessage("Reiniciando o agente... aguarde.", "success");
              try {
                await api("/api/agent/restart", { method: "POST", body: "{}" });
              } catch {
                // A conexão pode cair antes da resposta — isso é esperado.
              }

              let attempts = 0;
              const poll = window.setInterval(async () => {
                attempts += 1;
                try {
                  await fetch("/api/setup/state");
                  window.clearInterval(poll);
                  window.location.reload();
                } catch {
                  if (attempts >= 40) {
                    window.clearInterval(poll);
                    showDashboardMessage(
                      "Se a tela não voltar, peça ao suporte para reinstalar o agente.",
                      "error"
                    );
                  }
                }
              }, 3000);
            });

            bootstrapApp();
            setInterval(() => {
              if (!document.getElementById("view-dashboard").classList.contains("hidden")) {
                refreshDashboard().catch(() => {});
              }
            }, 3000);
          </script>
        </body>
        </html>
        """;
}

internal static class AgentVersion
{
    public static string Get() =>
        System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";
}
