using System.IO;
using Ornare.Core;
using Xunit;

namespace Ornare.Core.Tests;

/// <summary>
/// <see cref="LongPath"/> decides whether a Win32 call receives an extended-length path. Getting
/// it wrong is silent: the call fails with <c>ERROR_PATH_NOT_FOUND</c> for a path that exists, so
/// an icon write near the length limit would report a filesystem error that looks like a
/// permission problem.
/// </summary>
public class LongPathTests
{
    [Fact]
    public void ShortAbsolutePathIsUnchanged()
    {
        string path = Path.Combine(Path.GetTempPath(), "ornare");

        Assert.Equal(path, LongPath.ToExtended(path));
        Assert.False(LongPath.IsExtendedRequired(path));
    }

    [Fact]
    public void PathBeyondMaxPathGetsTheExtendedPrefix()
    {
        string path = @"C:\" + new string('a', 300) + @"\desktop.ini";

        string extended = LongPath.ToExtended(path);

        Assert.StartsWith(@"\\?\", extended, StringComparison.Ordinal);
        Assert.Equal(@"\\?\" + path, extended);
        Assert.True(LongPath.IsExtendedRequired(path));
    }

    [Fact]
    public void UncPathUsesTheUncForm()
    {
        string path = @"\\server\share\" + new string('b', 300);

        string extended = LongPath.ToExtended(path);

        Assert.StartsWith(@"\\?\UNC\", extended, StringComparison.Ordinal);
        Assert.DoesNotContain(@"\\?\\\", extended, StringComparison.Ordinal);
    }

    [Fact]
    public void AlreadyExtendedPathIsNotPrefixedTwice()
    {
        string path = @"\\?\C:\" + new string('c', 300);

        Assert.Equal(path, LongPath.ToExtended(path));
    }
    [Fact]
    public void DeviceNamespacePathIsLeftAlone()
    {
        // Prefixing \\.\ would retarget the device instead of naming it.
        string path = @"\\.\PhysicalDrive0";

        Assert.Equal(path, LongPath.ToExtended(path));
    }

    [Fact]
    public void RelativePathIsLeftAlone()
    {
        // A relative path has no meaning to the object manager, so an extended prefix cannot help.
        Assert.Equal(@"sub\desktop.ini", LongPath.ToExtended(@"sub\desktop.ini"));
    }

    [Fact]
    public void EmptyOrNullInputIsPassedThrough()
    {
        Assert.Equal(string.Empty, LongPath.ToExtended(string.Empty));
        Assert.Null(LongPath.ToExtended(null!));
    }
}
