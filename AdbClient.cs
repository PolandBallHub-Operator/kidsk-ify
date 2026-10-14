using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;

namespace KidsK_ify;

internal sealed class AdbClient
{
    private readonly string _captureDirectory;
    private readonly string _adbCommand = File.Exists(Path.Combine(AppContext.BaseDirectory, "adb.exe")) ? Path.Combine(AppContext.BaseDirectory, "adb.exe") : "adb";
    public AdbClient(string captureDirectory) { _captureDirectory = captureDirectory; Directory.CreateDirectory(captureDirectory); }

    public async Task<Size> GetScreenSizeAsync(CancellationToken ct)
    {
        var r = await RunAsync(new[] { "shell", "wm", "size" }, ct, 8);
        var matches = Regex.Matches(r.StandardOutput, @"(?:Physical|Override) size:\s*(\d+)x(\d+)");
        if (matches.Count == 0) throw new InvalidOperationException("画面サイズを取得できませんでした。ADB接続を確認してください。\n" + r.StandardOutput + r.StandardError);
        var m = matches.Cast<Match>().LastOrDefault(x => x.Value.StartsWith("Override", StringComparison.Ordinal)) ?? matches[^1];
        return new Size(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value));
    }

    public async Task CaptureAsync(string fileName, CancellationToken ct)
    {
        ValidateCaptureName(fileName);
        var path = Path.Combine(_captureDirectory, fileName);
        await RunBinaryAsync(new[] { "exec-out", "screencap", "-p", "-d", "0" }, path, ct, 12);
        if (!File.Exists(path) || new FileInfo(path).Length == 0) throw new InvalidOperationException("画面キャプチャを取得できませんでした。");
    }
    public async Task DownloadCurrentScreenAsync(string destination, CancellationToken ct) => await RunBinaryAsync(new[] { "exec-out", "screencap", "-p", "-d", "0" }, destination, ct, 12);
    public Task<CommandResult> RunAdbAsync(string arguments, CancellationToken ct = default) => RunAsync(Tokenize(arguments), ct, 30);
    public Task<CommandResult> RunShellAsync(string command, CancellationToken ct = default) => RunAsync(new[] { "shell", command }, ct, 30);
    public Task<CommandResult> InstallAsync(string apk, bool replace, CancellationToken ct = default) => RunAsync(replace ? new[] { "install", "-r", apk } : new[] { "install", apk }, ct, 120);
    public Task<CommandResult> UninstallAsync(string packageName, CancellationToken ct = default) => RunAsync(new[] { "uninstall", packageName }, ct, 60);
    public Task<CommandResult> PullAsync(string remote, string local, CancellationToken ct = default) => RunAsync(new[] { "pull", remote, local }, ct, 120);
    public Task<CommandResult> PushAsync(string local, string remote, CancellationToken ct = default) => RunAsync(new[] { "push", local, remote }, ct, 120);
    public Task<CommandResult> GetPropertiesAsync(CancellationToken ct = default) => RunShellAsync("getprop", ct);
    public Task<CommandResult> ListPackagesAsync(string options, CancellationToken ct = default) => RunShellAsync("pm list packages " + options, ct);
    public Task<CommandResult> SetPackageEnabledAsync(string packageName, bool enabled, CancellationToken ct = default) => RunShellAsync($"pm {(enabled ? "enable" : "disable-user --user 0")} {packageName}", ct);
    public Task SendTapAsync(int x, int y, CancellationToken ct) => RunShellAsync($"input tap {x} {y}", ct);
    public Task SendSwipeAsync(int x1, int y1, int x2, int y2, int durationMs, CancellationToken ct) => RunShellAsync($"input swipe {x1} {y1} {x2} {y2} {durationMs}", ct);
    public Task SendKeyAsync(int keyCode, CancellationToken ct) => RunShellAsync($"input keyevent {keyCode}", ct);
    public Task SendTextAsync(string text, CancellationToken ct)
    {
        var value = text.Replace("\r", " ").Replace("\n", " ").Replace(" ", "%s");
        return RunShellAsync("input text '" + value.Replace("'", "'\\''") + "'", ct);
    }
    public Bitmap LoadCapturedBitmap(string fileName) { using var s = new FileStream(Path.Combine(_captureDirectory, fileName), FileMode.Open, FileAccess.Read, FileShare.Read); using var image = Image.FromStream(s, false, true); return new Bitmap(image); }
    public Task DeleteCaptureAsync(string fileName, CancellationToken ct = default) { ValidateCaptureName(fileName); try { File.Delete(Path.Combine(_captureDirectory, fileName)); } catch { } return Task.CompletedTask; }

    private async Task<CommandResult> RunAsync(IReadOnlyList<string> args, CancellationToken ct, int seconds)
    {
        var si = new ProcessStartInfo { FileName = _adbCommand, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        foreach (var arg in args) si.ArgumentList.Add(arg);
        using var p = new Process { StartInfo = si };
        try { if (!p.Start()) throw new InvalidOperationException("adb.exeを起動できませんでした。PATHまたはexe配置を確認してください。"); }
        catch (Exception ex) when (ex is not OperationCanceledException) { throw new InvalidOperationException("adb.exeを起動できません。adb.exeをKidsK-ify.exeと同じフォルダーに置くかPATHへ登録してください。", ex); }
        var stdout = p.StandardOutput.ReadToEndAsync(); var stderr = p.StandardError.ReadToEndAsync();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(seconds));
        try { await p.WaitForExitAsync(timeout.Token); } catch (OperationCanceledException) { try { if (!p.HasExited) p.Kill(true); } catch { } if (ct.IsCancellationRequested) throw; throw new TimeoutException("ADBコマンドがタイムアウトしました。"); }
        var output = await stdout; var error = await stderr;
        if (p.ExitCode != 0) throw new InvalidOperationException($"ADBコマンドに失敗しました (終了コード {p.ExitCode})。\n{error}{output}".Trim());
        return new CommandResult(output, error);
    }
    private async Task RunBinaryAsync(IReadOnlyList<string> args, string path, CancellationToken ct, int seconds)
    {
        var si = new ProcessStartInfo { FileName = _adbCommand, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardErrorEncoding = Encoding.UTF8 };
        foreach (var arg in args) si.ArgumentList.Add(arg);
        using var p = new Process { StartInfo = si }; if (!p.Start()) throw new InvalidOperationException("adb.exeを起動できませんでした。");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!); await using (var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None)) { var copy = p.StandardOutput.BaseStream.CopyToAsync(file, ct); var err = p.StandardError.ReadToEndAsync(); using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(seconds)); try { await Task.WhenAll(copy, p.WaitForExitAsync(timeout.Token)); } catch { try { if (!p.HasExited) p.Kill(true); } catch { } throw; } var error = await err; if (p.ExitCode != 0) throw new InvalidOperationException(error); }
    }
    private static IReadOnlyList<string> Tokenize(string text)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        foreach (var ch in text)
        {
            if (ch == '"') { quoted = !quoted; continue; }
            if (char.IsWhiteSpace(ch) && !quoted)
            {
                if (current.Length > 0) { result.Add(current.ToString()); current.Clear(); }
            }
            else current.Append(ch);
        }
        if (current.Length > 0) result.Add(current.ToString());
        return result;
    }
    private static void ValidateCaptureName(string name) { if (!Regex.IsMatch(name, @"^screenshot(?:_legacy|[1-9]|10)\.png$", RegexOptions.IgnoreCase)) throw new ArgumentException("キャプチャ名が不正です。", nameof(name)); }
    internal sealed record CommandResult(string StandardOutput, string StandardError);
}
