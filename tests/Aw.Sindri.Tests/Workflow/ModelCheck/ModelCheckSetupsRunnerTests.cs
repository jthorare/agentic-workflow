using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.ModelCheck;

namespace Aw.Sindri.Tests.Workflow.ModelCheck;

/// <summary>The runner named by AW_TLC_CHECK, which needs a real file.</summary>
[Lane(TestLane.Integration)]
public sealed class ModelCheckSetupsRunnerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aw-model-setups-" + Path.GetRandomFileName());

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void AW_TLC_CHECK_naming_an_existing_file_is_the_runner_and_tlc_sh_sits_in_its_tree()
    {
        var scripts = Path.Combine(_root, "scripts");
        _ = Directory.CreateDirectory(scripts);
        var runner = Path.Combine(scripts, "tlc-check.sh");
        File.WriteAllText(runner, "#!/bin/sh\n");

        var setup = ModelCheckSetups.Read(name => name switch { "AW_STATE_DIR" => Path.Combine(_root, "state"), "AW_TLC_CHECK" => runner, _ => null });

        Assert.Equal(runner, setup.Runner);
        Assert.Equal(Path.Combine(_root, "skills", "formal-verify", "scripts", "tlc.sh"), setup.TlcSh);
    }
}
