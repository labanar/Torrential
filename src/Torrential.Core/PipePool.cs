using Microsoft.Extensions.ObjectPool;
using System.IO.Pipelines;

namespace Torrential.Core;

public static class PipePool
{
    public readonly static ObjectPool<Pipe> Shared = new DefaultObjectPool<Pipe>(new PipePoolPolicy());
}

internal sealed class PipePoolPolicy : PooledObjectPolicy<Pipe>
{
    public override Pipe Create()
    {
        return new Pipe();
    }

    public override bool Return(Pipe obj)
    {
        obj.Reset();
        return true;
    }
}
