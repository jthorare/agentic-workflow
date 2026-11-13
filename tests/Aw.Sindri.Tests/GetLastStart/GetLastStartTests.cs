using Aw.Sindri.Core.Control;
using Aw.Sindri.Features.GetLastStart;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Tests.StartSession;

namespace Aw.Sindri.Tests.GetLastStart;

/// <summary>GET /sindri/sessions/last-start (agents spec §15.4, A7): the repo and agent of the last 2xx start; both null, never an error, when the file is absent or unreadable.</summary>
public sealed class GetLastStartTests : IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("aw-laststart-").FullName;

    public void Dispose() => Directory.Delete(_home, recursive: true);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private StartChoicesFile File_() => new(new FixedHome(_home));

    private static async Task<LastStartView> Ask(Core.Launch.IStartChoices choices) =>
        (await new GetLastStartService(choices).Handle(new GetLastStartRequest(), Token)).Value;

    [Fact, Trait("Lane", "unit")]
    public async Task Present_answers_what_the_last_start_remembered()
    {
        var choices = File_();
        await choices.Remember("demo", "codex", Token);

        Assert.Equal(new LastStartView("demo", "codex"), await Ask(choices));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Absent_answers_both_null()
        => Assert.Equal(new LastStartView(Repo: null, Agent: null), await Ask(File_()));

    [Theory, Trait("Lane", "unit")]
    [InlineData("{not json")]
    [InlineData("[1,2]")]
    [InlineData("")]
    [InlineData("""{"repo":3,"agent":null}""")]
    public async Task A_corrupt_file_answers_both_null_and_no_error(string text)
    {
        await File.WriteAllTextAsync(Path.Combine(_home, StartChoicesFile.FileName), text, Token);

        var result = await new GetLastStartService(File_()).Handle(new GetLastStartRequest(), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(new LastStartView(Repo: null, Agent: null), result.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_fake_and_the_file_agree_on_a_remembered_choice()
    {
        var fake = new FakeStartChoices();
        await fake.Remember("web", "claude-code", Token);

        Assert.Equal(new LastStartView("web", "claude-code"), await Ask(fake));
    }

    private sealed class FixedHome(string path) : ISindriHome
    {
        public string Path => path;
    }
}
