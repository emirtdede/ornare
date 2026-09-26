namespace Ornare.Core;

/// <summary>
/// Storage contract required by ResetService to orchestrate folder reset
/// without coupling Ornare.Windows to concrete SQLite or storage implementation packages.
/// </summary>
public interface IResetStorage : IFolderOperationStorage
{
}
