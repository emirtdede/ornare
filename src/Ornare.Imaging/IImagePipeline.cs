using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Ornare.Core;

namespace Ornare.Imaging;

public interface IImagePipeline
{
    bool ValidateImageFormat(string filePath);
    OperationResult<NormalizedImageResult> ProcessImage(string inputPath, string outputIcoPath, ImagePlacementMode placementMode = ImagePlacementMode.Contain);
    OperationResult<NormalizedImageResult> ProcessImageBytes(byte[] imageBytes, string outputIcoPath, ImagePlacementMode placementMode = ImagePlacementMode.Contain);

    // F18: Streaming hash and recipe rendering
    Task<string> ComputeSourceHashStreamAsync(Stream stream, CancellationToken cancellationToken = default);
    string ComputeSourceHash(Stream stream);
    string ComputeSourceHash(byte[] bytes);
    bool IsValidIco(string path, int? expectedMinFrames = null, long? expectedSize = null);
    OperationResult<NormalizedImageResult> ProcessRecipe(
        RenderRecipe recipe,
        byte[]? sourceBytes,
        string outputIcoPath,
        Func<string, RenderCacheEntry?>? cacheLookup = null,
        Action<RenderCacheEntry>? cacheStore = null,
        Action<string>? cacheInvalidate = null,
        string? cacheDirectory = null);
}
