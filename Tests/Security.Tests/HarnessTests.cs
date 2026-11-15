namespace Security.Tests;

public class HarnessTests
{
    [Fact]
    public void Harness_resolves_repository_root()
    {
        Assert.True(File.Exists(Path.Combine(Check.RepositoryRoot, "IW4MAdmin.slnx")));
    }
}
