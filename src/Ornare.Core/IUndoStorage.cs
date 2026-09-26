namespace Ornare.Core;

/// <summary>
/// Storage contract required by UndoService to orchestrate folder undo
/// without coupling Ornare.Windows to concrete SQLite or storage implementation packages.
/// </summary>
public interface IUndoStorage : IFolderOperationStorage
{
}
