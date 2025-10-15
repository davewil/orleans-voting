using Microsoft.Extensions.Configuration;
using Orleans.TestingHost;

namespace OrleansVoting.Tests.Fixtures;

public class TestClusterFixture : IDisposable
{
    public TestCluster Cluster { get; private set; }

    public TestClusterFixture()
    {
        var builder = new TestClusterBuilder();

        builder.AddSiloBuilderConfigurator<TestSiloConfigurator>();

        Cluster = builder.Build();
        Cluster.Deploy();
    }

    public void Dispose()
    {
        Cluster?.StopAllSilos();
        Cluster?.Dispose();
    }

    private class TestSiloConfigurator : ISiloConfigurator
    {
        public void Configure(ISiloBuilder siloBuilder)
        {
            siloBuilder.AddMemoryGrainStorage("votes");
        }
    }
}
