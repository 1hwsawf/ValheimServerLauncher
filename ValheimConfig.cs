using System.IO;
using System.Text.Json;

namespace ValheimServerLauncher;

public sealed class ValheimConfig
{
    public const string SteamAppId = "896660";
    public static string DataRoot => Path.Combine(AppContext.BaseDirectory, "ValheimServerLauncherData");
    public static string ConfigPath => Path.Combine(DataRoot, "launcher.json");
    public string ServerRoot { get; set; } = DataRoot;
    public string ServerName { get; set; } = "Valheim Dedicated Server";
    public string WorldName { get; set; } = "Dedicated";
    public string Password { get; set; } = "change-me";
    public int Port { get; set; } = 2456;
    public int MaxPlayers { get; set; } = 10;
    public bool PublicServer { get; set; } = true;
    public bool Crossplay { get; set; } = false;
    public string LogFilePath { get; set; } = Path.Combine(DataRoot, "server.log");
    public int SaveInterval { get; set; } = 1800;
    public int BackupCount { get; set; } = 4;
    public int ShortBackupInterval { get; set; } = 7200;
    public int LongBackupInterval { get; set; } = 43200;
    public string InstanceId { get; set; } = "";
    public string WorldPreset { get; set; } = "Normal";
    public string CombatModifier { get; set; } = "default";
    public string DeathPenaltyModifier { get; set; } = "default";
    public string ResourcesModifier { get; set; } = "default";
    public string RaidsModifier { get; set; } = "default";
    public string PortalsModifier { get; set; } = "default";
    public bool NoBuildCost { get; set; }
    public bool PlayerEvents { get; set; }
    public bool PassiveMobs { get; set; }
    public bool NoMap { get; set; }
    public bool UpdateOnStart { get; set; } = true;
    public bool AutoUpdateEnabled { get; set; } = true;
    public int UpdateCheckMinutes { get; set; } = 30;
    public bool RestartOnCrash { get; set; } = true;
    public bool ValidateOnRestart { get; set; } = false;
    public bool ScheduledRestartEnabled { get; set; } = false;
    public List<string> RestartTimes { get; set; } = new() { "05:00", "17:00" };
    public bool ScheduledBackupEnabled { get; set; } = false;
    public List<string> BackupTimes { get; set; } = new() { "04:00" };
    public int BackupRetentionDays { get; set; } = 7;
    public bool BackupOnStartup { get; set; } = true;
    public bool BackupOnShutdown { get; set; } = true;
    public bool HealthCheckEnabled { get; set; } = true;
    public int HealthCheckSeconds { get; set; } = 10;
    public int MinUptimeHours { get; set; } = 2;
    public bool AnnounceRestartsEnabled { get; set; } = true;
    public int AnnounceLead1 { get; set; } = 15;
    public int AnnounceLead2 { get; set; } = 5;
    public int AnnounceLead3 { get; set; } = 1;
    public string AnnouncementText { get; set; } = "服务器将在 {minutes} 分钟后重启。";
    public string BackupPath { get; set; } = "";
    public bool CompactMode { get; set; }
    public bool IsIpRevealed { get; set; } = true;
    public string Language { get; set; } = "";
    public bool StartAtLogin { get; set; } = false;
    public bool AutoReconnectSingleInstance { get; set; } = true;
    public bool HideSteamCmdWindow { get; set; } = true;
    public bool LogHealthStats { get; set; } = true;
    public bool WarnUnknownServers { get; set; } = true;
    public bool ClearLogsOnManualStart { get; set; } = true;
    // Kept only for backwards-compatible deserialization of older launcher.json files.
    public string DiscordWebhookUrl { get; set; } = "";
    public bool DiscordNotifyLifecycle { get; set; } = false;
    public bool DiscordNotifyPlayers { get; set; } = false;
    public bool VersionPinEnabled { get; set; } = false;
    public string PinnedBuild { get; set; } = "";

    public string ServerDirectory => Path.Combine(ServerRoot, "server");
    public string SaveDirectory { get; set; } = Path.Combine(DataRoot, "saves");
    public string BackupDirectory => string.IsNullOrWhiteSpace(BackupPath)
        ? Path.Combine(ServerRoot, "backups")
        : Path.GetFullPath(BackupPath);
    public string SteamCmdDirectory => Path.Combine(ServerRoot, "steamcmd");
    public string ServerExecutable => Path.Combine(ServerDirectory, "valheim_server.exe");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static ValheimConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
                return JsonSerializer.Deserialize<ValheimConfig>(File.ReadAllText(ConfigPath), JsonOptions) ?? new();
        }
        catch
        {
            // Start with defaults when the settings file is damaged.
        }
        return new();
    }

    public void Save()
    {
        Directory.CreateDirectory(DataRoot);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, JsonOptions));
    }

    public IReadOnlyList<string> BuildArguments()
    {
        var args = new List<string>
        {
            "-nographics", "-batchmode",
            "-name", ServerName,
            "-port", Port.ToString(),
            "-world", WorldName,
            "-password", Password,
            "-public", PublicServer ? "1" : "0",
            "-savedir", Path.GetFullPath(SaveDirectory),
            "-logFile", Path.GetFullPath(LogFilePath),
            "-saveinterval", SaveInterval.ToString(),
            "-backups", BackupCount.ToString(),
            "-backupshort", ShortBackupInterval.ToString(),
            "-backuplong", LongBackupInterval.ToString()
        };
        if (Crossplay) args.Add("-crossplay");
        if (!string.IsNullOrWhiteSpace(InstanceId))
        {
            args.Add("-instanceid");
            args.Add(InstanceId.Trim());
        }
        if (!string.IsNullOrWhiteSpace(WorldPreset) && !WorldPreset.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            args.Add("-preset");
            args.Add(WorldPreset.ToLowerInvariant());
        }

        AddModifier(args, "combat", CombatModifier);
        AddModifier(args, "deathpenalty", DeathPenaltyModifier);
        AddModifier(args, "resources", ResourcesModifier);
        AddModifier(args, "raids", RaidsModifier);
        AddModifier(args, "portals", PortalsModifier);
        if (NoBuildCost) AddWorldKey(args, "nobuildcost");
        if (PlayerEvents) AddWorldKey(args, "playerevents");
        if (PassiveMobs) AddWorldKey(args, "passivemobs");
        if (NoMap) AddWorldKey(args, "nomap");
        return args;
    }

    private static void AddModifier(List<string> args, string name, string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals("default", StringComparison.OrdinalIgnoreCase))
            return;
        args.Add("-modifier");
        args.Add(name);
        args.Add(value.ToLowerInvariant());
    }

    private static void AddWorldKey(List<string> args, string key)
    {
        args.Add("-setkey");
        args.Add(key);
    }

    public IReadOnlyList<TimeOnly> ParsedRestartTimes() => ParseTimes(RestartTimes);
    public IReadOnlyList<TimeOnly> ParsedBackupTimes() => ParseTimes(BackupTimes);

    private static IReadOnlyList<TimeOnly> ParseTimes(IEnumerable<string> values) =>
        values.Select(value => TimeOnly.TryParse(value, out var time) ? (TimeOnly?)time : null)
              .Where(time => time.HasValue).Select(time => time!.Value).OrderBy(time => time).ToArray();
}
