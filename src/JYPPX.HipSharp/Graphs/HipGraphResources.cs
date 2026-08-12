using System;
using System.Collections.Generic;

namespace JYPPX.HipSharp.Graphs;

/// <summary>在 graph 与 executable owners 之间共享资源 / Shares retained resources between graph and executable owners.</summary>
internal sealed class HipGraphResources
{
    private readonly object _sync = new();
    private readonly List<IDisposable> _leases;
    private int _referenceCount = 1;

    internal HipGraphResources(List<IDisposable> leases)
    {
        _leases = leases ?? throw new ArgumentNullException(nameof(leases));
    }

    internal void Add(IDisposable lease)
    {
        if (lease is null) throw new ArgumentNullException(nameof(lease));
        lock (_sync)
        {
            if (_referenceCount == 0) throw new ObjectDisposedException(nameof(HipGraphResources));
            _leases.Add(lease);
        }
    }

    internal IDisposable AcquireReference()
    {
        lock (_sync)
        {
            if (_referenceCount == 0) throw new ObjectDisposedException(nameof(HipGraphResources));
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

            while (_leases.Count != 0)
            {
                int index = _leases.Count - 1;
                _leases[index].Dispose();
                _leases.RemoveAt(index);
            }
            _referenceCount = 0;
        }
    }

    private sealed class ResourceReference : IDisposable
    {
        private readonly object _sync = new();
        private HipGraphResources? _resources;

        internal ResourceReference(HipGraphResources resources) => _resources = resources;

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
