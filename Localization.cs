using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace ValheimServerLauncher;

public static class Localization
{
    public static string CurrentLanguage { get; private set; } = "zh";
    public static bool IsChinese => CurrentLanguage.StartsWith("zh", StringComparison.OrdinalIgnoreCase);

    public static void Initialize(string? configuredLanguage)
    {
        CurrentLanguage = string.IsNullOrWhiteSpace(configuredLanguage)
            ? (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase) ? "zh" : "en")
            : configuredLanguage.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "zh" : "en";
    }

    public static string Text(string chinese, string english) => IsChinese ? chinese : english;

    public static string Translate(string value)
    {
        if (IsChinese || string.IsNullOrWhiteSpace(value)) return value;
        return value switch
        {
            "安装" => "Install",
            "导入" => "Import",
            "重启服务器" => "Restart server",
            "检查更新" => "Check updates",
            "校验文件" => "Validate files",
            "强制停止" => "Force stop",
            "服务器设置" => "Server settings",
            "模组" => "Mods",
            "服务器目录" => "Server directory",
            "命令" => "Command",
            "启动器设置" => "Launcher settings",
            "状态" => "Status",
            "已停止" => "Stopped",
            "运行中" => "Running",
            "未安装" => "Not installed",
            "更新" => "Updates",
            "未检查" => "Not checked",
            "公网 IP" => "Public IP",
            "紧凑模式" => "Compact mode",
            "重新启动" => "Restart",
            "定时重新启动" => "Scheduled restart",
            "最短运行时间" => "Minimum uptime",
            "小时" => "hours",
            "重启公告" => "Restart announcements",
            "分钟" => "minutes",
            "重启时校验文件" => "Validate on restart",
            "备份" => "Backups",
            "备份时机" => "Backup timing",
            "启动时" => "On startup",
            "关闭时" => "On shutdown",
            "定时" => "Scheduled",
            "保留" => "Keep",
            "天" => "days",
            "下次备份" => "Next backup",
            "立即备份" => "Back up now",
            "备份位置" => "Backup location",
            "默认" => "Default",
            "杂项" => "Miscellaneous",
            "固定服务器版本" => "Pin server version",
            "自动更新" => "Automatic updates",
            "启动/重启时更新" => "Update on start/restart",
            "检查更新间隔" => "Update check interval",
            "崩溃自动重启" => "Restart on crash",
            "健康检查" => "Health check",
            "检查间隔" => "Check interval",
            "秒" => "seconds",
            "常规" => "General",
            "启动器" => "Launcher",
            "服务器日志" => "Server log",
            "玩家" => "Players",
            "SteamCMD" => "SteamCMD",
            "服务器命令" => "Server command",
            "跳转到最新" => "Jump to latest",
            "清空日志" => "Clear log",
            "版本" => "Version",
            "运行时间" => "Uptime",
            "下次重启" => "Next restart",
            "语言" => "Language",
            "跟随系统" => "Follow system",
            "简体中文" => "Simplified Chinese",
            "保存" => "Save",
            "取消" => "Cancel",
            _ => value
        };
    }

    public static void ApplyTo(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            switch (child)
            {
                case TextBlock textBlock:
                    textBlock.Text = Translate(textBlock.Text);
                    break;
                case Button button when button.Content is string content:
                    button.Content = Translate(content);
                    break;
                case CheckBox checkBox when checkBox.Content is string content:
                    checkBox.Content = Translate(content);
                    break;
                case TabItem tabItem when tabItem.Header is string header:
                    tabItem.Header = Translate(header);
                    break;
            }
            ApplyTo(child);
        }
    }
}
