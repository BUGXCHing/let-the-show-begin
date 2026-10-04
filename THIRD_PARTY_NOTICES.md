# 第三方资源与依赖

根目录的 MIT 许可适用于本项目自行编写的代码、脚本和说明文档。下列内容保留各自许可。

| 内容 | 来源 / 位置 | 许可与处理方式 |
| --- | --- | --- |
| 中文字体子集 `NotoSansSC-UI.ttf` | `Assets/Resources/` | SIL Open Font License 1.1；完整声明保留在同目录 `OFL-NotoSansSC.txt`。字体不改用 MIT 许可。 |
| Unity Editor / Player | 通过 Unity Hub 安装 | 遵循 Unity 自身条款；引擎本体不包含在源码包中。 |
| Input System、URP、uGUI、Test Framework 及其传递依赖 | `Packages/manifest.json` 与 `packages-lock.json` | 由 Unity Package Manager 下载；各包的 LICENSE / Third Party Notices 以所解析包内文件为准。源码包不包含 `Library/PackageCache`。 |
| URP 配置和项目设置 | `Assets/Settings/`、`ProjectSettings/` | 项目配置，使用 Unity 内置渲染管线；未分发 Unity 引擎实现。 |

人物、武器、舞台和击打音效由项目代码在运行时生成。没有崩坏系列、洛天依、MMD、Steam 创意工坊或 AI 生成的角色资产。先前下载但未实装的哥布林候选模型已从开源工程中移出。

WebGL 成品中的 Unity 运行时与模板内容仍遵循 Unity 的分发条款。给本项目换入其他模型、音频或字体时，应一并保留该资源的许可、作者和来源。
