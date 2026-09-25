using Ornare.Core;
using Xunit;

namespace Ornare.Core.Tests;

public class OrnareConstantsTests
{
    [Fact]
    public void ApplicationNameMatchesOrnare()
    {
        Assert.Equal("Ornare", OrnareConstants.ApplicationName);
    }

    [Fact]
    public void DesktopIniFileNameMatchesDesktopIni()
    {
        Assert.Equal("desktop.ini", OrnareConstants.DesktopIniFileName);
    }

    [Fact]
    public void CustomIconFileNameMatchesFolderIco()
    {
        Assert.Equal("folder.ico", OrnareConstants.CustomIconFileName);
    }
}
