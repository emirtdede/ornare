using System;
using System.Threading.Tasks;

namespace Ornare.Core;

/// <summary>
/// Storage contract required by ApplyService to orchestrate durable folder icon application
/// without coupling Ornare.Windows to concrete SQLite storage classes.
/// </summary>
public interface IApplyStorage : IApplyLastStorage
{
    Task<LibraryAsset> AddOrUpdateAssetAsync(string sha256, string canonicalIcoPath, string sourceFormat, DateTimeOffset? now = null);
}
