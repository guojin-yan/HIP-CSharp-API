# HIP-CSharp-API 中文文章规划

本目录是 HIP-CSharp-API 的中文公开文章入口和文章路线规划。文章内容以项目总体方案为基线，围绕 AMD HIP Runtime、HIPRTC、Direct C ABI、.NET 多目标框架、NuGet Runtime 包和 Radeon Cloud 验证组织，不把本地方案、开发日记、云端连接信息或内部临时日志直接复制到公开文章。

## 1. 模块结构

| 模块 | 内容边界 | 首要读者 |
| --- | --- | --- |
| `01-release` | 版本发布、升级影响、支持边界和已知限制 | 包使用者、升级者 |
| `02-samples` | `samples/` 中单一目标、可复现的小案例 | C# / .NET 开发者 |
| `03-applications` | 从环境准备到验证输出的完整工作流 | 应用开发者、部署工程师 |
| `04-api` | 按 HIP 对象职责和生命周期解释公开 API | API 使用者、贡献者 |
| `05-installation` | ROCm/HIP SDK、Runtime 包、平台和加载排错 | 环境维护者、NuGet 使用者 |
| `06-source-build` | 绑定生成、ABI probe、托管构建、原生辅助和打包 | 项目贡献者 |
| `07-misc` | 项目背景、架构、兼容策略和证据边界 | 评审者、技术作者 |
| `publishing` | 文章写作规范、发布路线和机器可读规划 | 维护者、文章作者 |

文章索引见 [`article-index.json`](article-index.json)。模块 README 只登记选题和文章边界；没有对应源码、输出或证据前，条目保持 `planned`，不创建“已完成”叙述。

## 2. 单篇文章结构

所有 canonical 文章按以下顺序组织，具体文章可以按主题合并小节，但不能省略证据边界：

1. 前言：项目定位、源码仓库、核心包、Runtime 包或官方 HIP/ROCm 依赖、本文程序入口。
2. 目标与边界：本文解决什么问题，哪些能力不在本文范围内。
3. 环境与前置条件：OS、架构、.NET TFM、ROCm/HIP 版本、GPU 要求和包来源。
4. API / 源码映射：对应的公开类型、native 函数、示例目录和关键文件。
5. 实现或操作步骤：命令、代码、资源所有权和生命周期说明。
6. 验证与输出：实际输出、退出码、报告或截图；没有真实运行时必须明确写“静态检查结果”或“期望输出”。
7. 兼容性与限制：Build、Package、Runtime-tested、GPU-validated、Supported 的状态不能混用。
8. 结论与文章声明：不承诺未验证的平台、GPU、ROCm 组合，不链接尚未发布的下一篇文章。

## 3. 事实和证据边界

- 方案中的目标、路线和待决事项可以用于规划，但不能替代运行证据。
- Radeon Cloud 单次通过只证明对应 commit、环境和测试批次；不能自动升级为 Windows 支持、性能承诺或 `1.0.0` 发布授权。
- `hipGetDeviceCount`、加载器成功、包还原成功或 `--help` 输出都不能单独表示 GPU 功能完成。
- 每篇需要运行结果的文章都应关联脱敏后的 record、命令、版本、Git SHA 和必要的输出摘要；敏感云端信息只保留在仓库外的 `Radeon_Cloud/`。

## 4. 与现有文档的关系

`docs/guides`、`docs/design`、`docs/compatibility`、`docs/releases` 和 `docs/api` 是项目文档站的长期技术文档；本目录面向连续阅读、案例复现和对外文章规划。文章可以引用这些文档，但不复制内部发布门禁或未公开记录。

文章状态统一使用：`planned`、`draft`、`review`、`ready`、`published`。进入 `published` 后保留正文哈希和发布提交；更正使用新文章和 `supersedes` 关系。

