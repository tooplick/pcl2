# 更新版本存档

- 归档原始可执行文件统一存放于 `versions/v<版本号>/`。
- 所有 README raw 下载链接都必须包含 `versions/` 路径段。
- `versions/` 里仅保留官方原始 exe，不要提交 PCL 运行目录、日志、配置或 Minecraft 数据。
- 添加文件时对照来源仓库历史 blob/hash，确认二进制未被修改。

# 补丁 GUI

- `PclDownloadPatch/` 是第三方 WinForms 工具源码，目前仅实现了 PCL 2.99.0.0 的专用补丁策略。
- 补丁 exe 不属于历史版本归档；不要放进 `versions/`。
- 修改补丁步骤时必须重新用原版 `versions/v2.99.0.0/Plain Craft Launcher 2.exe` 测试，JIT 校验需 436 个方法全部通过，并做启动烟测。
- 发布工具前检查 PCL 官方许可条款，并保留 Mono.Cecil MIT 许可。

## 补丁版本策略

- PCL 发布新版本后，必须在 `PatchEngine.cs` 的 `ApplyVersionStrategy` 中为该版本新增独立分支和专用策略方法（参照 `PatchRestrictedBuild299` 的写法），按新版本的实际 IL 结构实现；不要把新版本的结构适配混入 299 策略。
- 新版本的专用策略实现并通过验证之前，未适配的版本（≥ 2.99.0.0）暂时沿用 2.99.0.0 的补丁方法 `PatchRestrictedBuild299`；若新版本 IL 结构已改变，补丁会因模式匹配失败而中止且不替换源文件，这是预期的安全回退。
- 新增任何版本策略后，必须先用对应版本的原版 exe 完整验证（JIT 校验全部通过 + 启动烟测），再发布工具。
