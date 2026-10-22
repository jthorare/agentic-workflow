using System.Security.Cryptography;
using System.Text;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.RegisterMcp;

namespace Aw.Sindri.Tests.RegisterMcp;

/// <summary>The cheap check a RegisterMcp start makes inside the gate (setup-once spec §5.4): the repo is named by the approved snapshot, else SND-SETUP-004, before any job exists.</summary>
[Lane(TestLane.Unit)]
public sealed class RegisterMcpAdmissionTests
{
    private static readonly CancellationToken _token = TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_repo_the_approved_profile_names_is_admitted_with_the_hashed_key()
    {
        var admission = Admission(("Demo", "/work/demo"));
        var request = new RegisterMcpJobRequest("Demo");

        var admitted = await admission.Admit(request, _token);

        Assert.True(admitted.IsSuccess);
        var digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("Demo")))[..16];
        Assert.Equal("mcp-" + digest, admission.KeyOf(request).Value);
    }

    [Fact]
    public async Task A_repo_the_approved_profile_does_not_name_is_SND_SETUP_004()
    {
        var admitted = await Admission(("Demo", "/work/demo")).Admit(new RegisterMcpJobRequest("Other"), _token);

        Assert.Equal("SND-SETUP-004", admitted.Error.Code);
    }

    [Fact]
    public async Task No_approved_profile_is_SND_SETUP_004()
    {
        var admitted = await new RegisterMcpAdmission(new Approved(profile: null)).Admit(new RegisterMcpJobRequest("Demo"), _token);

        Assert.Equal("SND-SETUP-004", admitted.Error.Code);
    }

    [Fact]
    public async Task No_repo_named_takes_the_only_repo_of_the_profile()
    {
        var admission = Admission(("Demo", "/work/demo"));
        var request = new RegisterMcpJobRequest(Repo: null);

        _ = await admission.Admit(request, _token);

        Assert.Equal("Demo", admission.Take(request)?.Value);
    }

    private static RegisterMcpAdmission Admission(params (string Name, string Path)[] repos)
    {
        var profile = new SindriProfile(1, "thor", Repos: [.. repos.Select(static r => new RepoEntry(r.Name, r.Path))]);
        return new RegisterMcpAdmission(new Approved(new LoadedProfile(profile, ProfileHash.From(new string('a', 64)), "/home/profile.yaml")));
    }

    private sealed class Approved(LoadedProfile? profile) : IApprovedProfile
    {
        public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken) => ValueTask.FromResult<Result<LoadedProfile?>>(profile);
    }
}
