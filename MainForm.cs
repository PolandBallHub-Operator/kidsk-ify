using System.Drawing;
using System.Threading.Channels;

namespace KidsK_ify;

internal sealed class MainForm : Form
{
    private readonly AdbClient _adb;
    private readonly AspectRatioPanel _preview = new();
    private readonly ToolStripStatusLabel _status = new("ADBを確認しています…");
    private readonly ToolStripComboBox _mode = new();
    private readonly ToolStripComboBox _slots = new();
    private readonly ToolStripLabel _resolution = new("解像度: —");
    private readonly TextBox _text = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _restartLock = new(1, 1);
    private CancellationTokenSource? _streamCts;
    private Task? _streamTask;
    private Size _deviceSize;
    private bool _connected;
    private Point _pointerDown;
    private DateTime _pointerDownAt;
    private MouseButtons _pressedButton;
    private int SlotCount => int.TryParse(_slots.Text, out var n) ? Math.Clamp(n, 4, 10) : 4;
    private bool HighSpeed => _mode.SelectedIndex == 1;

    public MainForm()
    {
        Text = "KidsK-ify — Android リモート画面"; StartPosition = FormStartPosition.CenterScreen; MinimumSize = new Size(900, 650); Size = new Size(1200, 850);
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "KidsK-ify.ico");
            if (File.Exists(iconPath)) Icon = new Icon(iconPath);
        }
        catch { /* exeのApplicationIconを使用 */ }
        _adb = new AdbClient(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KidsK-ify", "captures"));
        BuildUi();
        Shown += (_, _) => _ = ConnectAsync();
        FormClosing += (_, _) => { _lifetime.Cancel(); _streamCts?.Cancel(); _preview.Screen.Image?.Dispose(); _lifetime.Dispose(); };
    }
    private void BuildUi()
    {
        var menu = new MenuStrip(); var device = new ToolStripMenuItem("デバイス");
        device.DropDownItems.Add("接続", null, async (_, _) => await ConnectAsync()); device.DropDownItems.Add("現在の表示画面を保存", null, async (_, _) => await SaveScreenAsync()); device.DropDownItems.Add(new ToolStripSeparator()); device.DropDownItems.Add("ADB管理ツール", null, (_, _) => new AdminForm(_adb).Show(this));
        var help = new ToolStripMenuItem("ヘルプ"); help.DropDownItems.Add("READMEを開く", null, (_, _) => MessageBox.Show(this, "adb.exeをPATHまたは本アプリと同じフォルダーに配置し、USBデバッグを許可してください。", "KidsK-ify")); menu.Items.Add(device); menu.Items.Add(help); MainMenuStrip = menu; Controls.Add(menu);
        var tool = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };
        tool.Items.Add(new ToolStripLabel("表示モード:")); _mode.Items.AddRange(new object[] { "通常（Legacy）", "高速モード" }); _mode.SelectedIndex = 0; _mode.SelectedIndexChanged += (_, _) => _ = RestartStreamAsync(); tool.Items.Add(_mode);
        tool.Items.Add(new ToolStripLabel("キャプチャ枚数:")); for (var i = 4; i <= 10; i++) _slots.Items.Add(i.ToString()); _slots.SelectedIndex = 0; _slots.SelectedIndexChanged += (_, _) => _ = RestartStreamAsync(); tool.Items.Add(_slots);
        tool.Items.Add(new ToolStripSeparator()); var connect = new ToolStripButton("接続"); connect.Click += async (_, _) => await ConnectAsync(); tool.Items.Add(connect); tool.Items.Add(new ToolStripSeparator()); tool.Items.Add(_resolution); Controls.Add(tool);
        _preview.Dock = DockStyle.Fill; _preview.Screen.MouseDown += PreviewMouseDown; _preview.Screen.MouseUp += PreviewMouseUp; _preview.Screen.MouseEnter += (_, _) => _preview.Screen.Focus(); Controls.Add(_preview);
        var bottom = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 82, ColumnCount = 3, RowCount = 2, Padding = new Padding(6) }; bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100)); bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        _text.Dock = DockStyle.Fill; _text.PlaceholderText = "送信する文字を入力…"; _text.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await SendTextAsync(); } }; bottom.Controls.Add(_text, 0, 0); var send = new Button { Text = "文字を送信", Dock = DockStyle.Fill }; send.Click += async (_, _) => await SendTextAsync(); bottom.Controls.Add(send, 1, 0);
        var nav = new FlowLayoutPanel { Dock = DockStyle.Fill }; var home = new Button { Text = "HOME", Width = 78 }; home.Click += async (_, _) => await RunInputAsync("HOME", ct => _adb.SendKeyAsync(3, ct)); var back = new Button { Text = "BACK", Width = 78 }; back.Click += async (_, _) => await RunInputAsync("BACK", ct => _adb.SendKeyAsync(4, ct)); nav.Controls.Add(home); nav.Controls.Add(back); bottom.Controls.Add(nav, 2, 0);
        var status = new StatusStrip(); status.Items.Add(_status); Controls.Add(status); Controls.Add(bottom); bottom.BringToFront(); status.BringToFront();
    }
    private async Task ConnectAsync()
    {
        try { _status.Text = "接続中…"; var size = await _adb.GetScreenSizeAsync(_lifetime.Token); _deviceSize = size; _preview.SetAspectRatio(size.Width, size.Height); _resolution.Text = $"解像度: {size.Width} × {size.Height}"; _connected = true; _status.Text = "接続済み — プレビューを操作できます"; await RestartStreamAsync(); }
        catch (Exception ex) when (ex is not OperationCanceledException) { _connected = false; _status.Text = "未接続 — " + FirstLine(ex.Message); }
    }
    private async Task SaveScreenAsync()
    {
        using var dialog = new SaveFileDialog { Filter = "PNG画像|*.png", FileName = "KidsK-ify-screen.png" }; if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { await _adb.DownloadCurrentScreenAsync(dialog.FileName, _lifetime.Token); _status.Text = "画面を保存しました: " + dialog.FileName; } catch (Exception ex) { _status.Text = "保存失敗 — " + FirstLine(ex.Message); }
    }
    private async Task RestartStreamAsync()
    {
        await _restartLock.WaitAsync(); try { _streamCts?.Cancel(); if (_streamTask is not null) try { await _streamTask; } catch { } _streamCts?.Dispose(); _streamCts = null; _streamTask = null; if (_connected && !_lifetime.IsCancellationRequested) { _streamCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token); var c = _streamCts; _streamTask = Task.Run(() => CaptureLoopAsync(c.Token)); } } finally { _restartLock.Release(); }
    }
    private async Task CaptureLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested) { try { if (HighSpeed) await RunHighSpeedAsync(ct); else await RunLegacyAsync(ct); } catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; } catch (Exception ex) { SetStatus("画面取得エラー — " + FirstLine(ex.Message)); await Task.Delay(500, ct); } }
    }
    private async Task RunLegacyAsync(CancellationToken ct) { const string name = "screenshot_legacy.png"; while (!ct.IsCancellationRequested) { try { await _adb.CaptureAsync(name, ct); using var b = _adb.LoadCapturedBitmap(name); ShowFrame(new Bitmap(b)); } finally { await _adb.DeleteCaptureAsync(name); } } }
    private async Task RunHighSpeedAsync(CancellationToken ct)
    {
        var count = SlotCount; var channel = Channel.CreateBounded<CapturedFrame>(count); var free = Enumerable.Range(0, count).Select(_ => new SemaphoreSlim(1, 1)).ToArray();
        async Task Produce() { var next = 0; try { while (!ct.IsCancellationRequested) { var slot = next; await free[slot].WaitAsync(ct); var name = $"screenshot{slot + 1}.png"; try { await _adb.CaptureAsync(name, ct); await channel.Writer.WriteAsync(new CapturedFrame(name, slot), ct); next = (next + 1) % count; } catch { free[slot].Release(); throw; } } } finally { channel.Writer.TryComplete(); } }
        async Task Consume() { await foreach (var f in channel.Reader.ReadAllAsync(ct)) { try { using var b = _adb.LoadCapturedBitmap(f.FileName); ShowFrame(new Bitmap(b)); } finally { await _adb.DeleteCaptureAsync(f.FileName); free[f.Slot].Release(); } } }
        try { await Task.WhenAll(Produce(), Consume()); } finally { foreach (var s in free) s.Dispose(); for (var i = 1; i <= count; i++) await _adb.DeleteCaptureAsync($"screenshot{i}.png"); }
    }
    private void ShowFrame(Bitmap b) { if (IsDisposed) { b.Dispose(); return; } try { BeginInvoke(new Action(() => { var old = _preview.Screen.Image; _preview.Screen.Image = b; old?.Dispose(); })); } catch { b.Dispose(); } }
    private void PreviewMouseDown(object? s, MouseEventArgs e) { if (!_connected) return; _pointerDown = e.Location; _pointerDownAt = DateTime.UtcNow; _pressedButton = e.Button; if (e.Button == MouseButtons.Right) { var p = ToDevicePoint(e.Location); _ = RunInputAsync("長押し", ct => _adb.SendSwipeAsync(p.X, p.Y, p.X, p.Y, 1000, ct)); _pressedButton = MouseButtons.None; } }
    private void PreviewMouseUp(object? s, MouseEventArgs e) { if (!_connected || _pressedButton != MouseButtons.Left || e.Button != MouseButtons.Left) return; var end = e.Location; var elapsed = (int)Math.Clamp((DateTime.UtcNow - _pointerDownAt).TotalMilliseconds, 100, 5000); var start = ToDevicePoint(_pointerDown); var finish = ToDevicePoint(end); _pressedButton = MouseButtons.None; var distance = Math.Sqrt(Math.Pow(end.X - _pointerDown.X, 2) + Math.Pow(end.Y - _pointerDown.Y, 2)); if (distance >= 8) _ = RunInputAsync("スワイプ", ct => _adb.SendSwipeAsync(start.X, start.Y, finish.X, finish.Y, elapsed, ct)); else if (elapsed >= 650) _ = RunInputAsync("長押し", ct => _adb.SendSwipeAsync(start.X, start.Y, start.X, start.Y, Math.Max(1000, elapsed), ct)); else _ = RunInputAsync("タップ", ct => _adb.SendTapAsync(finish.X, finish.Y, ct)); }
    private Point ToDevicePoint(Point p) { var screen = _preview.Screen; var x = Math.Clamp(p.X, 0, screen.ClientSize.Width - 1); var y = Math.Clamp(p.Y, 0, screen.ClientSize.Height - 1); return new Point((int)((long)x * _deviceSize.Width / Math.Max(1, screen.ClientSize.Width)), (int)((long)y * _deviceSize.Height / Math.Max(1, screen.ClientSize.Height))); }
    private async Task SendTextAsync() { if (string.IsNullOrEmpty(_text.Text)) return; await RunInputAsync("文字送信", ct => _adb.SendTextAsync(_text.Text, ct)); _text.Clear(); }
    private async Task RunInputAsync(string action, Func<CancellationToken, Task> command) { if (!_connected) { SetStatus("先にAndroidを接続してください。"); return; } try { await command(_lifetime.Token); SetStatus(action + "を送信しました"); } catch (Exception ex) { SetStatus(action + "に失敗 — " + FirstLine(ex.Message)); } }
    private void SetStatus(string text) { if (IsDisposed) return; if (InvokeRequired) BeginInvoke(new Action(() => _status.Text = text)); else _status.Text = text; }
    private static string FirstLine(string text) => text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? text;
    private sealed record CapturedFrame(string FileName, int Slot);
}
