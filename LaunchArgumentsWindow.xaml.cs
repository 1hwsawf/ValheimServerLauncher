using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ValheimServerLauncher;

public partial class LaunchArgumentsWindow : Window
{
    private readonly ValheimConfig _config;

    public LaunchArgumentsWindow(ValheimConfig config)
    {
        InitializeComponent();
        _config = config;
        SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
        ServerNameBox.TextChanged += (_, _) => UpdateLaunchPreview();
        WorldNameBox.TextChanged += (_, _) => UpdateLaunchPreview();
        PasswordBox.PasswordChanged += (_, _) => UpdateLaunchPreview();
        PortBox.TextChanged += (_, _) => UpdateLaunchPreview();
        SaveDirectoryBox.TextChanged += (_, _) => UpdateLaunchPreview();
        LogFilePathBox.TextChanged += (_, _) => UpdateLaunchPreview();
        InstanceIdBox.TextChanged += (_, _) => UpdateLaunchPreview();
        SaveIntervalBox.TextChanged += (_, _) => UpdateLaunchPreview();
        BackupCountBox.TextChanged += (_, _) => UpdateLaunchPreview();
        ShortBackupIntervalBox.TextChanged += (_, _) => UpdateLaunchPreview();
        LongBackupIntervalBox.TextChanged += (_, _) => UpdateLaunchPreview();
        PublicBox.Checked += (_, _) => UpdateLaunchPreview();
        PublicBox.Unchecked += (_, _) => UpdateLaunchPreview();
        CrossplayBox.Checked += (_, _) => UpdateLaunchPreview();
        CrossplayBox.Unchecked += (_, _) => UpdateLaunchPreview();
        WorldPresetBox.SelectionChanged += (_, _) => UpdateLaunchPreview();
        CombatModifierBox.SelectionChanged += (_, _) => UpdateLaunchPreview();
        DeathPenaltyModifierBox.SelectionChanged += (_, _) => UpdateLaunchPreview();
        ResourcesModifierBox.SelectionChanged += (_, _) => UpdateLaunchPreview();
        RaidsModifierBox.SelectionChanged += (_, _) => UpdateLaunchPreview();
        PortalsModifierBox.SelectionChanged += (_, _) => UpdateLaunchPreview();
        NoBuildCostBox.Checked += (_, _) => UpdateLaunchPreview();
        NoBuildCostBox.Unchecked += (_, _) => UpdateLaunchPreview();
        PlayerEventsBox.Checked += (_, _) => UpdateLaunchPreview();
        PlayerEventsBox.Unchecked += (_, _) => UpdateLaunchPreview();
        PassiveMobsBox.Checked += (_, _) => UpdateLaunchPreview();
        PassiveMobsBox.Unchecked += (_, _) => UpdateLaunchPreview();
        NoMapBox.Checked += (_, _) => UpdateLaunchPreview();
        NoMapBox.Unchecked += (_, _) => UpdateLaunchPreview();
        LoadValues();
    }

    private void LoadValues()
    {
        ServerNameBox.Text = _config.ServerName;
        WorldNameBox.Text = _config.WorldName;
        PasswordBox.Password = _config.Password;
        PortBox.Text = _config.Port.ToString();
        MaxPlayersBox.Text = _config.MaxPlayers.ToString();
        PublicBox.IsChecked = _config.PublicServer;
        CrossplayBox.IsChecked = _config.Crossplay;
        SaveDirectoryBox.Text = _config.SaveDirectory;
        LogFilePathBox.Text = _config.LogFilePath;
        InstanceIdBox.Text = _config.InstanceId;
        SaveIntervalBox.Text = _config.SaveInterval.ToString();
        BackupCountBox.Text = _config.BackupCount.ToString();
        ShortBackupIntervalBox.Text = _config.ShortBackupInterval.ToString();
        LongBackupIntervalBox.Text = _config.LongBackupInterval.ToString();
        Select(WorldPresetBox, string.IsNullOrWhiteSpace(_config.WorldPreset) ? "Normal" : _config.WorldPreset);
        Select(CombatModifierBox, _config.CombatModifier);
        Select(DeathPenaltyModifierBox, _config.DeathPenaltyModifier);
        Select(ResourcesModifierBox, _config.ResourcesModifier);
        Select(RaidsModifierBox, _config.RaidsModifier);
        Select(PortalsModifierBox, _config.PortalsModifier);
        NoBuildCostBox.IsChecked = _config.NoBuildCost;
        PlayerEventsBox.IsChecked = _config.PlayerEvents;
        PassiveMobsBox.IsChecked = _config.PassiveMobs;
        NoMapBox.IsChecked = _config.NoMap;
        UpdateLaunchPreview();
    }

    private static void Select(ComboBox box, string? value)
    {
        var wanted = string.IsNullOrWhiteSpace(value) ? "default" : value;
        box.SelectedItem = box.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), wanted, StringComparison.OrdinalIgnoreCase))
            ?? box.Items[0];
    }

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ServerNameBox.Text) ||
            string.IsNullOrWhiteSpace(WorldNameBox.Text) ||
            PasswordBox.Password.Length < 5 ||
            !int.TryParse(PortBox.Text, out var port) || port is < 1 or > 65535 ||
            !int.TryParse(MaxPlayersBox.Text, out var maxPlayers) || maxPlayers is < 1 or > 64 ||
            !int.TryParse(SaveIntervalBox.Text, out var saveInterval) || saveInterval < 30 ||
            !int.TryParse(BackupCountBox.Text, out var backupCount) || backupCount < 0 ||
            !int.TryParse(ShortBackupIntervalBox.Text, out var shortBackup) || shortBackup < 1 ||
            !int.TryParse(LongBackupIntervalBox.Text, out var longBackup) || longBackup < 1)
        {
            MessageBox.Show("请检查服务器名称、世界名称、密码、端口、最大玩家数及保存备份参数。", "设置错误",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(SaveDirectoryBox.Text) || string.IsNullOrWhiteSpace(LogFilePathBox.Text))
        {
            MessageBox.Show("存档目录和日志文件路径不能为空。", "设置错误",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _config.ServerName = ServerNameBox.Text.Trim();
        _config.WorldName = WorldNameBox.Text.Trim();
        _config.Password = PasswordBox.Password;
        _config.Port = port;
        _config.MaxPlayers = maxPlayers;
        _config.PublicServer = PublicBox.IsChecked == true;
        _config.Crossplay = CrossplayBox.IsChecked == true;
        _config.SaveDirectory = Path.GetFullPath(SaveDirectoryBox.Text.Trim());
        _config.LogFilePath = Path.GetFullPath(LogFilePathBox.Text.Trim());
        _config.InstanceId = InstanceIdBox.Text.Trim();
        _config.SaveInterval = saveInterval;
        _config.BackupCount = backupCount;
        _config.ShortBackupInterval = shortBackup;
        _config.LongBackupInterval = longBackup;
        _config.WorldPreset = Selected(WorldPresetBox, "Normal");
        _config.CombatModifier = Selected(CombatModifierBox, "default");
        _config.DeathPenaltyModifier = Selected(DeathPenaltyModifierBox, "default");
        _config.ResourcesModifier = Selected(ResourcesModifierBox, "default");
        _config.RaidsModifier = Selected(RaidsModifierBox, "default");
        _config.PortalsModifier = Selected(PortalsModifierBox, "default");
        _config.NoBuildCost = NoBuildCostBox.IsChecked == true;
        _config.PlayerEvents = PlayerEventsBox.IsChecked == true;
        _config.PassiveMobs = PassiveMobsBox.IsChecked == true;
        _config.NoMap = NoMapBox.IsChecked == true;
        DialogResult = true;
    }

    private static string Selected(ComboBox box, string fallback) =>
        (box.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? fallback;

    private void UpdateLaunchPreview()
    {
        if (LaunchCommandBox is null) return;
        var args = _config.BuildArguments()
            .Select(QuoteIfNeeded)
            .ToArray();
        LaunchCommandBox.Text = "valheim_server.exe " + string.Join(" ", args);
    }

    private static string QuoteIfNeeded(string value) =>
        value.Contains(' ') ? $"\"{value.Replace("\"", "\\\"")}\"" : value;

    private void CopyCommandClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(LaunchCommandBox.Text)) return;
        try { Clipboard.SetText(LaunchCommandBox.Text); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "复制命令失败", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}
