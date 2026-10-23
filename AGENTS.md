# 更新版本存档

- 归档原始可执行文件统一存放于 `versions/v<版本号>/`。
- 所有 README raw 下载链接都必须包含 `versions/` 路径段。
- `versions/` 里仅保留官方原始 exe，不要提交 PCL 运行目录、日志、配置或 Minecraft 数据。
- 添加文件时对照来源仓库历史 blob/hash，确认二进制未被修改。
- 新增或替换归档文件时，同步把 SHA-256 追加到 `versions/CHECKSUMS.txt`；该文件必须保持 LF 行尾（`sha256sum -c` 依赖，已由 `.gitattributes` 钉死，不要改行尾）。

# 补丁 GUI

- `PclDownloadPatch/` 是第三方 WinForms 工具源码，目前仅实现了 PCL 2.99.0.0 的专用补丁策略。
- 补丁 exe 不属于历史版本归档；不要放进 `versions/`。
- 构建产物只由 CI（`build-patch-tool.yml`）自动提交到仓库根目录；不要手动提交 `PclDownloadPatch/bin/` 下的任何产物，改完源码直接推送即可，CI 会跟进构建。
- 修改补丁步骤时必须重新用原版 exe 测试（2.99.0.0 基准为 436 个方法），JIT 校验需全部方法通过，并做启动烟测。
- 发布工具前检查 PCL 官方许可条款，并保留 Mono.Cecil MIT 许可（`MONO-CECIL-LICENSE.txt`）及本工具自身的 MIT 声明（`PclDownloadPatch/LICENSE`）。

## 补丁版本策略

- PCL 发布新版本后，必须在 `PatchEngine.cs` 的版本策略登记表 `VersionStrategies` 中登记该版本的门槛并编写专用策略方法（参照 `PatchRestrictedBuild299` 的写法），按新版本的实际 IL 结构实现；不要把新版本的结构适配混入 299 策略。
- 新版本的专用策略实现并通过验证之前，未适配的版本（≥ 2.99.0.0）暂时沿用 2.99.0.0 的补丁方法 `PatchRestrictedBuild299`；若新版本 IL 结构已改变，补丁会因模式匹配失败而中止且不替换源文件，这是预期的安全回退。
- 新增任何版本策略后，必须先用对应版本的原版 exe 完整验证（JIT 校验全部通过 + 启动烟测），再发布工具。
