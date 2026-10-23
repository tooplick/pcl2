# 更新版本存档

- 归档原始可执行文件统一存放于 `versions/v<版本号>/`。
- 所有 README raw 下载链接都必须包含 `versions/` 路径段。
- `versions/` 里仅保留官方原始 exe，不要提交 PCL 运行目录、日志、配置或 Minecraft 数据。
- 添加文件时对照来源仓库历史 blob/hash，确认二进制未被修改。

# 补丁 GUI

- `PclDownloadPatch/` 是第三方 WinForms 工具源码，只处理 PCL 2.99.0.0。
- 补丁 exe 不属于历史版本归档；不要放进 `versions/`。
- 修改补丁步骤时必须重新用原版 `versions/v2.99.0.0/Plain Craft Launcher 2.exe` 测试，JIT 校验需 436 个方法全部通过，并做启动烟测。
- 发布工具前检查 PCL 官方许可条款，并保留 Mono.Cecil MIT 许可。
