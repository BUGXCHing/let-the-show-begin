# 构建、测试与交付

## 环境

使用 `ProjectSettings/ProjectVersion.txt` 指定的 Unity 6000.6.3f1。第一次打开时由 Package Manager 下载锁定依赖。导出网页还需通过 Unity Hub 安装 Web Build Support。

以下批处理步骤应在交互式 Unity Editor 关闭后执行，且不要对同一个工程同时启动两个 Unity 进程。macOS 工具链默认按对应版本的 Hub 路径定位；其他安装位置可设置 `UNITY_EDITOR` 为编辑器可执行文件。

## 三层验证

| 层级 | 方法 | 能说明什么 |
| --- | --- | --- |
| 公式回归 | Window → General → Test Runner → EditMode；或下面的 tests 命令 | 已知刃速的基础分档、速度／质量对伤害的影响、极端速度的上限。 |
| 玩法多帧自检 | 好戏开演 → 运行玩法自检；或 gameplay 命令 | 双触点、移动、刀刃扫掠／格挡、敌人命中、部位损伤、拾取、倒地／起身／复位等。 |
| 动作多帧自检 | 好戏开演 → 动作系统自检；或 motion 命令 | 待机姿势、移动反转、原地旋转换脚、骨长误差与关节稳定性；保存诊断截图。 |

```sh
bash tools/unity.sh tests
bash tools/unity.sh gameplay
bash tools/unity.sh motion
```

命令成功返回 0；失败返回非零。日志在 `Logs/Unity-*.log`，NUnit 报告在 `Logs/EditMode.xml`；动作报告在 `Logs/MotionQA/result.txt`。交互式 Editor 中运行动作自检还会保存截图；无图形批处理不保证生成截图，不要把目录中旧截图当作本次证据。这些诊断属于本机产物，不提交到 Git。

玩法自检既含真实扫掠，又含直接注入受击的隔离场景。前者允许“命中或被武器挡住”作为合法结果；后者验证状态机，不证明人能用当前摇杆精确瞄准某个高度。动作自检不代替优雅程度的主观验收。

输入、碰撞或参数改动后，额外手动验证：走近／拉开距离、画慢弧／快弧、格挡、击倒后起身、换武器、增加多个敌人、暂停／重开。手机应使用两根手指同时控制两个摇杆；桌面测试无法替代真机浏览器。

## 构建网页

菜单“好戏开演 → 构建 WebGL试玩包”，或：

```sh
bash tools/unity.sh build
python3 tools/serve_webgl.py --port 8765
```

打开 `http://127.0.0.1:8765/`，首次加载完成后实际进入战斗。构建只包含 `HaoxiArena` 场景。工具与 NUnit 测试在 Editor 目录／程序集，探针组件通过 `UNITY_EDITOR` 条件编译排除，不进入 WebGL。编辑源码不会自动更新已有网页，必须重新构建。

WebGL 使用 Brotli 压缩。部署服务器须给 `.br` 资源发送 `Content-Encoding: br`，WASM 类型为 `application/wasm`，JS 类型为 `application/javascript`。`serve_webgl.py` 已实现这些头，并支持文件 URL 带查询字符串。若托管服务不能配置这些头，可改 Unity 压缩设置后重建，或使用支持这些配置的静态托管。

若增量构建出现原生链接重复符号或过期 IL2CPP 符号，先检查错误指向的生成文件，不要修改玩法来掩盖缓存问题。可运行 `bash tools/unity.sh clean-build`，通过 Unity 的 `BuildOptions.CleanBuildCache` 清理构建缓存后重建；它比正常增量构建慢，不应每次使用。源码包不会分发 Library 或 Data 下的旧编译产物。

## 导出干净的源码和网页包

```sh
python3 tools/package_release.py --kind source
python3 tools/package_release.py --kind web
# 两份一起导出，需要已构建网页
python3 tools/package_release.py --kind all
```

输出在 `Releases/`，文件名带时间戳并打印 SHA-256。源码包保留 Assets、Packages、ProjectSettings、文档、工具和 GitHub 模板；排除缓存、日志、用户布局、旧构建与账号状态。网页包只收集 `index.html` 实际引用的四个构建文件及模板资源，避免历史重复 WASM；附带本地服务器脚本。

解压源码到新目录，Unity Hub 添加该目录即可还原工程。解压网页包后，在包根目录运行 `python3 tools/serve_webgl.py` 即可通过本机 HTTP 试玩。

## 发布到 GitHub 时的组织方式

源码提交到仓库；源码 ZIP 与 WebGL ZIP 可作为 Release 附件。`.gitignore` 默认忽略构建和发布包。README 不保存会失效的临时隧道地址；部署完成后再添加实际长期试玩链接。

目前提供本地可重复验证命令，没有已配置的云端 Unity CI。在线构建需要独立配置 Unity 许可、对应编辑器与 Web 模块。
