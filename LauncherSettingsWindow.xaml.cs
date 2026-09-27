using System.Windows;
using System.Windows.Controls;

namespace ValheimServerLauncher;

public partial class LauncherSettingsWindow : Window
{
    private readonly ValheimConfig _config;

    public LauncherSettingsWindow(ValheimConfig config)
    {
        InitializeComponent();
        Localization.ApplyTo(this);
        _config = config;
        SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
        AutoReconnectBox.IsChecked = _config.AutoReconnectSingleInstance;
        StartAtLoginBox.IsChecked = _config.StartAtLogin;
        HideSteamCmdBox.IsChecked = _config.HideSteamCmdWindow;
        LogHealthStatsBox.IsChecked = _config.LogHealthStats;
        WarnUnknownServersBox.IsChecked = !_config.WarnUnknownServers;
        ClearLogsBox.IsChecked = _config.ClearLogsOnManualStart;
        LanguageBox.SelectedIndex = _config.Language.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
            ? 1
            : _config.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? 2 : 0;
    }

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        _config.AutoReconnectSingleInstance = AutoReconnectBox.IsChecked == true;
        _config.StartAtLogin = StartAtLoginBox.IsChecked == true;
        _config.HideSteamCmdWindow = HideSteamCmdBox.IsChecked == true;
        _config.LogHealthStats = LogHealthStatsBox.IsChecked == true;
        _config.WarnUnknownServers = WarnUnknownServersBox.IsChecked != true;
        _config.ClearLogsOnManualStart = ClearLogsBox.IsChecked == true;
        _config.Language = (LanguageBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
        _config.Save();
        if (_config.Language != Localization.CurrentLanguage &&
            !(_config.Language.Length == 0 && (Localization.IsChinese ? "zh" : "en") == Localization.CurrentLanguage))
        {
            MessageBox.Show("语言设置将在重新启动启动器后生效。", "语言设置",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        DialogResult = true;
    }
}
