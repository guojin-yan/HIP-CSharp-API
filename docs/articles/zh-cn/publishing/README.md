# HIP-CSharp-API 文章发布规划

`publishing/` 保存文章路线、写作规范和发布目录，不代表任何文章已经发布，也不构成 NuGet、GitHub Release 或 `1.0.0` 授权。

## 文件职责

| 文件 | 用途 |
| --- | --- |
| [`article-roadmap.md`](article-roadmap.md) | 按 M0-M8 排列首轮文章和依赖 |
| [`public-article-writing-spec.md`](public-article-writing-spec.md) | 标题、结构、链接、输出、图片和声明规范 |
| [`../article-index.json`](../article-index.json) | 机器可读的 canonical 文章规划和状态 |

## 发布门槛

文章只能引用已公开或已脱敏的事实。涉及 GPU、Runtime 包、ABI、许可证和版本的内容必须绑定 exact Git SHA、环境版本和证据摘要；Windows GPU 验证完成前，所有文章都不得暗示 `1.0.0` 已可发布。

