using System.Text.Json.Nodes;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Aw.Judge.Features.EvaluateQuestion;
using Aw.Judge.Host;
using Aw.Judge.Testing;
using Aw.Judge.Tests.Images;
using Aw.Judge.Tests.Providers;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.EvaluateQuestion;

/// <summary>'judge eval' on an agent CLI: a stored row's images reach the CLI in the per-call folder a live call gives it, and the folder is gone after the case.</summary>
[Lane(TestLane.Unit)]
public sealed class EvalImageScopeTests
{
    [Fact]
    public async Task Eval_of_a_stored_image_row_on_a_cli_gives_it_the_images_in_its_per_call_folder()
    {
        var bytes = ImageBytes.Png(1512, 982, "kept");
        var reader = new FakeImageReader(new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["/shots/kept.png"] = bytes });
        var world = new EvalWorld(reader: reader);
        var cli = new FolderReadingCli();
        var service = new EvaluateQuestionService(
            world.Store, new InMemoryJudgeConfigStore(), new FakeProviderCatalog(cli), world.Files,
            new EvalRedaction(new FakeInputRedactor(), AwJson.CreateOptions(JudgeJsonContext.Default)), world.Clock, new EvalImageSource(reader, world.Workspaces));
        _ = world.Store.Label(EvalWorld.Question, "one", Stored("/shots/kept.png", JevImages.Sha256(bytes)), "ask");

        var result = await service.Handle(A.EvaluateQuestionRequest().Provider(ProviderName.ClaudeCli).Build(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : "");
        Assert.Equal("opened", cli.Outcome);
        Assert.Equal(bytes, cli.Read);
        Assert.True(Assert.Single(world.Workspaces.Created).Disposed);
    }

    private static string Stored(string path, string sha256) =>
        new JsonObject
        {
            ["transcriptTail"] = "run the tests",
            ["images"] = new JsonArray(new JsonObject { ["path"] = path, ["sha256"] = sha256, ["bytes"] = 100, ["width"] = 1512, ["height"] = 982 }),
        }.ToJsonString();

    // Plays claude-cli the way AgentCliProvider does: an image call opens the question's scope, and no scope is the image-workspace failure.
    private sealed class FolderReadingCli : IModelProvider
    {
        public string Outcome { get; private set; } = "";

        public byte[]? Read { get; private set; }

        public ProviderName Name => ProviderName.ClaudeCli;

        public IReadOnlySet<ContentClass> Classes { get; } = new HashSet<ContentClass>(ContentClasses.All);

        public ValueTask<ProviderResult> Decide(QuestionRef question, JsonNode? input, TimeSpan budget, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(question);
            if (question.ImageScope?.Open() is not { } folder)
            {
                Outcome = "no-scope";
                return ValueTask.FromResult<ProviderResult>(new ProviderErrored("image-workspace"));
            }

            Outcome = "opened";
            Read = ((FakeImageWorkspace)folder).Read(folder.Files[0]);
            return ValueTask.FromResult<ProviderResult>(new ProviderDecided("ask", 0.9, "model"));
        }
    }
}
