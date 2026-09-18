ArenaPilot 源码

编译：
1. 安装 XIVLauncherCN，并确保已生成
   %APPDATA%\XIVLauncherCN\addon\Hooks\dev\Dalamud.dll
2. 安装 .NET 10 SDK
3. 在本目录执行：
   dotnet build ArenaPilot.csproj -c Release
4. 产物在 output\ArenaPilot.dll
5. 用 Dalamud 开发插件目录加载 output

游戏内命令：/斗兽塔
