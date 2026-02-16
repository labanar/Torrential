namespace Torrential.Files;

public interface IFileSelectionService
{
    Task<IReadOnlySet<long>> GetSelectedFileIds(InfoHash infoHash);
    Task SetSelectedFileIds(InfoHash infoHash, IReadOnlyCollection<long> fileIds);
}
