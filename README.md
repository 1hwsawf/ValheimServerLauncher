<img width="986" height="673" alt="image" src="https://github.com/user-attachments/assets/0808dd9f-373a-428d-abe0-723d54230bc3" />
<img width="886" height="753" alt="image" src="https://github.com/user-attachments/assets/956f60bd-09ec-4e23-86c4-80608a816246" />



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
