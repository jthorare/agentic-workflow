using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Features.AddRepo;

namespace Aw.Sindri.Tests.AddRepo;

/// <summary>The AddRepo admission against a real profile file on disk: one it can't read refuses the add and is left as it was.</summary>
[Lane(TestLane.Integration)]
public sealed class AddRepoAdmissionFileTests : IDisposable
{
    private readonly AddRepoRig _rig = new();

    public void Dispose() => _rig.Dispose();

    private static CancellationToken Token => AddRepoRig.Token;

    [Fact]
    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    public async Task A_profile_that_exists_but_cannot_be_read_refuses_the_add_and_is_left_alone()
    {
        Assert.SkipWhen(Environment.IsPrivilegedProcess, "root ignores file permissions");
        using var files = new Profile.ProfileFiles();
        const string Mine = "schemaVersion: 1\nuser: thor\n";
        files.Write(Mine);
        _ = _rig.Admission();   // takes the caller slot the gate uses
        var admission = new AddRepoAdmission(_rig.Inspector, files.Source, _rig.Resolved, _rig.Backfill);
        File.SetUnixFileMode(files.Settings.ProfilePath, UnixFileMode.None);
        try
        {
            var admitted = await admission.Admit(new AddRepoRequest("/work/demo"), Token);

            Assert.Equal(SindriErrors.ProfileUnreadable.Code, admitted.Error.Code);
            Assert.Contains("can't be read", admitted.Error.Message, StringComparison.Ordinal);
            Assert.Contains("fix:", admitted.Error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(_rig.Slice.Store.Events, static e => e.Envelope.Type == EventTypes.RepoAdded);   // the admission itself never writes the profile, so the check is that nothing was admitted
        }
        finally
        {
            File.SetUnixFileMode(files.Settings.ProfilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        Assert.Equal(Mine, await File.ReadAllTextAsync(files.Settings.ProfilePath, Token));
    }
}
