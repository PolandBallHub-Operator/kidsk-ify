namespace KidsK_ify;

internal sealed class AdminForm : Form
{
    private readonly AdbClient _adb;
    private readonly TextBox _output = new() { Multiline = true, ScrollBars = ScrollBars.Both, ReadOnly = true, Dock = DockStyle.Fill, Font = new Font("Consolas", 9) };
    private readonly TextBox _filePath = new() { Text = "/sdcard", Width = 320 };
    private readonly TextBox _fileTarget = new() { Text = "/sdcard/target", Width = 220 };
    private readonly TextBox _package = new() { Width = 260 };
    public AdminForm(AdbClient adb)
    {
        _adb = adb; Text = "KidsK-ify — ADB管理ツール"; StartPosition = FormStartPosition.CenterParent; Size = new Size(1000, 700); MinimumSize = new Size(760, 500);
        var tabs = new TabControl { Dock = DockStyle.Fill }; tabs.TabPages.Add(BuildCommandTab()); tabs.TabPages.Add(BuildPropertiesTab()); tabs.TabPages.Add(BuildFileTab()); tabs.TabPages.Add(BuildPackageTab()); Controls.Add(tabs);
    }
    private TabPage BuildCommandTab()
    {
        var page = new TabPage("ADB / Shellコマンド"); var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, WrapContents = false, Padding = new Padding(6) }; var input = new TextBox { Width = 700, PlaceholderText = "例: shell dumpsys battery / devices / reboot" }; var run = new Button { Text = "実行", Width = 90 }; run.Click += async (_, _) => { await ExecuteAsync(input.Text.StartsWith("shell ", StringComparison.OrdinalIgnoreCase) ? input.Text : input.Text, input.Text.StartsWith("shell ", StringComparison.OrdinalIgnoreCase)); }; top.Controls.Add(input); top.Controls.Add(run); page.Controls.Add(_output); page.Controls.Add(top); input.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await ExecuteAsync(input.Text, input.Text.StartsWith("shell ", StringComparison.OrdinalIgnoreCase)); } }; return page;
    }
    private TabPage BuildPropertiesTab()
    {
        var page = new TabPage("getprop"); var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, Padding = new Padding(6) }; var refresh = new Button { Text = "getpropを取得", Width = 130 }; refresh.Click += async (_, _) => await RunAndShowAsync(() => _adb.GetPropertiesAsync()); top.Controls.Add(refresh); page.Controls.Add(_output); page.Controls.Add(top); return page;
    }
    private TabPage BuildFileTab()
    {
        var page = new TabPage("ADB File Manager"); var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 80, Padding = new Padding(6), WrapContents = true }; top.Controls.Add(new Label { Text = "リモートパス:", AutoSize = true, Padding = new Padding(0, 7, 0, 0) }); top.Controls.Add(_filePath);
        top.Controls.Add(new Label { Text = "移動先:", AutoSize = true, Padding = new Padding(4, 7, 0, 0) }); top.Controls.Add(_fileTarget);
        foreach (var item in new[] { ("ls", "ls -la {0}"), ("cd", "cd {0} && pwd"), ("mkdir", "mkdir -p {0}"), ("rm", "rm -rf {0}"), ("mv", "mv {0} {1}") }) { var b = new Button { Text = item.Item1, Width = 70, Tag = item.Item2 }; b.Click += async (_, _) => await FileCommandAsync((string)b.Tag); top.Controls.Add(b); }
        var pull = new Button { Text = "受信 (pull)", Width = 100 }; pull.Click += async (_, _) => await PullAsync(); var push = new Button { Text = "送信 (push)", Width = 100 }; push.Click += async (_, _) => await PushAsync(); top.Controls.Add(pull); top.Controls.Add(push); page.Controls.Add(_output); page.Controls.Add(top); return page;
    }
    private TabPage BuildPackageTab()
    {
        var page = new TabPage("Package Manager"); var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 85, Padding = new Padding(6), WrapContents = true }; var list = new Button { Text = "pm list packages", Width = 130 }; list.Click += async (_, _) => await RunAndShowAsync(() => _adb.ListPackagesAsync("")); var system = new Button { Text = "システム含む一覧", Width = 130 }; system.Click += async (_, _) => await RunAndShowAsync(() => _adb.ListPackagesAsync("-s")); top.Controls.Add(list); top.Controls.Add(system); top.Controls.Add(new Label { Text = "パッケージ名:", AutoSize = true, Padding = new Padding(8, 7, 0, 0) }); top.Controls.Add(_package);
        var install = new Button { Text = "install", Width = 80 }; install.Click += async (_, _) => await InstallAsync(false); var replace = new Button { Text = "install -r", Width = 90 }; replace.Click += async (_, _) => await InstallAsync(true); var uninstall = new Button { Text = "uninstall", Width = 90 }; uninstall.Click += async (_, _) => await PackageActionAsync(() => _adb.UninstallAsync(_package.Text)); var enable = new Button { Text = "enable", Width = 75 }; enable.Click += async (_, _) => await PackageActionAsync(() => _adb.SetPackageEnabledAsync(_package.Text, true)); var disable = new Button { Text = "disable-user", Width = 105 }; disable.Click += async (_, _) => await PackageActionAsync(() => _adb.SetPackageEnabledAsync(_package.Text, false)); foreach (var b in new[] { install, replace, uninstall, enable, disable }) top.Controls.Add(b); page.Controls.Add(_output); page.Controls.Add(top); return page;
    }
    private async Task ExecuteAsync(string text, bool shell) { if (string.IsNullOrWhiteSpace(text)) return; await RunAndShowAsync(() => shell ? _adb.RunShellAsync(text[6..].Trim()) : _adb.RunAdbAsync(text)); }
    private async Task FileCommandAsync(string format) { var cmd = format.Contains("{1}", StringComparison.Ordinal) ? string.Format(format, EscapeShell(_filePath.Text), EscapeShell(_fileTarget.Text)) : string.Format(format, EscapeShell(_filePath.Text)); await RunAndShowAsync(() => _adb.RunShellAsync(cmd)); }
    private static string EscapeShell(string value) => "'" + value.Replace("'", "'\\''") + "'";
    private async Task PullAsync() { using var d = new FolderBrowserDialog { Description = "保存先フォルダーを選択" }; if (d.ShowDialog(this) != DialogResult.OK) return; await RunAndShowAsync(() => _adb.PullAsync(_filePath.Text, d.SelectedPath)); }
    private async Task PushAsync() { using var d = new OpenFileDialog(); if (d.ShowDialog(this) != DialogResult.OK) return; await RunAndShowAsync(() => _adb.PushAsync(d.FileName, _filePath.Text)); }
    private async Task InstallAsync(bool replace) { using var d = new OpenFileDialog { Filter = "Android package (*.apk)|*.apk|すべてのファイル|*.*" }; if (d.ShowDialog(this) != DialogResult.OK) return; await RunAndShowAsync(() => _adb.InstallAsync(d.FileName, replace)); }
    private async Task PackageActionAsync(Func<Task<AdbClient.CommandResult>> action) { if (string.IsNullOrWhiteSpace(_package.Text)) { MessageBox.Show(this, "パッケージ名を入力してください。"); return; } await RunAndShowAsync(action); }
    private async Task RunAndShowAsync(Func<Task<AdbClient.CommandResult>> action) { try { var r = await action(); _output.Text = r.StandardOutput + (string.IsNullOrWhiteSpace(r.StandardError) ? "" : "\r\n[stderr]\r\n" + r.StandardError); } catch (Exception ex) { _output.Text = ex.ToString(); } }
}
