namespace Torrential.Application.FileCopy;

public interface IFileCopyService
{
    Task CopyFilesAsync(InfoHash infoHash, CancellationToken cancellationToken = default);
}
