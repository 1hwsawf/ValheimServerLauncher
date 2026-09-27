using System.Diagnostics;
using System.IO.Compression;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace ValheimServerLauncher;

public sealed class ValheimServerManager : IDisposable
{
    private const string SteamCmdUrl = "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip";
    private readonly ValheimConfig _config;
    private Process? _process;
    private bool _stopRequested;
    private readonly object _sync = new();

    public event Action<string>? Log;
    public event Action<bool>? RunningChanged;
    public DateTime? StartedAt { get; private set; }
    public bool IsRunning => _process is { HasExited: false };
    public bool IsInstalled => File.Exists(_config.ServerExecutable);
    public string SteamCmdPath => Path.Combine(_config.SteamCmdDirectory, "steamcmd.exe");

    public ValheimServerManager(ValheimConfig config) => _config = config;

    public async Task InstallOrUpdateAsync(bool validate, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_config.ServerRoot);
        Directory.CreateDirectory(_config.ServerDirectory);
        await EnsureSteamCmdAsync(cancellationToken);
        var args = new[]
        {
            "+force_install_dir", _config.ServerDirectory,
            "+login", "anonymous",
            "+app_update", ValheimConfig.SteamAppId,
            validate ? "validate" : "",
            "+quit"
        }.Where(a => a.Length > 0).ToArray();
        Log?.Invoke("正在通过 SteamCMD 安装/更新英灵神殿服务器...");
        var exit = await RunSteamCmdAsync(args, cancellationToken);
        if (exit != 0) throw new InvalidOperationException($"SteamCMD 退出代码：{exit}");
        Log?.Invoke("服务器文件已准备完成。");
    }

    public async Task ImportServerAsync(string sourceDirectory, CancellationToken cancellationToken = default)
    {
        if (IsRunning)
            throw new InvalidOperationException("请先停止服务器再导入。");
        if (IsInstalled)
            throw new InvalidOperationException("服务器已经安装，无需重复导入。");
        if (!Directory.Exists(sourceDirectory))
            throw new DirectoryNotFoundException("选择的目录不存在。");

        var sourceExecutable = Directory.EnumerateFiles(sourceDirectory, "valheim_server.exe", SearchOption.AllDirectories)
            .FirstOrDefault();
        if (sourceExecutable is null)
            throw new InvalidOperationException("所选目录中没有找到 valheim_server.exe，不像是英灵神殿专用服务器目录。");

        var sourceRoot = Path.GetDirectoryName(sourceExecutable)!;
        if (PathsEqual(sourceRoot, _config.ServerDirectory) ||
            sourceRoot.StartsWith(Path.GetFullPath(_config.ServerDirectory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("不能把启动器自己的服务器目录导入到自身。");

        Directory.CreateDirectory(_config.ServerDirectory);
        Log?.Invoke($"正在导入服务器文件：{sourceRoot}");
        await CopyDirectoryAsync(sourceRoot, _config.ServerDirectory, cancellationToken);
        Log?.Invoke("服务器导入完成，原目录未被修改。");
    }

    private static async Task CopyDirectoryAsync(string source, string destination, CancellationToken ct)
    {
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await using var input = File.OpenRead(file);
            await using var output = File.Create(target);
            await input.CopyToAsync(output, ct);
        }
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

    private async Task EnsureSteamCmdAsync(CancellationToken ct)
    {
        if (File.Exists(SteamCmdPath)) return;
        Directory.CreateDirectory(_config.SteamCmdDirectory);
        var zipPath = Path.Combine(_config.SteamCmdDirectory, "steamcmd.zip");
        Log?.Invoke("正在下载 SteamCMD...");
        using var http = new HttpClient();
        await using (var source = await http.GetStreamAsync(SteamCmdUrl, ct))
        await using (var target = File.Create(zipPath))
            await source.CopyToAsync(target, ct);
        ZipFile.ExtractToDirectory(zipPath, _config.SteamCmdDirectory, true);
        File.Delete(zipPath);
        await RunSteamCmdAsync(new[] { "+login", "anonymous", "+quit" }, ct);
    }

    private async Task<int> RunSteamCmdAsync(IReadOnlyList<string> args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(SteamCmdPath) { WorkingDirectory = _config.SteamCmdDirectory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = _config.HideSteamCmdWindow };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) Log?.Invoke("[SteamCMD] " + e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) Log?.Invoke("[SteamCMD] " + e.Data); };
        if (!process.Start()) throw new InvalidOperationException("无法启动 SteamCMD。");
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(ct);
        return process.ExitCode;
    }

    public void Start()
    {
        lock (_sync)
        {
            if (IsRunning) return;
            if (!File.Exists(_config.ServerExecutable))
                throw new FileNotFoundException("找不到 valheim_server.exe，请先安装服务器。", _config.ServerExecutable);
            Directory.CreateDirectory(_config.SaveDirectory);
            _stopRequested = false;
            StartedAt = DateTime.Now;
            var psi = new ProcessStartInfo(_config.ServerExecutable)
            {
                WorkingDirectory = _config.ServerDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            foreach (var arg in _config.BuildArguments()) psi.ArgumentList.Add(arg);
            _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _process.OutputDataReceived += OnOutput;
            _process.ErrorDataReceived += OnOutput;
            _process.Exited += OnExited;
            if (!_process.Start()) throw new InvalidOperationException("无法启动 Valheim 服务器。");
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
            Log?.Invoke("英灵神殿服务器已启动。");
            RunningChanged?.Invoke(true);
        }
    }

    public async Task StopAsync()
    {
        Process? process;
        lock (_sync) { _stopRequested = true; process = _process; }
        if (process is null || process.HasExited) return;
        try
        {
            await process.StandardInput.WriteLineAsync("save");
            await Task.Delay(500);
            await process.StandardInput.WriteLineAsync("shutdown");
        }
        catch { }
        if (!await WaitForExitAsync(process, TimeSpan.FromSeconds(20)))
        {
            Log?.Invoke("服务器未能正常退出，正在强制结束进程。");
            try { process.Kill(true); } catch { }
        }
    }

    public async Task SendCommandAsync(string command)
    {
        if (_process is not { HasExited: false })
            throw new InvalidOperationException("服务器当前未运行。");
        await _process.StandardInput.WriteLineAsync(command);
    }

    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try { await process.WaitForExitAsync(cts.Token); return true; }
        catch (OperationCanceledException) { return false; }
    }

    public async Task<string> CreateBackupAsync()
    {
        Directory.CreateDirectory(_config.BackupDirectory);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var path = Path.Combine(_config.BackupDirectory, $"valheim-{stamp}.zip");
        if (IsRunning)
        {
            try { await _process!.StandardInput.WriteLineAsync("save"); await Task.Delay(800); } catch { }
        }
        if (!Directory.Exists(_config.SaveDirectory))
            throw new DirectoryNotFoundException("保存目录还不存在。");
        ZipFile.CreateFromDirectory(_config.SaveDirectory, path, CompressionLevel.Fastest, false);
        CleanupOldBackups();
        Log?.Invoke($"备份已创建：{Path.GetFileName(path)}");
        return path;
    }

    private void CleanupOldBackups()
    {
        if (_config.BackupRetentionDays <= 0)
            return;
        var cutoff = DateTime.Now.AddDays(-_config.BackupRetentionDays);
        try
        {
            foreach (var file in Directory.EnumerateFiles(_config.BackupDirectory, "valheim-*.zip"))
            {
                if (File.GetLastWriteTime(file) < cutoff)
                    File.Delete(file);
            }
        }
        catch (Exception ex)
        {
            Log?.Invoke("清理过期备份失败：" + ex.Message);
        }
    }

    public async Task<PortCheckResult> CheckPortsAsync(CancellationToken ct = default)
    {
        if (_config.Crossplay)
            return new PortCheckResult(PortCheckStatus.NotRequiredForCrossplay, _config.Port,
                "当前使用 Crossplay 中继，公网联机不需要路由器端口转发。");
        if (IsRunning)
            return new PortCheckResult(PortCheckStatus.ServerRunning, _config.Port,
                "请先停止服务器再检测。服务器运行时不能由启动器临时接管游戏端口。");
        if (_config.Port is < 1 or > 65535)
            return new PortCheckResult(PortCheckStatus.InvalidPort, _config.Port,
                "端口必须在 1 到 65535 之间。");

        using var listener = new UdpClient();
        try
        {
            listener.Client.Bind(new IPEndPoint(IPAddress.Any, _config.Port));
        }
        catch (SocketException ex)
        {
            return new PortCheckResult(PortCheckStatus.PortInUse, _config.Port,
                $"本机 UDP 端口 {_config.Port} 无法绑定，可能已被其他程序占用或被本机策略拦截：{ex.SocketErrorCode}。");
        }
        await Task.CompletedTask;
        return new PortCheckResult(PortCheckStatus.Available, _config.Port,
            $"本机 UDP 端口 {_config.Port} 当前可用。");
    }

    private static async Task EchoUdpAsync(UdpClient listener, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var packet = await listener.ReceiveAsync(ct);
                await listener.SendAsync(packet.Buffer, packet.RemoteEndPoint, ct);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException) { }
    }

    public void OpenServerDirectory()
    {
        Directory.CreateDirectory(_config.ServerDirectory);
        Process.Start(new ProcessStartInfo { FileName = _config.ServerDirectory, UseShellExecute = true });
    }

    public void OpenModsDirectory()
    {
        var mods = Path.Combine(_config.ServerDirectory, "BepInEx", "plugins");
        Directory.CreateDirectory(mods);
        Process.Start(new ProcessStartInfo { FileName = mods, UseShellExecute = true });
    }

    public void ForceKill()
    {
        try { _process?.Kill(true); } catch { }
    }

    private void OnOutput(object _, DataReceivedEventArgs e)
    {
        if (e.Data is not null) Log?.Invoke(e.Data);
    }

    private void OnExited(object? _, EventArgs __)
    {
        var restart = !_stopRequested && _config.RestartOnCrash;
        Log?.Invoke(restart ? "服务器意外退出，将自动重启。" : "服务器已停止。");
        RunningChanged?.Invoke(false);
        StartedAt = null;
        if (restart)
            _ = Task.Run(async () => { await Task.Delay(3000); try { Start(); } catch (Exception ex) { Log?.Invoke("自动重启失败：" + ex.Message); } });
    }

    public void Dispose()
    {
        try { _process?.Dispose(); } catch { }
    }
}

public enum PortCheckStatus
{
    NotRequiredForCrossplay,
    ServerRunning,
    InvalidPort,
    PortInUse,
    Available
}

public sealed record PortCheckResult(PortCheckStatus Status, int Port, string Message);
