# ArenaPilot 发布流程

ArenaPilot 使用四段版本号，例如 `0.2.4.0`。Git 标签、程序集版本、插件清单版本和 GitHub Release 必须一致。

## 发布前检查

1. 更新 `ArenaPilot.csproj`：
   - `Version` 使用三段显示版本，例如 `0.2.4`。
   - `AssemblyInformationalVersion` 使用相同三段版本。
   - `AssemblyVersion` 使用四段版本，例如 `0.2.4.0`。
2. 更新 `ArenaPilot.json` 的 `AssemblyVersion`。
3. 更新 UI 和控制器中的版本兜底值。
4. 更新 `repo.json` 的版本、下载链接和 `LastUpdated`。
5. 更新 `DESIGN.md` 中的版本及行为说明。
6. 执行：

```powershell
dotnet build ArenaPilot.csproj -c Release
```

7. 确认构建为 `0` 个错误，并检查：

```text
output/ArenaPilot.dll
output/ArenaPilot.json
output/ArenaPilot.deps.json
output/icon.png
```

## 提交与发布

1. 检查改动：

```powershell
git status --short
git diff
git log --oneline -10
```

2. 提交并推送 `main`：

```powershell
git add .
git commit -m "Release 0.2.4"
git push origin main
```

3. 创建并推送四段版本标签：

```powershell
git tag 0.2.4.0
git push origin 0.2.4.0
```

4. 标签推送后，`.github/workflows/release.yml` 会在 Windows Runner 上：
   - 安装 .NET 10。
   - 下载 Dalamud 开发程序集。
   - 按标签版本构建 Release。
   - 校验 DLL、清单、依赖文件和图标。
   - 生成 `ArenaPilot.zip`。
   - 创建同名 GitHub Release。

## 发布后检查

1. 打开 GitHub Actions，确认 `Create Release` 成功。
2. 打开对应 Release，确认 `ArenaPilot.zip` 可下载。
3. 解压 ZIP，确认包含：

```text
ArenaPilot.dll
ArenaPilot.json
ArenaPilot.deps.json
icon.png
```

4. 检查 `repo.json` 中的下载链接与 Release 标签一致。
5. 在 Dalamud 开发插件中加载发布 ZIP 的 DLL，验证 `/斗兽塔`、配置窗口和版本显示。

## 失败处理

- 工作流构建失败时，修复代码并创建新的四段版本号，不覆盖或强制移动已发布标签。
- 不修改已发布 Release 的历史产物；使用补丁版本重新发布。
- 不在仓库中提交 `output/`、`obj/` 或本地插件配置文件。
