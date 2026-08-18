# PeerToPeerCopy / 多设备 P2P Copy

This sample checks a directed device pair, enables peer access, copies four bytes from device 1 to
device 0, and verifies the result. Fewer than two devices or a false capability is a controlled skip.

本案例检查有方向的设备对，启用 peer access，将四个字节从设备 1 复制到设备 0 并验证结果。设备
少于两个或 capability 为 false 时属于受控 skip。

```powershell
dotnet run --project samples/tutorials/06-MultiDevice/PeerToPeerCopy/PeerToPeerCopy.csproj -c Release
```
