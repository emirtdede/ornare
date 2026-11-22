using Ornare.Core;
using Xunit;

namespace Ornare.Core.Tests;

/// <summary>
/// A control raises the same notification for a user pick and for a state change made by
/// code. The settings surface depends on being able to tell them apart: treating a
/// programmatic re-selection as a user decision persisted a settings snapshot captured
/// before the refresh, which silently reverted a language that had just been applied.
/// </summary>
public sealed class ReentrancyScopeTests
{
    [Fact]
    public void ScopeIsInactiveOutsideTheGuardedRegion()
    {
        var scope = new ReentrancyScope();

        Assert.False(scope.IsActive);
    }

    [Fact]
    public void GuardedRegionIsObservableFromInsideAndRestoredAfterwards()
    {
        var scope = new ReentrancyScope();
        bool observedInside = false;

        scope.Run(() => observedInside = scope.IsActive);

        Assert.True(observedInside);
        Assert.False(scope.IsActive);
    }

    [Fact]
    public void NestedRegionsKeepSuppressionUntilTheOutermostCompletes()
    {
        var scope = new ReentrancyScope();
        bool activeAfterInner = false;

        scope.Run(() =>
        {
            scope.Run(() => { });
            activeAfterInner = scope.IsActive;
        });

        Assert.True(activeAfterInner);
        Assert.False(scope.IsActive);
    }

    [Fact]
    public void ThrowingGuardedRegionDoesNotLeaveSuppressionStuckOn()
    {
        var scope = new ReentrancyScope();

        Assert.Throws<InvalidOperationException>(() =>
            scope.Run(() => throw new InvalidOperationException("handler failed")));

        Assert.False(scope.IsActive);
    }

    [Fact]
    public void GuardedComputationStillReturnsItsValue()
    {
        var scope = new ReentrancyScope();

        int result = scope.Run(() => 42);

        Assert.Equal(42, result);
        Assert.False(scope.IsActive);
    }

    [Fact]
    public void IndependentScopesDoNotAffectEachOther()
    {
        var pickers = new ReentrancyScope();
        var unrelated = new ReentrancyScope();

        pickers.Run(() => Assert.False(unrelated.IsActive));

        Assert.False(pickers.IsActive);
        Assert.False(unrelated.IsActive);
    }
}
