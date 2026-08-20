# HIP-CSharp-API 首轮文章路线

本路线是文章规划，不是发布审批、GPU 证明或 Release close。排序依据是项目总体方案的 M0-M8 里程碑和读者依赖关系。

## 1. 阶段安排

| 阶段 | 文章重点 | 主要模块 | 进入条件 |
| --- | --- | --- | --- |
| M0 | 项目总览、目录、版本与多目标策略 | `07-misc`、`01-release` | 工程骨架和命名冻结 |
| M1 | Direct C ABI、加载器、设备和内存 PoC | `02-samples`、`04-api` | 云端加载与设备枚举证据 |
| M2 | HIPRTC VectorAdd、Module、Kernel、Stream/Event | `02-samples`、`03-applications`、`04-api` | `gfx1100` 真实 GPU E2E |
| M3 | 生成器、ABI probe、核心托管 API | `04-api`、`06-source-build` | 生成确定性和 ABI 一致 |
| M4 | Core 包、clean consumer、DocFX 和接口状态 | `01-release`、`05-installation`、`06-source-build` | `0.x` 候选审计完成 |
| M5 | Linux Runtime 包、依赖闭包、许可证和 SBOM | `01-release`、`05-installation`、`06-source-build` | Runtime 包独立可用 |
| M6 | Memory Pool、Graph、Occupancy、Module globals 等高级 API | `04-api`、`07-misc` | 对应 API 和测试证据 |
| M7 | Windows loader、HIP SDK 和静态兼容边界 | `05-installation`、`07-misc` | 获得官方 Windows AMD GPU 环境后再晋级 |
| M8 | API 冻结、最终支持矩阵和 `1.0.0` 文章 | `01-release`、`publishing` | Windows GPU 验证和 Owner 明确发布指令同时满足 |

## 2. 写作顺序

推荐先写 `MSC-001`、`SMP-001`、`API-001`、`INS-001` 和 `BLD-001`，建立读者入口；再按 M1/M2 的真实程序输出写案例和 HIPRTC 文章；高级 API、Runtime 包和发布故事必须等待对应证据闭环。

## 3. 状态晋级

`planned` 只表示选题和路径已确定；`draft` 表示正文存在；`review` 表示需要技术或证据复核；`ready` 表示链接、代码和声明校验通过；`published` 还必须记录外部 URL、发布日期、commit 和正文 SHA-256。任何状态都不改变项目方案中的发布授权边界。

