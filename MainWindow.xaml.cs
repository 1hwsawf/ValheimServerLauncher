using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using Microsoft.VisualBasic;

namespace ValheimServerLauncher;

public partial class MainWindow : Window
{
    private readonly ValheimConfig _config = ValheimConfig.Load();
    private readonly ValheimServerManager _manager;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly List<string> _all = new();
    private readonly List<string> _launcher = new();
    private readonly List<string> _server = new();
    private readonly List<string> _players = new();
    private readonly List<string> _steam = new();
    private bool _busy;
    private bool _forceClose;
    private bool _restartTriggered;
    private bool _backupTriggered;
    private DateTime _lastStatsAt;

    public MainWindow()
    {
        InitializeComponent();
        Localization.ApplyTo(this);
        SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
        _manager = new ValheimServerManager(_config);
        _manager.Log += message => Dispatcher.BeginInvoke(() => AddLog(message));
        _manager.RunningChanged += running => Dispatcher.BeginInvoke(() => UpdateStatus(running));
        LoadFields();
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
        Loaded += (_, _) => _ = LoadPublicIpAsync();
        Closing += OnClosing;
        UpdateStatus(false);
        AddLog($"启动器已就绪。服务器目录：{_config.ServerRoot}");
    }

    private void LoadFields()
    {
        ScheduledRestartBox.IsChecked = _config.ScheduledRestartEnabled;
        ScheduledBackupBox.IsChecked = _config.ScheduledBackupEnabled;
        RestartTimesBox.Content = string.Join(", ", _config.RestartTimes);
        BackupTimesBox.Content = string.Join(", ", _config.BackupTimes);
        RestartMinimumHoursBox.Text = _config.MinUptimeHours.ToString();
        RestartAnnouncementBox.IsChecked = _config.AnnounceRestartsEnabled;
        AnnouncementFirstBox.Text = _config.AnnounceLead1.ToString();
        AnnouncementSecondBox.Text = _config.AnnounceLead2.ToString();
        AnnouncementThirdBox.Text = _config.AnnounceLead3.ToString();
        RetentionBox.Text = _config.BackupRetentionDays.ToString();
        BackupStartupBox.IsChecked = _config.BackupOnStartup;
        BackupShutdownBox.IsChecked = _config.BackupOnShutdown;
        PinVersionBox.IsChecked = _config.VersionPinEnabled;
        AutoUpdateBox.IsChecked = _config.AutoUpdateEnabled;
        UpdateOnStartBox.IsChecked = _config.UpdateOnStart;
        UpdateMinutesBox.Text = _config.UpdateCheckMinutes.ToString();
        RestartOnCrashBox.IsChecked = _config.RestartOnCrash;
        HealthCheckBox.IsChecked = _config.HealthCheckEnabled;
        HealthSecondsBox.Text = _config.HealthCheckSeconds.ToString();
        CompactToggle.IsChecked = _config.CompactMode;
        ApplyCompactMode();
        UpdatePublicPortText();
        UpdateBackupLocationText();
    }

    private bool ReadFields()
    {
        if (!TryInt(RetentionBox.Text, 0, 3650, out var retention) ||
            !TryInt(RestartMinimumHoursBox.Text, 0, 168, out var minUptime) ||
            !TryInt(AnnouncementFirstBox.Text, 0, 1440, out var lead1) ||
            !TryInt(AnnouncementSecondBox.Text, 0, 1440, out var lead2) ||
            !TryInt(AnnouncementThirdBox.Text, 0, 1440, out var lead3) ||
            !TryInt(UpdateMinutesBox.Text, 1, 10080, out var updateMinutes) ||
            !TryInt(HealthSecondsBox.Text, 1, 3600, out var healthSeconds) ||
            _config.ParsedRestartTimes().Count == 0 ||
            _config.ParsedBackupTimes().Count == 0)
        {
            MessageBox.Show("请检查重启时间、备份时间以及各项数字设置。", "设置错误",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        _config.ScheduledRestartEnabled = ScheduledRestartBox.IsChecked == true;
        _config.ScheduledBackupEnabled = ScheduledBackupBox.IsChecked == true;
        _config.MinUptimeHours = minUptime;
        _config.AnnounceRestartsEnabled = RestartAnnouncementBox.IsChecked == true;
        _config.AnnounceLead1 = lead1;
        _config.AnnounceLead2 = lead2;
        _config.AnnounceLead3 = lead3;
        _config.BackupRetentionDays = retention;
        _config.BackupOnStartup = BackupStartupBox.IsChecked == true;
        _config.BackupOnShutdown = BackupShutdownBox.IsChecked == true;
        _config.VersionPinEnabled = PinVersionBox.IsChecked == true;
        _config.AutoUpdateEnabled = AutoUpdateBox.IsChecked == true;
        _config.UpdateOnStart = UpdateOnStartBox.IsChecked == true;
        _config.UpdateCheckMinutes = updateMinutes;
        _config.RestartOnCrash = RestartOnCrashBox.IsChecked == true;
        _config.HealthCheckEnabled = HealthCheckBox.IsChecked == true;
        _config.HealthCheckSeconds = healthSeconds;
        _config.CompactMode = CompactToggle.IsChecked == true;
        _config.Save();
        UpdateStatusText.Text = _config.AutoUpdateEnabled ? "自动" : "手动";
        return true;
    }

    private static bool TryInt(string value, int min, int max, out int result) =>
        int.TryParse(value, out result) && result >= min && result <= max;

    private static List<string> SplitTimes(string text)
    {
        return text.Split(new[] { ',', '，', ';', '；', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => TimeOnly.TryParse(value, out var time) ? time.ToString("HH:mm") : "")
            .Where(value => value.Length > 0).Distinct().OrderBy(value => value).ToList();
    }

    private async void StartClick(object sender, RoutedEventArgs e)
    {
        if (!_manager.IsInstalled)
        {
            await InstallAsync(false);
            return;
        }
        if (_manager.IsRunning) { await StopClickAsync(); return; }
        if (!ReadFields() || _busy) return;
        SetBusy(true);
        try
        {
            if (_config.ClearLogsOnManualStart) ClearLogEntries();
            if (_config.BackupOnStartup && Directory.Exists(_config.SaveDirectory))
                await _manager.CreateBackupAsync();
            if (_config.UpdateOnStart && _config.AutoUpdateEnabled)
                await _manager.InstallOrUpdateAsync(_config.ValidateOnRestart);
            _manager.Start();
        }
        catch (Exception ex) { Error("启动失败", ex); }
        finally { SetBusy(false); }
    }

    private async void RestartClick(object sender, RoutedEventArgs e)
    {
        if (_busy || !_manager.IsRunning) return;
        if (!ReadFields()) return;
        SetBusy(true);
        await RestartServerAsync();
    }

    private async void StopClick(object sender, RoutedEventArgs e) => await StopClickAsync();

    private async Task StopClickAsync()
    {
        if (_busy || !_manager.IsRunning) return;
        SetBusy(true);
        try
        {
            if (_config.BackupOnShutdown) await _manager.CreateBackupAsync();
            await _manager.StopAsync();
        }
        catch (Exception ex) { Error("停止失败", ex); }
        finally { SetBusy(false); }
    }

    private void ForceStopClick(object sender, RoutedEventArgs e)
    {
        _manager.ForceKill();
        AddLog("已强制停止服务器。");
    }

    private async void InstallClick(object sender, RoutedEventArgs e) => await InstallAsync(false);
    private async void ValidateClick(object sender, RoutedEventArgs e) => await InstallAsync(true);

    private async void ImportClick(object sender, RoutedEventArgs e)
    {
        if (_busy || _manager.IsRunning || _manager.IsInstalled) return;

        var picker = new OpenFolderDialog
        {
            Title = "选择英灵神殿专用服务器目录",
            Multiselect = false
        };
        if (picker.ShowDialog(this) != true) return;

        SetBusy(true);
        try
        {
            await _manager.ImportServerAsync(picker.FolderName);
            UpdateStatusText.Text = "已导入";
            UpdateStatus(_manager.IsRunning);
        }
        catch (OperationCanceledException)
        {
            AddLog("服务器导入已取消。");
        }
        catch (Exception ex)
        {
            Error("导入失败", ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task InstallAsync(bool validate)
    {
        if (_busy || _manager.IsRunning) return;
        if (!ReadFields()) return;
        SetBusy(true);
        try
        {
            await _manager.InstallOrUpdateAsync(validate);
            UpdateStatusText.Text = validate ? "已校验" : "已检查";
        }
        catch (Exception ex) { Error("SteamCMD 操作失败", ex); }
        finally { SetBusy(false); }
    }

    private async void BackupClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await _manager.CreateBackupAsync();
            UpdateBackupLocationText();
        }
        catch (Exception ex) { Error("备份失败", ex); }
    }

    private void ServerSettingsClick(object sender, RoutedEventArgs e)
    {
        if (!_manager.IsInstalled) return;
        var dialog = new LaunchArgumentsWindow(_config) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            _config.Save();
            UpdatePublicPortText();
            AddLog("服务器设置已保存。");
        }
    }

    private void ModsClick(object sender, RoutedEventArgs e)
    {
        if (_manager.IsInstalled) _manager.OpenModsDirectory();
    }

    private void ServerDirectoryClick(object sender, RoutedEventArgs e)
    {
        if (_manager.IsInstalled) Open(_config.ServerDirectory);
    }
    private void OpenBackupsClick(object sender, RoutedEventArgs e) => Open(_config.BackupDirectory);

    private void LauncherSettingsClick(object sender, RoutedEventArgs e)
    {
        var dialog = new LauncherSettingsWindow(_config) { Owner = this };
        if (dialog.ShowDialog() == true) AddLog("启动器设置已保存。");
    }

    private async void CommandClick(object sender, RoutedEventArgs e)
    {
        if (!_manager.IsInstalled) return;
        var dialog = new CommandWindow { Owner = this };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.CommandText)) return;
        try
        {
            await _manager.SendCommandAsync(dialog.CommandText);
            AddLog($"> {dialog.CommandText}");
        }
        catch (Exception ex) { Error("命令发送失败", ex); }
    }

    private void EditRestartTimesClick(object sender, RoutedEventArgs e)
    {
        var value = Prompt("重启时间", "请输入重启时间，多个时间用逗号分隔，例如：05:00, 17:00",
            string.Join(", ", _config.RestartTimes));
        if (value is null) return;
        var times = SplitTimes(value);
        if (times.Count == 0) { MessageBox.Show("没有识别到有效时间。", "设置错误"); return; }
        _config.RestartTimes = times;
        RestartTimesBox.Content = string.Join(", ", times);
        _config.Save();
    }

    private void EditBackupTimesClick(object sender, RoutedEventArgs e)
    {
        var value = Prompt("备份时间", "请输入备份时间，多个时间用逗号分隔，例如：04:00",
            string.Join(", ", _config.BackupTimes));
        if (value is null) return;
        var times = SplitTimes(value);
        if (times.Count == 0) { MessageBox.Show("没有识别到有效时间。", "设置错误"); return; }
        _config.BackupTimes = times;
        BackupTimesBox.Content = string.Join(", ", times);
        _config.Save();
    }

    private static string? Prompt(string title, string message, string initial)
    {
        var value = Interaction.InputBox(message, title, initial);
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static void Open(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

    private void ToggleIpClick(object sender, RoutedEventArgs e)
    {
        _config.IsIpRevealed = !_config.IsIpRevealed;
        UpdatePublicAddressText();
        _config.Save();
    }

    private void CopyPublicIpClick(object sender, RoutedEventArgs e)
    {
        var ip = PublicIpBox.Tag?.ToString();
        if (string.IsNullOrWhiteSpace(ip) || ip == "-")
            return;
        CopyToClipboard($"{ip}:{_config.Port}", "公网地址");
    }

    private static void CopyToClipboard(string text, string label)
    {
        if (string.IsNullOrWhiteSpace(text) || text == "-") return;
        try { Clipboard.SetText(text); }
        catch (Exception ex) { MessageBox.Show(ex.Message, $"复制{label}失败", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void CompactClick(object sender, RoutedEventArgs e)
    {
        _config.CompactMode = CompactToggle.IsChecked == true;
        ApplyCompactMode();
        _config.Save();
    }

    private void ApplyCompactMode()
    {
        SettingsArea.Visibility = _config.CompactMode ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ClearLogClick(object sender, RoutedEventArgs e) => ClearLogEntries();

    private void ClearLogEntries()
    {
        _all.Clear(); _launcher.Clear(); _server.Clear(); _players.Clear(); _steam.Clear();
        RefreshLog();
    }

    private void LogTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source == LogTabs) RefreshLog();
    }

    private void LogFilterChanged(object sender, TextChangedEventArgs e) => RefreshLog();

    private void LogPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && LogFilterBox.Text.Length > 0)
        {
            LogFilterBox.Clear();
            e.Handled = true;
        }
    }

    private void JumpLatestClick(object sender, RoutedEventArgs e) => LogBox.ScrollToEnd();

    private void Tick()
    {
        UpdateStatus(_manager.IsRunning);
        if (_manager.IsRunning && _manager.StartedAt is { } start)
            UptimeText.Text = (DateTime.Now - start).ToString(@"dd\.hh\:mm\:ss");
        else
            UptimeText.Text = "-";
        NextRestartText.Text = NextTime(_config.ParsedRestartTimes());
        NextBackupText.Text = NextTime(_config.ParsedBackupTimes());

        if (DateTime.Now - _lastStatsAt > TimeSpan.FromSeconds(2))
        {
            _lastStatsAt = DateTime.Now;
            UpdateProcessStats();
        }

        var now = DateTime.Now;
        if (now.Second < 2 && _config.ScheduledRestartEnabled &&
            _config.ParsedRestartTimes().Any(t => t.Hour == now.Hour && t.Minute == now.Minute))
        {
            if (!_restartTriggered && _manager.IsRunning && IsMinimumUptimeReached())
            {
                _restartTriggered = true;
                _ = RestartScheduledAsync();
            }
        }
        else if (now.Second >= 2) _restartTriggered = false;

        if (now.Second < 2 && _config.ScheduledBackupEnabled &&
            _config.ParsedBackupTimes().Any(t => t.Hour == now.Hour && t.Minute == now.Minute))
        {
            if (!_backupTriggered)
            {
                _backupTriggered = true;
                _ = BackupScheduledAsync();
            }
        }
        else if (now.Second >= 2) _backupTriggered = false;
    }

    private bool IsMinimumUptimeReached() =>
        _manager.StartedAt is { } started &&
        DateTime.Now - started >= TimeSpan.FromHours(_config.MinUptimeHours);

    private async Task RestartScheduledAsync()
    {
        AddLog("开始执行定时重启。");
        if (!ReadFields() || _busy) return;
        SetBusy(true);
        await RestartServerAsync();
    }

    private async Task RestartServerAsync()
    {
        try
        {
            if (_config.AnnounceRestartsEnabled)
                AddLog(_config.AnnouncementText.Replace("{minutes}", "0"));
            await _manager.StopAsync();
            if (_config.UpdateOnStart && _config.AutoUpdateEnabled)
                await _manager.InstallOrUpdateAsync(_config.ValidateOnRestart);
            _manager.Start();
        }
        catch (Exception ex) { Error("重启失败", ex); }
        finally { SetBusy(false); }
    }

    private async Task BackupScheduledAsync()
    {
        try { await _manager.CreateBackupAsync(); }
        catch (Exception ex) { AddLog("定时备份失败：" + ex.Message); }
    }

    private void UpdateStatus(bool running)
    {
        var installed = _manager.IsInstalled;
        StatusText.Text = !installed ? Localization.Text("未安装", "Not installed") :
            running ? Localization.Text("运行中", "Running") : Localization.Text("已停止", "Stopped");
        StartButton.Content = !installed ? Localization.Text("安装", "Install") :
            running ? Localization.Text("停止", "Stop") : Localization.Text("启动", "Start");
        StartButton.ToolTip = !installed ? Localization.Text("安装英灵神殿专用服务器", "Install the Valheim dedicated server") :
            running ? Localization.Text("停止服务器", "Stop server") : Localization.Text("启动服务器", "Start server");
        StartButton.IsEnabled = !_busy;
        ImportButton.Visibility = installed ? Visibility.Collapsed : Visibility.Visible;
        ImportButton.IsEnabled = !_busy && !installed;
        RestartButton.IsEnabled = !_busy && installed && running;
        InstallButton.Visibility = installed ? Visibility.Visible : Visibility.Collapsed;
        InstallButton.IsEnabled = !_busy && installed && !running;
        ValidateButton.Visibility = installed ? Visibility.Visible : Visibility.Collapsed;
        ValidateButton.IsEnabled = !_busy && installed && !running;
        ForceStopButton.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        ServerSettingsButton.IsEnabled = !_busy && installed;
        ModsButton.IsEnabled = !_busy && installed;
        ServerDirectoryButton.IsEnabled = !_busy && installed;
        CommandButton.IsEnabled = !_busy && installed && running;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        UpdateStatus(_manager.IsRunning);
    }

    private void AddLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        _all.Add(line);
        _launcher.Add(line);
        if (message.StartsWith("[SteamCMD]", StringComparison.OrdinalIgnoreCase)) _steam.Add(line);
        else _server.Add(line);
        if (message.Contains("joined", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("left", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("进入", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("离开", StringComparison.OrdinalIgnoreCase))
        {
            _players.Add(line);
        }
        RefreshLog();
    }

    private void RefreshLog()
    {
        if (LogBox is null) return;
        var source = LogTabs.SelectedIndex switch
        {
            1 => _launcher,
            2 => _server,
            3 => _players,
            4 => _steam,
            _ => _all
        };
        var filter = LogFilterBox?.Text?.Trim() ?? "";
        var lines = string.IsNullOrEmpty(filter)
            ? source
            : source.Where(line => line.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        LogBox.Text = string.Join(Environment.NewLine, lines);
        LogBox.ScrollToEnd();
    }

    private void UpdatePublicPortText() => UpdatePublicAddressText();

    private void UpdatePublicAddressText()
    {
        var ip = PublicIpBox.Tag?.ToString();
        PublicIpBox.Text = string.IsNullOrWhiteSpace(ip) || ip == "-"
            ? "-"
            : $"{(_config.IsIpRevealed ? ip : MaskIp(ip))}:{_config.Port}";
    }

    private async Task LoadPublicIpAsync()
    {
        var endpoints = new[]
        {
            "https://api.ipify.org",
            "https://ifconfig.me/ip",
            "https://icanhazip.com"
        };

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        foreach (var endpoint in endpoints)
        {
            try
            {
                var value = (await client.GetStringAsync(endpoint)).Trim();
                if (System.Net.IPAddress.TryParse(value, out var address) &&
                    address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    PublicIpBox.Tag = value;
                    UpdatePublicAddressText();
                    return;
                }
            }
            catch
            {
                // Try the next public IP service.
            }
        }

        PublicIpBox.Tag = "-";
        PublicIpBox.Text = "-";
        AddLog("公网 IP 查询失败，请检查网络连接。");
    }

    private static string MaskIp(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip) || ip == "-") return "-";
        var parts = ip.Split('.');
        return parts.Length == 4 ? $"{parts[0]}.{parts[1]}.***.***" : "已隐藏";
    }

    private void UpdateBackupLocationText() =>
        BackupLocationButton.Content = string.IsNullOrWhiteSpace(_config.BackupPath) ? "默认" : _config.BackupPath;

    private static string NextTime(IEnumerable<TimeOnly> times)
    {
        var now = TimeOnly.FromDateTime(DateTime.Now);
        var next = times.Select(time => time >= now ? time : time.AddHours(24)).OrderBy(time => time).FirstOrDefault();
        return next == default ? "-" : next.ToString("HH:mm");
    }

    private void UpdateProcessStats()
    {
        if (!_manager.IsRunning) { VersionText.Text = "-"; FpsText.Text = "-"; CpuText.Text = "-"; MemoryText.Text = "-"; PlayersText.Text = "-"; return; }
        try
        {
            using var process = Process.GetProcessesByName("valheim_server").FirstOrDefault();
            if (process is null) return;
            process.Refresh();
            MemoryText.Text = $"{process.WorkingSet64 / 1024d / 1024d:0} MB";
            CpuText.Text = "-";
            FpsText.Text = "-";
            PlayersText.Text = "-";
            VersionText.Text = FileVersionInfo.GetVersionInfo(process.MainModule?.FileName ?? "").FileVersion ?? "-";
        }
        catch { }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_forceClose || !_manager.IsRunning) return;
        e.Cancel = true;
        var choice = MessageBox.Show("服务器仍在运行，是否先停止服务器？\n选择“否”将直接关闭启动器并保留服务器运行。",
            "关闭启动器", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
        if (choice == MessageBoxResult.Cancel) return;
        if (choice == MessageBoxResult.No)
        {
            _forceClose = true;
            Close();
            return;
        }
        _ = ShutdownThenCloseAsync();
    }

    private async Task ShutdownThenCloseAsync()
    {
        try { await StopClickAsync(); }
        finally { _forceClose = true; Dispatcher.Invoke(Close); }
    }

    private void Error(string title, Exception ex)
    {
        AddLog($"{title}：{ex.Message}");
        MessageBox.Show(ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
