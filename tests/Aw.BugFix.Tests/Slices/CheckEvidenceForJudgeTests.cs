using Aw.BugFix.Core.Errors;
using Aw.BugFix.Features.AdvanceReproduce;
using Aw.BugFix.Features.JudgeCandidate;
using Aw.BugFix.Features.RecordRun;
using Aw.BugFix.Features.RunTest;

namespace Aw.BugFix.Tests.Slices;

/// <summary>A dotnet test check (#1248): the refusal names the fix, and the judge reads the test class the project's filter names, not the project file.</summary>
public sealed class CheckEvidenceForJudgeTests : IDisposable
{
    private const string Project = "tests/Foo.Tests/Foo.Tests.csproj";
    private const string TestClass = "tests/Foo.Tests/Bug/BugTests.cs";

    private readonly World _w = new();

    public void Dispose() => _w.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task A_dotnet_test_command_that_misses_the_check_file_is_told_to_pass_the_csproj_as_project_and_check()
    {
        CommitTestProject();
        await _w.Investigated();

        var result = await _w.Run<RunTestRequest, RunTestResponse>(new RunTestRequest(World.State, TestClass, World.Repo, ["dotnet", "test", "--project", Project]));

        var message = World.Refused(result, BugFixErrors.CommandRefused).Message;
        Assert.Contains("--project " + Project, message, StringComparison.Ordinal);
        Assert.Contains("--check " + Project, message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_judge_input_for_a_csproj_check_carries_the_test_class_its_filter_class_names()
    {
        CommitTestProject();
        await _w.Investigated();
        string[] command = ["dotnet", "test", "--project", Project, "--filter-class", "Foo.Tests.Bug.BugTests"];
        _ = _w.Runner.Exits("dotnet", 1);
        var baseline = World.Ok(await _w.Run<RunTestRequest, RunTestResponse>(new RunTestRequest(World.State, Project, World.Repo, command)));
        _ = World.Ok(await _w.Run<AdvanceReproduceRequest, AdvanceReproduceResponse>(new AdvanceReproduceRequest(World.State, baseline.Evidence, Project, World.Repo)));
        _ = World.Ok(await _w.Start());
        _ = _w.Fix();
        var c1 = World.Ok(await _w.Record()).Candidate;
        _ = _w.Runner.Exits("dotnet", 0);
        var after = World.Ok(await _w.Run<RunTestRequest, RunTestResponse>(new RunTestRequest(World.State, Project, World.Worktree, command)));
        _ = World.Ok(await _w.Run<RecordRunRequest, RecordRunResponse>(new RecordRunRequest(World.State, c1, after.Evidence)));

        _ = World.Ok(await _w.Run<JudgeCandidateRequest, JudgeCandidateResponse>(new JudgeCandidateRequest(World.State, c1)));

        var asked = Assert.Single(_w.Judge.Asked);
        Assert.Contains("Keeps_the_cookie", asked.CheckSource, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_wildcard_filter_class_finds_a_test_class_in_a_subfolder()
    {
        CommitDeepClass();

        var asked = await JudgedWith(["dotnet", "test", "--project", Project, "--filter-class", "*.BugTests"]);

        Assert.Contains("Keeps_the_cookie", asked.CheckSource, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_filter_method_finds_the_test_class_in_a_subfolder()
    {
        CommitDeepClass();

        var asked = await JudgedWith(["dotnet", "test", "--project", Project, "--filter-method", "Foo.Tests.X.BugTests.Keeps_the_cookie"]);

        Assert.Contains("Keeps_the_cookie", asked.CheckSource, StringComparison.Ordinal);
    }

    private async Task<Core.Model.ResolutionCheckInput> JudgedWith(string[] command)
    {
        await _w.Investigated();
        _ = _w.Runner.Exits("dotnet", 1);
        var baseline = World.Ok(await _w.Run<RunTestRequest, RunTestResponse>(new RunTestRequest(World.State, Project, World.Repo, command)));
        _ = World.Ok(await _w.Run<AdvanceReproduceRequest, AdvanceReproduceResponse>(new AdvanceReproduceRequest(World.State, baseline.Evidence, Project, World.Repo)));
        _ = World.Ok(await _w.Start());
        _ = _w.Fix();
        var c1 = World.Ok(await _w.Record()).Candidate;
        _ = _w.Runner.Exits("dotnet", 0);
        var after = World.Ok(await _w.Run<RunTestRequest, RunTestResponse>(new RunTestRequest(World.State, Project, World.Worktree, command)));
        _ = World.Ok(await _w.Run<RecordRunRequest, RecordRunResponse>(new RecordRunRequest(World.State, c1, after.Evidence)));
        _ = World.Ok(await _w.Run<JudgeCandidateRequest, JudgeCandidateResponse>(new JudgeCandidateRequest(World.State, c1)));
        return Assert.Single(_w.Judge.Asked);
    }

    private void CommitDeepClass()
    {
        _w.Repos.Write($"{World.Repo}/{Project}", "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
        _w.Repos.Write($"{World.Repo}/tests/Foo.Tests/Slices/Deep/BugTests.cs", "namespace Foo.Tests.X;\n\npublic class BugTests\n{\n    [Fact]\n    public void Keeps_the_cookie() { }\n}\n");
        _ = _w.Repos.Commit(World.Repo);
    }

    private void CommitTestProject()
    {
        _w.Repos.Write($"{World.Repo}/{Project}", "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
        _w.Repos.Write($"{World.Repo}/{TestClass}", "namespace Foo.Tests.Bug;\n\npublic class BugTests\n{\n    [Fact]\n    public void Keeps_the_cookie() { }\n}\n");
        _ = _w.Repos.Commit(World.Repo);
    }
}
