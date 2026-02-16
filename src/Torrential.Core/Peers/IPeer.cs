using System.IO.Pipelines;

namespace Torrential.Core.Peers;

public interface IPeer
{
    PipeReader Reader { get; }
    PipeWriter Writer { get; }
}
