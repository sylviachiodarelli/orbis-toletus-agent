namespace Orbis.ToletusAgent.Setup;

public sealed class InstallForm : Form
{
    private readonly Label _statusLabel;
    private readonly Button _installButton;
    private readonly ProgressBar _progressBar;

    public InstallForm()
    {
        Text = "Orbis Toletus Agent — Instalação";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(520, 280);

        var title = new Label
        {
            Text = "Instalar Orbis Toletus Agent",
            Font = new Font(Font.FontFamily, 14, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(24, 20)
        };

        var description = new Label
        {
            Text = "Este assistente instala o agente no PC da recepção (mesma rede da catraca Toletus).\r\n\r\n" +
                   "Após instalar, configure a API key e o IP da catraca em http://127.0.0.1:5080",
            AutoSize = false,
            Size = new Size(472, 90),
            Location = new Point(24, 56)
        };

        _progressBar = new ProgressBar
        {
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 30,
            Visible = false,
            Location = new Point(24, 156),
            Size = new Size(472, 18)
        };

        _statusLabel = new Label
        {
            Text = "Pronto para instalar.",
            AutoSize = false,
            Size = new Size(472, 40),
            Location = new Point(24, 182)
        };

        _installButton = new Button
        {
            Text = "Instalar",
            Size = new Size(120, 34),
            Location = new Point(376, 224)
        };
        _installButton.Click += async (_, _) => await InstallAsync().ConfigureAwait(true);

        var cancelButton = new Button
        {
            Text = "Fechar",
            Size = new Size(120, 34),
            Location = new Point(248, 224),
            DialogResult = DialogResult.Cancel
        };

        Controls.Add(title);
        Controls.Add(description);
        Controls.Add(_progressBar);
        Controls.Add(_statusLabel);
        Controls.Add(_installButton);
        Controls.Add(cancelButton);

        AcceptButton = _installButton;
        CancelButton = cancelButton;
    }

    private async Task InstallAsync()
    {
        _installButton.Enabled = false;
        _progressBar.Visible = true;

        var progress = new Progress<string>(message => _statusLabel.Text = message);

        try
        {
            await Task.Run(() => AgentWindowsInstaller.InstallFromEmbeddedPayload(progress)).ConfigureAwait(true);
            _statusLabel.Text = "Instalação concluída. Abrindo configuração...";
            AgentWindowsInstaller.OpenSetupUi();
            MessageBox.Show(
                "Orbis Toletus Agent instalado com sucesso.\r\n\r\n" +
                "1. Crie a senha de administrador local\r\n" +
                "2. Informe API key, código do dispositivo e IP da catraca\r\n" +
                "3. Salve e verifique SDK Conectado no painel",
                "Instalação concluída",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            Close();
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Falha na instalação.";
            MessageBox.Show(
                ex.Message,
                "Erro na instalação",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            _installButton.Enabled = true;
            _progressBar.Visible = false;
        }
    }
}
