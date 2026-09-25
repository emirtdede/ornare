using Ornare.Core;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Ornare.IntegrationTests;

public class IntegrationBootstrapTests
{
    [Fact]
    public void CoreAndWindowsProjectsAreReferencedSuccessfully()
    {
        Assert.NotNull(OrnareConstants.ApplicationName);
    }
}

