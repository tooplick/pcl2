# PCL2 历史版本存档

本仓库用于保存 **PCL2（Plain Craft Launcher 2）** 的历史版本安装包，方便需要旧版本的用户下载与回溯。

> 本仓库用于 PCL2 历史版本归档，并附有一个独立的第三方补丁工具源码；不是 PCL2 官方仓库，也不提供官方支持。

## 文件来源

本仓库归档的所有安装包均取自 PCL 官方仓库 **[Meloong-Git/PCL](https://github.com/Meloong-Git/PCL)** 的历史提交（Historical Commits），即官方在过往版本中实际发布的原始文件。

- 获取方式：在官方仓库的历史提交记录中定位对应版本的发布提交，下载其中的 `最新正式版.zip`，原样解压，并将可执行文件存入 `versions/<版本号>/` 目录。
- 所有文件均未修改、未重新打包、未补充或删减任何内容；文件哈希可通过对比上游提交校验。

## 当前版本列表

| 版本 | 文件 | 大小 | 下载 |
| --- | --- | --- | --- |
| v2.99.0.0 | [Plain Craft Launcher 2.exe](versions/v2.99.0.0/Plain%20Craft%20Launcher%202.exe) | 5.70 MB | [点击下载](https://raw.githubusercontent.com/tooplick/pcl2/main/versions/v2.99.0.0/Plain%20Craft%20Launcher%202.exe) |
| v2.14.0.0 | [Plain Craft Launcher 2.exe](versions/v2.14.0.0/Plain%20Craft%20Launcher%202.exe) | 5.71 MB | [点击下载](https://raw.githubusercontent.com/tooplick/pcl2/main/versions/v2.14.0.0/Plain%20Craft%20Launcher%202.exe) |
| v2.13.1.1 | [Plain Craft Launcher 2.exe](versions/v2.13.1.1/Plain%20Craft%20Launcher%202.exe) | 5.48 MB | [点击下载](https://raw.githubusercontent.com/tooplick/pcl2/main/versions/v2.13.1.1/Plain%20Craft%20Launcher%202.exe) |
| v2.13.0.1 | [Plain Craft Launcher 2.exe](versions/v2.13.0.1/Plain%20Craft%20Launcher%202.exe) | 5.38 MB | [点击下载](https://raw.githubusercontent.com/tooplick/pcl2/main/versions/v2.13.0.1/Plain%20Craft%20Launcher%202.exe) |
| v2.12.8.2 | [Plain Craft Launcher 2.exe](versions/v2.12.8.2/Plain%20Craft%20Launcher%202.exe) | 5.33 MB | [点击下载](https://raw.githubusercontent.com/tooplick/pcl2/main/versions/v2.12.8.2/Plain%20Craft%20Launcher%202.exe) |
| v2.12.8.1 | [Plain Craft Launcher 2.exe](versions/v2.12.8.1/Plain%20Craft%20Launcher%202.exe) | 5.32 MB | [点击下载](https://raw.githubusercontent.com/tooplick/pcl2/main/versions/v2.12.8.1/Plain%20Craft%20Launcher%202.exe) |
| v2.12.7.3 | [Plain Craft Launcher 2.exe](versions/v2.12.7.3/Plain%20Craft%20Launcher%202.exe) | 4.90 MB | [点击下载](https://raw.githubusercontent.com/tooplick/pcl2/main/versions/v2.12.7.3/Plain%20Craft%20Launcher%202.exe) |

## 说明

> **注意**：2.99.0.0 及之后的归档版本中，PCL 的 MC（Minecraft）下载功能受限制。仓库另提供独立第三方补丁工具源码，详见下文；归档 exe 本身保持官方原样。

- 每个版本单独存放于 `versions/` 下以版本号命名的目录中（如 `versions/v2.13.1.1/`）；目录仅保存官方原始 exe，不包含运行时生成的日志、设置或游戏数据。
- 目录内的 `Plain Craft Launcher 2.exe` 即该版本的可执行文件，双击即可运行，无需安装。
- 新增历史版本时，请新建对应版本号目录并放入原始文件，同时在上方版本列表中补充一行。
- 贡献方式与命名约定请参阅 [贡献指南](CONTRIBUTING.md)。

## 第三方补丁工具源码

`PclDownloadPatch/` 包含 Windows GUI 补丁工具源码。

- **直接下载**：[PCL Download Patch.exe](https://raw.githubusercontent.com/tooplick/pcl2/main/PCL%20Download%20Patch.exe)（由 CI 自动构建并提交到仓库根目录，SHA-256 见提交信息）
- 源码构建方法见 [PclDownloadPatch/README.md](PclDownloadPatch/README.md)。

本工具是第三方独立制作，与 PCL 官方无关。使用和分发请自行确认符合 [PCL 分发许可](https://github.com/Meloong-Git/PCL/blob/main/LICENCE)；详见工具目录中的说明。

## 分发许可

PCL 的复制与分发须遵守《PCL 分发有限许可》与《PCL 存储库合理使用指南》：

> 《PCL 分发有限许可》
>
> 详见 <https://shimo.im/docs/rGrd8pY8xWkt6ryW#anchor-X0bo>
>
> 官方许可文件：<https://github.com/Meloong-Git/PCL/blob/main/LICENCE>

本仓库仅按该许可存档、分享官方发布的原始安装包，未作任何修改或二次打包。如本仓库说明与该许可条款不一致，以官方许可为准。

本仓库自身的说明文字不保留任何权利（CC0 1.0），归档的 PCL 文件不适用任何开源协议。完整条款见 [LICENSE.md](LICENSE.md)。

## 免责声明

所有版本文件均来源于 [Meloong-Git/PCL](https://github.com/Meloong-Git/PCL) 官方仓库的历史提交，版权归原作者所有。本仓库仅用于存档与分享，请勿用于商业用途；使用旧版本带来的风险由使用者自行承担。
