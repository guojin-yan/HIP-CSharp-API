# 显式 Stream/Event VectorAdd / Explicit Stream/Event VectorAdd

`HipStreamEventVectorAdd` is the M3 vertical sample carried into the M4 release-candidate audit. It uses two explicit streams, records start/end events, queues asynchronous host-to-device copies, an explicit-stream HIPRTC kernel launch, and device-to-host copy, then synchronizes before a CPU element-by-element comparison. The sample also exercises the exact-once disposal path at least 100 times.

`HipStream` owns its native handle and keeps pending memory, module, kernel-argument, and pinned-host leases until `Synchronize` or a successful `Query`. `HipEvent.Dispose` synchronizes an incomplete event before destroy and can be retried after a native error. These rules are deliberately visible in the sample so a cloud record can prove ownership rather than rely on `GC.KeepAlive`.

`HipRuntime.GetDeviceAttribute` exposes only the manifest's ABI-verified scalar attributes; it does not expose the version-sensitive `hipDeviceProp_t` structure.
