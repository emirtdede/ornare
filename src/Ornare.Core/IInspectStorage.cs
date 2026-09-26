using System.Threading.Tasks;

namespace Ornare.Core;

/// <summary>
/// Storage contract required by InspectService for read-only folder inspection.
/// </summary>
public interface IInspectStorage : IFolderBaselineLookup
{
    Task<FolderAssignment?> GetAssignmentByPathAsync(string folderPath);
}

