# PCL 下载功能补丁工具
让新版本 PCL（Plain Craft Launcher 2）解除下载功能限制。

> 该工具是第三方项目，不代表 PCL 官方。`versions/` 中的归档文件保持官方原样。修改后的 exe 由用户自行生成，不应作为官方原版归档。

## 界面

把官方 PCL `.exe` 拖到窗口，或点击中央卡片选择文件，即可自动识别版本；页面显示识别出的版本，结束后显示成功、失败或「该版本无下载限制」。已应用过补丁的文件再次拖入会显示「该文件已应用补丁」，不会重复修改。

## 命令行模式

```text
PCL Download Patch.exe <输入exe> [输出exe]
```

- 仅给输入文件：与拖入窗口效果相同，补丁成功后直接替换原文件。
- 同时给输出路径：只生成补丁副本，不修改原文件（自动化验证用）。
- 退出码：`0` 补丁成功；`1` 无需处理（版本低于 2.99.0.0 或已打过补丁）；`2` 处理失败（错误信息输出到 stderr）。

## 版本策略

入口在 `PatchEngine.cs` 的 `ApplyVersionStrategy`，各版本策略集中登记在 `VersionStrategies` 数组（门槛版本 → 策略方法）：低于最早门槛版本（2.99.0.0）不调用补丁；匹配时取门槛不高于源版本的最新一条策略。PCL 发布新版本后，应编写专用策略方法并在登记表追加一行、先验证再发布（见仓库 `AGENTS.md` 的补丁版本策略约束）；专用策略就绪之前，未适配版本自动沿用最近的旧策略，若新版本 IL 结构已改变会因模式匹配失败而中止，不会替换源文件。

## 构建

需要 Windows、Visual Studio 2022（.NET 桌面开发工作负载）和 .NET Framework 4.8 Developer Pack。

1. 在 Visual Studio 中打开 `PclDownloadPatch.csproj`。
2. 还原并构建 `Release`。
3. 产物位于 `bin\Release\net48\PCL Download Patch.exe`，Mono.Cecil 依赖嵌入在 exe 中。CI 会把它复制到仓库根目录并提交，供访客直接下载。

本工具源码以 MIT 许可发布，声明见 [LICENSE](LICENSE)；Mono.Cecil 0.10.4 同样采用 MIT 许可，版权和许可文本见 `MONO-CECIL-LICENSE.txt`。

命令行构建：

```powershell
dotnet build .\PclDownloadPatch.csproj -c Release
```

发布前须分别测试低于 2.99.0.0 的原版文件（应只显示“该版本无下载限制”）和受限版文件（应验证补丁结果及直接替换行为）。不要把补丁版 exe 加入 `versions/`。
