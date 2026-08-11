using System;
using System.Collections.Generic;

namespace JYPPX.HipSharp.Graphs;

/// <summary>
/// 在 graph 与 executable owner 之间共享 stream capture 引用的资源 / Shares resources referenced during stream capture between a graph and its executable owners.
/// </summary>
internal sealed class HipGraphCaptureResources
{
    private readonly object _sync = new();
    private readonly List<IDisposable> _leases;
    private int _referenceCount = 1;

    internal HipGraphCaptureResources(List<IDisposable> leases)
    {
        _leases = leases ?? throw new ArgumentNullException(nameof(leases));
    }

    internal IDisposable AcquireReference()
    {
        lock (_sync)
        {
            if (_referenceCount == 0)
            {
                throw new ObjectDisposedException(nameof(HipGraphCaptureResources));
            }

            _referenceCount++;
            return new ResourceReference(this);
        }
    }

    internal void ReleaseInitialReference() => ReleaseReference();

    private void ReleaseReference()
    {
        lock (_sync)
        {
            if (_referenceCount == 0) return;
            if (_referenceCount > 1)
            {
                _referenceCount--;
                return;
            }

            for (int index = _leases.Count - 1; index >= 0; index--)
            {
                _leases[index].Dispose();
                _leases.RemoveAt(index);
            }

            _referenceCount = 0;
        }
    }

    private sealed class ResourceReference : IDisposable
    {
        private readonly object _sync = new();
        private HipGraphCaptureResources? _resources;

        internal ResourceReference(HipGraphCaptureResources resources) => _resources = resources;

        public void Dispose()
        {
            lock (_sync)
            {
                if (_resources is null) return;
                _resources.ReleaseReference();
                _resources = null;
            }
        }
    }
}
