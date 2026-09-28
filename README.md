<p>
<img src="https://github.com/user-attachments/assets/8bca5925-02b6-420b-84cd-35b1794d63c8" style="max-width:32%;height:auto;">
<img src="https://github.com/user-attachments/assets/f70d01ab-daf3-4309-841a-8df444adca81" style="max-width:32%;height:auto;">
<img src="https://github.com/user-attachments/assets/4dc915f9-7608-4512-a1ba-fdb83c632f5f" style="max-width:32%;height:auto;">
</p>
# 英灵神殿服务器启动器

这是基于 `PalworldServerLauncher-1.5.1` 的架构思路重做的 Valheim Windows WPF 启动器。

## 功能

- 自动下载 SteamCMD，并通过 Steam AppID `896660` 安装或更新 Valheim Dedicated Server
- 启动参数编辑：名称、世界、密码、端口、公开服务器、Crossplay、保存与备份间隔
- 启动前更新、崩溃自动重启
- 捕获服务器和 SteamCMD 日志
- 通过服务器标准输入执行 `save` / `shutdown`
- 世界保存目录 ZIP 备份
- 启动参数预览和复制

## 编译

在 `ValheimServerLauncher` 目录运行：

```powershell
dotnet build
dotnet publish -c Release
```

启动器会把 SteamCMD、服务器、存档、备份和配置放到 exe 旁边的
`ValheimServerLauncherData` 目录中。
