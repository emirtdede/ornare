using Ornare.DesignSystem;
using Xunit;

namespace Ornare.UiTests;

public class UiBootstrapTests
{
    [Fact]
    public void DesignTokensQuickPanelDimensionsAreDefined()
    {
        Assert.True(DesignTokens.QuickPanelWidth > 0);
        Assert.True(DesignTokens.QuickPanelHeight > 0);
    }
}
