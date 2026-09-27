namespace Ornare.Core;

/// <summary>
/// Platform-independent contract for rendering Unicode emoji grapheme clusters into raw BGRA pixels (Section 25).
/// </summary>
public interface IEmojiRasterizer
{
    /// <summary>
    /// Renders the specified Unicode emoji grapheme cluster to raw 32-bit PBGRA pixels of targetSize x targetSize.
    /// </summary>
    OperationResult<byte[]> RenderEmojiBgra(string graphemeSequence, int targetSize);
}
