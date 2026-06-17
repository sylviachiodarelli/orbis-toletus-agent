using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.ServiceProcess;

namespace Orbis.ToletusAgent.Setup;

public static class AgentWindowsInstaller
{
    public const string ServiceName = "OrbisToletusAgent";
    public const string DisplayName = "Orbis Toletus Agent";
    public const string InstallDir = @"C:\Program Files\Orbis\ToletusAgent";
    public const string SetupUiUrl = "http://127.0.0.1:5080";

    public static void InstallFromEmbeddedPayload(IProgress<string> progress)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("agent-payload.zip")
            ?? throw new InvalidOperationException("Pacote do agente não encontrado no instalador.");

        var tempDir = Path.Combine(Path.GetTempPath(), "OrbisToletusAgent-setup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            progress.Report("Extraindo arquivos...");
            var zipPath = Path.Combine(tempDir, "payload.zip");
            using (var file = File.Create(zipPath))
            {
                stream.CopyTo(file);
            }

            var extractDir = Path.Combine(tempDir, "payload");
            ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);
            InstallFromDirectory(extractDir, progress);
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // Best effort cleanup.
            }
        }
    }

    public static void InstallFromDirectory(string sourceDir, IProgress<string> progress)
    {
        var agentExe = Path.Combine(sourceDir, "Orbis.ToletusAgent.exe");
        if (!File.Exists(agentExe))
        {
            throw new FileNotFoundException("Orbis.ToletusAgent.exe não encontrado no pacote.", agentExe);
        }

        progress.Report("Copiando para Program Files...");
        Directory.CreateDirectory(InstallDir);
        CopyDirectory(sourceDir, InstallDir);

        var configPath = Path.Combine(InstallDir, "appsettings.json");
        var examplePath = Path.Combine(InstallDir, "appsettings.example.json");
        if (!File.Exists(configPath) && File.Exists(examplePath))
        {
            File.Copy(examplePath, configPath);
        }

        var installedExe = Path.Combine(InstallDir, "Orbis.ToletusAgent.exe");
        RemoveExistingService(progress);
        RegisterService(installedExe, progress);
        StartService(progress);
    }

    public static void OpenSetupUi()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = SetupUiUrl,
            UseShellExecute = true
        });
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        foreach (var directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, directory);
            Directory.CreateDirectory(Path.Combine(targetDir, relative));
        }

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            var destination = Path.Combine(targetDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }

    private static void RemoveExistingService(IProgress<string> progress)
    {
        using var existing = ServiceController.GetServices()
            .FirstOrDefault(service => string.Equals(service.ServiceName, ServiceName, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            return;
        }

        progress.Report("Removendo serviço anterior...");
        try
        {
            if (existing.Status != ServiceControllerStatus.Stopped
                && existing.Status != ServiceControllerStatus.StopPending)
            {
                existing.Stop();
                existing.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Não foi possível parar o serviço existente.", ex);
        }

        RunSc($"delete {ServiceName}");
        Thread.Sleep(2000);
    }

    private static void RegisterService(string exePath, IProgress<string> progress)
    {
        progress.Report("Registrando serviço Windows...");
        RunSc($"create {ServiceName} binPath= \"{exePath}\" start= demand DisplayName= \"{DisplayName}\"");
        RunSc($"description {ServiceName} \"Ponte de acesso Toletus LiteNet2 para Orbisfit\"");

        var delayed = RunSc($"config {ServiceName} start= delayed-auto");
        if (delayed.ExitCode != 0)
        {
            RunSc($"config {ServiceName} start= auto");
        }
    }

    private static void StartService(IProgress<string> progress)
    {
        progress.Report("Iniciando serviço...");
        using var service = new ServiceController(ServiceName);
        if (service.Status == ServiceControllerStatus.Running)
        {
            return;
        }

        service.Start();
        service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
    }

    private static Process RunSc(string arguments)
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException($"Falha ao executar sc.exe {arguments}");

        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            var error = process.StandardError.ReadToEnd();
            var output = process.StandardOutput.ReadToEnd();
            throw new InvalidOperationException($"sc.exe falhou ({arguments}): {error} {output}".Trim());
        }

        return process;
    }
}
