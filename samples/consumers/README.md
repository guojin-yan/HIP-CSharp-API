# Consumers / 包消费者

This directory contains applications that reference the published NuGet package instead of the source
project. Use it to distinguish package consumption from repository development samples.

Consumer applications also use an explicit `Program.Main(string[] args)` entry point so their process
contract matches the tutorial cases.

本目录中的应用通过 NuGet `PackageReference` 使用已发布包，不引用仓库源码，用于区分真实包消费
路径和仓库开发案例。消费者应用同样使用显式 `Program.Main(string[] args)` 入口，使其进程契约与
教程案例一致。
