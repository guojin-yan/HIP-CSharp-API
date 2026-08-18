# 综合案例

<p align="center"><a href="README.md">English</a> | <strong>简体中文</strong></p>

综合案例把多个 HIP 功能模块组合为完整工作负载。它们属于应用级案例，不属于编号教程主线，
也不承担发布验证门禁职责。

| 案例 | 工作负载 | 主要能力 |
| --- | --- | --- |
| [`HeatDiffusion`](HeatDiffusion/README.zh-CN.md) | 带 CPU 参考实现的二维热扩散模拟 | HIPRTC、设备内存、Stream、Event、Graph 重放、结果校验和 BMP 热力图 |
| [`VisualInspection`](VisualInspection/README.zh-CN.md) | OpenCV CPU 参考与 AMD GPU 缺陷掩码流水线 | OpenCV 图像读写与阈值处理、HIPRTC、锁页/设备内存、Stream、Event、Graph 重放、PNG 掩码以及 CSV/JSON 证据 |

`HeatDiffusion` 是独立于 OpenCV 验证的 P0 热扩散旗舰案例。`VisualInspection` 是 `CasePlan`
中的 P1 视觉案例，使用已发布的
[`OpenCV-CSharp-API`](https://github.com/guojin-yan/OpenCV-CSharp-API) 包，并在代码目录旁提供
Radeon Cloud 运行脚本。

| CasePlan 优先级 | 案例 | Showcase 状态 |
| --- | --- | --- |
| P0 热场 | 热扩散 / 热异常监测 | 已实现并完成云端验证 |
| P1 视觉 | 工业缺陷分割 | 已结合 OpenCV-CSharp-API 实现并完成云端验证 |
| P2 振动 | 时域/频域健康分析 | 等待 rocFFT 绑定后实现 |
| P3 推理 | 模型推理流水线 | 等待 MIGraphX 或 ONNX Runtime 集成后实现 |
