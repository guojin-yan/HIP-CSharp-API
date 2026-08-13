using System;

namespace JYPPX.ROCm.HipSharp.Streams;

/// <summary>
/// 记录异步操作的托管保活引用 / Records managed keep-alive references for an async operation.
/// </summary>
internal sealed class HipAsyncLease : IDisposable
{
    private readonly object _sync = new();
    private Action? _release;

    internal HipAsyncLease(Action release)
    {
        _release = release ?? throw new ArgumentNullException(nameof(release));
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_release is null) return;
            _release();
            _release = null;
        }
    }
}
