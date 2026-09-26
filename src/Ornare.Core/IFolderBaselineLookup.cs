namespace Ornare.Core;

using System.Threading.Tasks;

/// <summary>
/// Lookup contract for retrieving baseline customization records for folders.
/// </summary>
public interface IFolderBaselineLookup
{
    Task<FolderBaseline?> GetLatestBaselineAsync(string folderPath);
}
