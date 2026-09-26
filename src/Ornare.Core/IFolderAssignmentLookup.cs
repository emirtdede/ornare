namespace Ornare.Core;

using System.Threading;
using System.Threading.Tasks;

public interface IFolderAssignmentLookup
{
    Task<FolderAssignment?> FindAssignmentAsync(NtfsFolderIdentity? identity, string canonicalPath, CancellationToken ct = default);
}
