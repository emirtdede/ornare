using Ornare.Core;
using Xunit;

namespace Ornare.IntegrationTests;

public class IntegrationBootstrapTests
{
    [Fact]
    public void CoreAndWindowsProjectsAreReferencedSuccessfully()
    {
        Assert.NotNull(OrnareConstants.ApplicationName);
    }
}
