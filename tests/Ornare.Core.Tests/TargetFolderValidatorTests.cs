using System.IO;
using Ornare.Core;
using Xunit;

namespace Ornare.Core.Tests;

public class TargetFolderValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateTargetEmptyOrNullReturnsInvalidTarget(string? path)
    {
        var result = TargetFolderValidator.ValidateTarget(path);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidTarget, result.ErrorCode);
    }

    [Fact]
    public void ValidateTargetUncPathReturnsTargetNotLocal()
    {
        var result = TargetFolderValidator.ValidateTarget(@"\\server\share\folder");
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.TargetNotLocal, result.ErrorCode);
    }

    [Fact]
    public void ValidateTargetVolumeRootReturnsTargetIsVolumeRoot()
    {
        string root = Path.GetPathRoot(Environment.SystemDirectory)!;
        var result = TargetFolderValidator.ValidateTarget(root);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.TargetIsVolumeRoot, result.ErrorCode);
    }

    [Fact]
    public void ValidateTargetNonExistentReturnsTargetNotFound()
    {
        string fakePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var result = TargetFolderValidator.ValidateTarget(fakePath);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.TargetNotFound, result.ErrorCode);
    }

    [Fact]
    public void ValidateTargetValidTempDirectoryReturnsSuccess()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "ornare_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var result = TargetFolderValidator.ValidateTarget(tempDir);
            Assert.True(result.IsSuccess);
            Assert.Equal(ErrorCode.None, result.ErrorCode);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ValidateTargetExcessivelyLongPathReturnsInvalidTarget()
    {
        string longPath = @"C:\" + new string('a', 265);
        var result = TargetFolderValidator.ValidateTarget(longPath);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidTarget, result.ErrorCode);
    }

    [Fact]
    public void ValidateTargetProtectedWindowsFolderReturnsAccessDenied()
    {
        string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var result = TargetFolderValidator.ValidateTarget(winDir);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.AccessDenied, result.ErrorCode);
    }

    [Fact]
    public void ValidateTargetProtectedProgramFilesFolderReturnsAccessDenied()
    {
        string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var result = TargetFolderValidator.ValidateTarget(progFiles);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.AccessDenied, result.ErrorCode);
    }
}
