using System.Diagnostics;
using System.Text;

namespace WydCdk.Server.ControlPanel;

internal sealed class MainForm : Form
{
    private readonly TextBox logBox = new();
    private readonly TextBox corePathBox = new();
    private readonly TextBox bindBox = new();
    private readonly NumericUpDown portBox = new() { Minimum = 1, Maximum = 65535, Value = 8281 };
    private readonly TextBox accountRootBox = new();
    private readonly TextBox accountDatabaseBox = new();
    private readonly TextBox donateDatabaseBox = new();
    private readonly ComboBox modeBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label statusLabel = new();
    private Process? serverProcess;

    public MainForm()
    {
        Text = "WYD CDK Server";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(980, 620);
        Size = new Size(1180, 720);
        BackColor = Color.FromArgb(24, 24, 28);
        ForeColor = Color.Gainsboro;

        bindBox.Text = "0.0.0.0";
        corePathBox.Text = Path.Combine(AppContext.BaseDirectory, "WydCdk.Server.Core.exe");
        accountRootBox.Text = Path.Combine(AppContext.BaseDirectory, "DBSRV", "run", "account");
        modeBox.Items.AddRange(["UP", "PVP"]);
        modeBox.SelectedIndex = 0;
        logBox.ReadOnly = true;
        logBox.Multiline = true;
        logBox.ScrollBars = ScrollBars.Both;
        logBox.Dock = DockStyle.Fill;
        logBox.BackColor = Color.FromArgb(12, 12, 14);
        logBox.ForeColor = Color.FromArgb(214, 232, 214);
        logBox.Font = new Font("Cascadia Mono", 9F);
        logBox.WordWrap = false;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = BackColor,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(root);

        root.Controls.Add(BuildNavigation(), 0, 0);
        root.Controls.Add(BuildServerPage(), 1, 0);
        FormClosed += (_, _) => StopServer();
        AppendLog("Painel WYD CDK iniciado. O núcleo ainda não foi iniciado.");
    }

    private Control BuildNavigation()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16), BackColor = Color.FromArgb(31, 31, 37) };
        var title = new Label
        {
            Text = "WYD CDK\nSERVER",
            AutoSize = true,
            Font = new Font("Segoe UI", 17F, FontStyle.Bold),
            ForeColor = Color.FromArgb(235, 190, 80),
            Location = new Point(16, 18),
        };
        panel.Controls.Add(title);

        var subtitle = new Label
        {
            Text = "Painel de controle e ferramentas",
            AutoSize = true,
            ForeColor = Color.Silver,
            Location = new Point(18, 78),
        };
        panel.Controls.Add(subtitle);

        var navigation = new FlowLayoutPanel
        {
            Location = new Point(12, 125),
            Width = 196,
            Height = 340,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent,
        };
        AddNavigationButton(navigation, "Servidor", true);
        AddNavigationButton(navigation, "Contas / MariaDB", false);
        AddNavigationButton(navigation, "Editor de XP", false);
        AddNavigationButton(navigation, "Editor de mobs", false);
        AddNavigationButton(navigation, "Editor de skills", false);
        AddNavigationButton(navigation, "Itens e drops", false);
        AddNavigationButton(navigation, "Mapas e eventos", false);
        panel.Controls.Add(navigation);

        var footer = new Label
        {
            Text = "C# port\nSandbox local",
            AutoSize = true,
            ForeColor = Color.Gray,
            Location = new Point(18, 545),
        };
        panel.Controls.Add(footer);
        return panel;
    }

    private void AddNavigationButton(Control parent, string text, bool active)
    {
        var button = new Button
        {
            Text = text,
            Width = 190,
            Height = 38,
            FlatStyle = FlatStyle.Flat,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 0, 0),
            BackColor = active ? Color.FromArgb(75, 61, 32) : Color.FromArgb(45, 45, 52),
            ForeColor = active ? Color.FromArgb(255, 215, 120) : Color.Gainsboro,
            FlatAppearance = { BorderSize = 0 },
            Margin = new Padding(0, 0, 0, 7),
        };
        if (!active)
        {
            button.Click += (_, _) => AppendLog($"Ferramenta '{text}' está reservada para a próxima etapa.");
        }
        parent.Controls.Add(button);
    }

    private Control BuildServerPage()
    {
        var page = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            RowCount = 3,
            ColumnCount = 1,
            BackColor = BackColor,
        };
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 205));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var header = new Panel { Dock = DockStyle.Fill };
        var heading = new Label
        {
            Text = "Servidor C#",
            AutoSize = true,
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            Location = new Point(0, 3),
        };
        header.Controls.Add(heading);
        statusLabel.Text = "● Parado";
        statusLabel.AutoSize = true;
        statusLabel.ForeColor = Color.FromArgb(220, 120, 120);
        statusLabel.Location = new Point(160, 9);
        header.Controls.Add(statusLabel);
        page.Controls.Add(header, 0, 0);

        page.Controls.Add(BuildConfiguration(), 0, 1);
        page.Controls.Add(BuildConsolePanel(), 0, 2);
        return page;
    }

    private Control BuildConfiguration()
    {
        var group = new GroupBox { Text = "Inicialização", Dock = DockStyle.Fill, ForeColor = Color.Gainsboro };
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 4, Padding = new Padding(8) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        AddField(grid, "Núcleo", corePathBox, 0, 0, 1, 3);
        AddField(grid, "Bind", bindBox, 0, 1, 1, 1);
        AddField(grid, "Porta", portBox, 2, 1, 3, 1);
        AddField(grid, "Accounts", accountRootBox, 0, 2, 1, 1);
        AddField(grid, "MariaDB auth", accountDatabaseBox, 2, 2, 3, 1);
        AddField(grid, "Donate DB", donateDatabaseBox, 0, 3, 1, 1);
        AddField(grid, "Modo", modeBox, 2, 3, 3, 1);
        group.Controls.Add(grid);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        var start = new Button { Text = "Iniciar", Width = 90, Height = 30 };
        start.Click += (_, _) => StartServer();
        var stop = new Button { Text = "Parar", Width = 90, Height = 30, Enabled = false };
        stop.Click += (_, _) => StopServer();
        var clear = new Button { Text = "Limpar log", Width = 90, Height = 30 };
        clear.Click += (_, _) => logBox.Clear();
        buttons.Controls.Add(start);
        buttons.Controls.Add(stop);
        buttons.Controls.Add(clear);
        grid.Controls.Add(buttons, 3, 0);
        grid.SetColumnSpan(buttons, 1);
        serverProcessChanged += running =>
        {
            start.Enabled = !running;
            stop.Enabled = running;
        };
        return group;
    }

    private static void AddField(TableLayoutPanel grid, string label, Control control, int column, int row, int labelColumn, int labelRow)
    {
        var caption = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 7, 3, 3) };
        grid.Controls.Add(caption, column, row);
        grid.Controls.Add(control, column + 1, row);
        control.Dock = DockStyle.Fill;
    }

    private Control BuildConsolePanel()
    {
        var group = new GroupBox { Text = "Console do núcleo", Dock = DockStyle.Fill, ForeColor = Color.Gainsboro, Padding = new Padding(8) };
        group.Controls.Add(logBox);
        return group;
    }

    private event Action<bool>? serverProcessChanged;

    private void StartServer()
    {
        if (serverProcess is not null && !serverProcess.HasExited)
        {
            AppendLog("O núcleo já está em execução.");
            return;
        }

        var executable = corePathBox.Text.Trim();
        if (!File.Exists(executable))
        {
            AppendLog($"Núcleo não encontrado: {executable}");
            MessageBox.Show(this, "Compile/publique o núcleo ou selecione o WydCdk.Server.Core.exe correto.", "Núcleo não encontrado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var startInfo = new ProcessStartInfo(executable)
        {
            WorkingDirectory = Path.GetDirectoryName(executable)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        startInfo.ArgumentList.Add("--bind");
        startInfo.ArgumentList.Add(bindBox.Text.Trim());
        startInfo.ArgumentList.Add("--port");
        startInfo.ArgumentList.Add(((int)portBox.Value).ToString());
        startInfo.ArgumentList.Add("--mode");
        startInfo.ArgumentList.Add(modeBox.SelectedItem?.ToString() ?? "UP");
        AddOptionalArgument(startInfo, "--accounts-root", accountRootBox.Text);
        AddOptionalArgument(startInfo, "--mariadb-account-config", accountDatabaseBox.Text);
        AddOptionalArgument(startInfo, "--donate-db-config", donateDatabaseBox.Text);

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) AppendLog(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) AppendLog("[erro] " + e.Data); };
        process.Exited += (_, _) =>
        {
            var exitCode = process.ExitCode;
            AppendLog($"Núcleo finalizado (código {exitCode}).");
            if (!IsDisposed)
            {
                BeginInvoke(() =>
                {
                    statusLabel.Text = "● Parado";
                    statusLabel.ForeColor = Color.FromArgb(220, 120, 120);
                    serverProcessChanged?.Invoke(false);
                });
            }
        };

        try
        {
            if (!process.Start()) throw new InvalidOperationException("Processo não iniciou.");
            serverProcess = process;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            statusLabel.Text = "● Rodando";
            statusLabel.ForeColor = Color.FromArgb(120, 220, 140);
            serverProcessChanged?.Invoke(true);
            AppendLog($"Núcleo iniciado: {Path.GetFileName(executable)}");
        }
        catch (Exception exception)
        {
            process.Dispose();
            AppendLog($"Falha ao iniciar núcleo: {exception.Message}");
        }
    }

    private static void AddOptionalArgument(ProcessStartInfo startInfo, string name, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            startInfo.ArgumentList.Add(name);
            startInfo.ArgumentList.Add(value.Trim());
        }
    }

    private void StopServer()
    {
        var process = serverProcess;
        serverProcess = null;
        if (process is null) return;
        try
        {
            if (!process.HasExited)
            {
                AppendLog("Solicitando parada cooperativa do núcleo...");
                process.StandardInput.WriteLine("stop");
                process.StandardInput.Flush();
                if (!process.WaitForExit(5000))
                {
                    AppendLog("O núcleo não encerrou em 5 segundos; usando encerramento forçado de fallback.");
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
            }
        }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
        process.Dispose();
        statusLabel.Text = "● Parado";
        statusLabel.ForeColor = Color.FromArgb(220, 120, 120);
        serverProcessChanged?.Invoke(false);
    }

    private void AppendLog(string line)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            BeginInvoke(() => AppendLog(line));
            return;
        }

        logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}");
        logBox.SelectionStart = logBox.TextLength;
        logBox.ScrollToCaret();
    }
}
