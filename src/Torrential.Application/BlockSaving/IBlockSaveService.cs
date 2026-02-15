namespace Torrential.Application.BlockSaving;

public interface IBlockSaveService
{
    Task SaveBlock(PooledBlock block);
}
