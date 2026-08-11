using System;
using JYPPX.HipSharp.Interop;
using JYPPX.HipSharp.Streams;

namespace JYPPX.HipSharp.Memory;

/// <summary>
/// 为异步操作提供受控指针借用 / Provides a controlled pointer borrow for asynchronous operations.
/// </summary>
internal interface IHipPointerOwner
{
    public IHipNativeApi NativeApi { get; }
    public HipStream? RequiredStream { get; }
    public IntPtr AcquirePointer(out bool addedReference);
    public void ReleasePointer();
}
