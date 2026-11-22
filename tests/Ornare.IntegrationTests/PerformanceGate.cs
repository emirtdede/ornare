using Xunit;

namespace Ornare.IntegrationTests;

/// <summary>
/// Timing gates share one xUnit collection so they never run beside another collection in this
/// assembly. Two end-to-end benchmarks measuring the same disk and the same image pipeline at
/// once would each report the other's work.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PerformanceGate
{
    public const string Name = "Performance";
}
