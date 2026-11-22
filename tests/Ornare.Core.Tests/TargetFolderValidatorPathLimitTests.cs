using System.IO;
using Ornare.Core;
using Xunit;

namespace Ornare.Core.Tests;

public class TargetFolderValidatorPathLimitTests
{
    private const int MaxPathLength = 259;

    [Fact]
    public void ValidateTargetAtMaximumSupportedLengthIsNotRejectedForLength()
    {
        using var scope = new TempDirectory();

        string segment = new('a', MaxPathLength - scope.Path.Length - 1);
        string path = Path.Combine(scope.Path, segment);

        Assert.Equal(MaxPathLength, path.Length);

        var result = TargetFolderValidator.ValidateTarget(path);

        // The path does not exist, but it must fail on existence, not on length.
        Assert.Equal(ErrorCode.TargetNotFound, result.ErrorCode);
    }

    [Fact]
    public void ValidateTargetOneCharacterOverLimitIsRejectedAsInvalid()
    {
        using var scope = new TempDirectory();

        string segment = new('a', MaxPathLength + 1 - scope.Path.Length - 1);
        string path = Path.Combine(scope.Path, segment);

        Assert.Equal(MaxPathLength + 1, path.Length);

        var result = TargetFolderValidator.ValidateTarget(path);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidTarget, result.ErrorCode);
    }

    [Fact]
    public void ValidateTargetRejectsCloudPlaceholderFolder()
    {
        using var scope = new TempDirectory();
        string folder = Path.Combine(scope.Path, "Placeholder");
        Directory.CreateDirectory(folder);

        var info = new DirectoryInfo(folder);
        FileAttributes original = info.Attributes;

        try
        {
            info.Attributes |= FileAttributes.Offline;

            var result = TargetFolderValidator.ValidateTarget(folder);

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorCode.TargetIsCloudPlaceholder, result.ErrorCode);
        }
        finally
        {
            info.Attributes = original;
        }
    }

    [Fact]
    public void ValidateTargetAcceptsOrdinaryLocalFolder()
    {
        using var scope = new TempDirectory();
        string folder = Path.Combine(scope.Path, "Ordinary");
        Directory.CreateDirectory(folder);

        var result = TargetFolderValidator.ValidateTarget(folder);

        Assert.True(result.IsSuccess);
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ornare_validate_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Temp cleanup must never turn a passing test into a failure.
            }
        }
    }
}
