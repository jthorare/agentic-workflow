using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Judge.Core.Decisions;
using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Providers;
using Aw.Judge.Core.Questions;
using Aw.Judge.Testing;
using Aw.Judge.Tests.Images;
using Aw.Judge.Tests.Providers;
using Aw.Kernel.Testing;
using static Aw.Judge.Tests.Questions.QuestionRun;

namespace Aw.Judge.Tests.Questions;

/// <summary>
/// Any question takes a top-level 'images' (absolute paths) and 'imagesBestEffort' in its input, both read and stripped by the evaluator before
/// the question's own record deserializes (spec 2026-11-02-judge-images-design.md, 2, 4 and 6). Asked as ask-check over in-memory fakes:
/// the images are in memory, Jev is scripted or sits behind a recording handler.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ImagesOnAnyQuestionTests
{
    private const string Tail = "run the tests";

    private const int MiB = 1024 * 1024;

    [Fact]
    public async Task AskCheck_with_an_image_routes_to_jev_and_stores_its_hash_not_its_bytes()
    {
        var bytes = ImageBytes.Png(1512, 982, "home");
        var run = new QuestionRun { ImageReader = Reader(("/shots/home.png", bytes)) };
        var jev = Jev().Decides("continue", 0.9);
        var cli = Cli(ContentClass.Transcript, ContentClass.Image).Decides("continue", 0.9);

        var outcome = await run.Ask(AskCheckQuestion.Definition, Input(["/shots/home.png"]), jev, cli);

        var decided = Assert.IsType<DecidedOutcome>(outcome);
        Assert.Equal(("continue", ProviderName.Jev), (decided.Decision, decided.Model));
        Assert.Empty(cli.Calls);
        var sent = Assert.Single(Assert.Single(jev.Calls).Question.Images);
        Assert.Equal(("/shots/home.png", JevImages.Sha256(bytes), 1512, 982), (sent.Path, sent.Sha256, sent.Width, sent.Height));
        var stored = Stored(run);
        Assert.Equal(Tail, stored["transcriptTail"]?.GetValue<string>());
        var image = Assert.Single(stored["images"]!.AsArray())!.AsObject();
        Assert.Equal(["bytes", "height", "path", "sha256", "width"], image.Select(pair => pair.Key).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal("/shots/home.png", image["path"]?.GetValue<string>());
        Assert.Equal(JevImages.Sha256(bytes), image["sha256"]?.GetValue<string>());
        Assert.Equal(bytes.Length, image["bytes"]?.GetValue<int>());
        Assert.Equal(1512, image["width"]?.GetValue<int>());
        Assert.Equal(982, image["height"]?.GetValue<int>());
        Assert.DoesNotContain(Convert.ToBase64String(bytes), stored.ToJsonString(), StringComparison.Ordinal);
        Assert.Null(stored["imagesBestEffort"]);
    }

    // The row a call with no images writes is today's, byte for byte (spec 6.2). The expected values were captured from main's code
    // before this change, so this test is green on the red commit and has to stay green.
    [Theory]
    [InlineData("""{"transcriptTail":"run the tests"}""")]
    [InlineData("""{"transcriptTail":"run the tests","images":[]}""")]
    [InlineData("""{"transcriptTail":"run the tests","images":[],"imagesBestEffort":true}""")]
    public async Task Input_without_images_stores_todays_row(string input)
    {
        var run = new QuestionRun();
        var jev = Jev().Decides("continue", 0.9);

        var outcome = await run.Ask(AskCheckQuestion.Definition, input, jev);

        Assert.Equal("continue", Assert.IsType<DecidedOutcome>(outcome).Decision);
        Assert.Empty(Assert.Single(jev.Calls).Question.Images);
        var row = Assert.Single(run.Log.Decisions);
        Assert.Equal("""{"transcriptTail":"run the tests"}""", row.Details?.InputJson);
        Assert.Equal(InputJson.Digest(JsonNode.Parse("""{"transcriptTail":"run the tests"}"""), Json), row.InputDigest);
        Assert.Equal(("ask-check", "transcript", "jev", "model", DecisionOutcome.Decided), (row.Question, row.ContentClass, row.Provider, row.ReasonCode, row.Outcome));
        Assert.Equal("continue", row.Decision);
        Assert.Empty(row.Skipped);
        Assert.Empty(outcome.StderrLines);
    }

    [Fact]
    public async Task Best_effort_drops_a_bad_image_and_answers_as_text()
    {
        var run = new QuestionRun();
        var jev = Jev().Decides("continue", 0.9);

        var outcome = await run.Ask(AskCheckQuestion.Definition, Input(["/shots/gone.png"], bestEffort: true), jev);

        var decided = Assert.IsType<DecidedOutcome>(outcome);
        Assert.Equal(("continue", ProviderName.Jev), (decided.Decision, decided.Model));
        Assert.Empty(Assert.Single(jev.Calls).Question.Images);
        Assert.Equal(["judge: images[0] /shots/gone.png does not exist"], outcome.StderrLines);
        var trace = Assert.Single(JevSkipNotes.For(Assert.Single(run.Log.Decisions)));
        Assert.StartsWith("jev images dropped: ", trace, StringComparison.Ordinal);
        Assert.Contains("images[0] /shots/gone.png does not exist", trace, StringComparison.Ordinal);
        Assert.Empty(run.Log.Failures);
    }

    // More than 4 paths: the judge never picks which 4, so a best-effort call drops them all and asks as text.
    [Fact]
    public async Task Best_effort_with_more_than_four_paths_drops_all_and_asks_as_text()
    {
        var (paths, files) = Case("more-than-four");
        var run = new QuestionRun { ImageReader = new FakeImageReader(files) };
        var jev = Jev().Decides("continue", 0.9);

        var outcome = await run.Ask(AskCheckQuestion.Definition, Input(paths, bestEffort: true), jev);

        Assert.Equal(("continue", ProviderName.Jev), (Assert.IsType<DecidedOutcome>(outcome).Decision, ProviderName.Jev));
        Assert.Empty(Assert.Single(jev.Calls).Question.Images);
        Assert.Equal(["judge: images has 5 paths; at most 4 are allowed; asked as text"], outcome.StderrLines);
        var dropped = Assert.IsType<JsonArray>(Stored(run)["imagesDropped"]);
        Assert.Equal("images has 5 paths; at most 4 are allowed; asked as text", Assert.Single(dropped)!.GetValue<string>());
        Assert.Null(Stored(run)["images"]);
    }

    // A strict caller (Sindri's step, a person at the CLI) gets no partial answer: nothing is asked, the reason is one code, the cause is on stderr
    // and the row keeps the paths as given.
    [Theory]
    [InlineData("more-than-four", "judge: images has 5 paths; at most 4 are allowed")]
    [InlineData("relative-path", "judge: images[0] shots/a.png is not an absolute path")]
    [InlineData("missing-file", "judge: images[0] /shots/gone.png does not exist")]
    [InlineData("directory", "judge: images[0] /shots/dir is a directory")]
    [InlineData("not-png-or-jpeg", "judge: images[2] /shots/a.webp is not a PNG or JPEG")]
    [InlineData("over-24-mib", "judge: images total 26 MiB; at most 24 MiB")]
    public async Task Strict_bad_image_escalates_invalid_images(string cause, string stderr)
    {
        var (paths, files) = Case(cause);
        var run = new QuestionRun { ImageReader = new FakeImageReader(files) };
        var jev = Jev().Decides("continue", 0.9);
        var cli = Cli(ContentClass.Transcript, ContentClass.Image).Decides("continue", 0.9);

        var outcome = await run.Ask(AskCheckQuestion.Definition, Input(paths), jev, cli);

        Assert.Equal("invalid-images", Assert.IsType<EscalatedOutcome>(outcome).ReasonCode);
        Assert.Equal([stderr], outcome.StderrLines);
        Assert.Empty(jev.Calls);
        Assert.Empty(cli.Calls);
        var row = Assert.Single(run.Log.Decisions);
        Assert.Equal(("none", "invalid-images", DecisionOutcome.Escalated), (row.Provider, row.ReasonCode, row.Outcome));
        Assert.Equal(paths, JsonNode.Parse(row.Details!.InputJson)!["images"]!.AsArray().Select(path => path!.GetValue<string>()), StringComparer.Ordinal);
    }

    // A hook cannot check what the judge checks, so an image Jev cannot take is dropped and Jev still answers, as text (spec 2).
    [Fact]
    public async Task Best_effort_image_under_jev_minimum_is_dropped_and_jev_answers_as_text()
    {
        var handler = RecordingJevHandler.Choosing("continue");
        using var jev = JevImages.Jev(handler);
        var cli = Cli(ContentClass.Transcript, ContentClass.Image).Decides("continue", 0.9);
        var run = new QuestionRun { ImageReader = Reader(("/shots/icon.png", ImageBytes.Png(200, 120, "icon"))) };

        var outcome = await run.Ask(AskCheckQuestion.Definition, Input(["/shots/icon.png"], bestEffort: true), jev, cli);

        var decided = Assert.IsType<DecidedOutcome>(outcome);
        Assert.Equal(("continue", ProviderName.Jev), (decided.Decision, decided.Model));
        Assert.Empty(cli.Calls);
        var request = Assert.Single(handler.Requests);
        Assert.Null(request.ImagesHeader);
        Assert.Empty(request.Parts);
        Assert.Null(request.Json["assets"]);
        Assert.Equal(
            ["jev images dropped: image 1 is 200x120, under Jev's 280 px minimum; asked jev as text"],
            JevSkipNotes.For(Assert.Single(run.Log.Decisions)));
    }

    // An image no downscale can fit (4000x500) goes alone; the other one stays on Jev with its picture.
    [Fact]
    public async Task Best_effort_image_that_cannot_fit_is_dropped_and_the_rest_stay_on_jev()
    {
        var handler = RecordingJevHandler.Choosing("continue");
        using var jev = JevImages.Jev(handler, new FakeImageScaler());
        var cli = Cli(ContentClass.Transcript, ContentClass.Image).Decides("continue", 0.9);
        var run = new QuestionRun
        {
            ImageReader = Reader(("/shots/wide.png", ImageBytes.Png(4000, 500, "wide")), ("/shots/home.png", ImageBytes.Png(1512, 982, "home"))),
        };

        var outcome = await run.Ask(AskCheckQuestion.Definition, Input(["/shots/wide.png", "/shots/home.png"], bestEffort: true), jev, cli);

        Assert.Equal(("continue", ProviderName.Jev), (Assert.IsType<DecidedOutcome>(outcome).Decision, ProviderName.Jev));
        Assert.Empty(cli.Calls);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("true", request.ImagesHeader);
        _ = Assert.Single(request.Parts, part => part.Name != "request");
        Assert.Equal(
            ["jev images dropped: image 1 is 4000x500; fitting it under 1568 px leaves it under 280 px; asked jev with images 2"],
            JevSkipNotes.For(Assert.Single(run.Log.Decisions)));
    }

    // Spec 4's own example: image 2 is shrunk, image 3 is dropped, in one call. The sizes sent line up with the images read.
    [Fact]
    public async Task Best_effort_scaled_and_dropped_images_each_get_their_own_why_line()
    {
        var handler = RecordingJevHandler.Choosing("continue");
        using var jev = JevImages.Jev(handler, new FakeImageScaler());
        var run = new QuestionRun
        {
            ImageReader = Reader(
                ("/shots/home.png", ImageBytes.Png(1512, 982, "home")),
                ("/shots/home@2x.png", ImageBytes.Png(3024, 1964, "retina")),
                ("/shots/icon.png", ImageBytes.Png(200, 120, "icon"))),
        };

        _ = await run.Ask(AskCheckQuestion.Definition, Input(["/shots/home.png", "/shots/home@2x.png", "/shots/icon.png"], bestEffort: true), jev);

        var row = Assert.Single(run.Log.Decisions);
        var lines = ImageNotes.For(row);
        Assert.Equal(4, lines.Count);
        Assert.DoesNotContain("sent to jev", lines[1], StringComparison.Ordinal);
        Assert.EndsWith("-> sent to jev as 1568x1018", lines[2], StringComparison.Ordinal);
        Assert.DoesNotContain("sent to jev", lines[3], StringComparison.Ordinal);
        Assert.Equal(
            ["jev images dropped: image 3 is 200x120, under Jev's 280 px minimum; asked jev with images 1, 2"],
            JevSkipNotes.For(row));
    }

    // Jev reports a size limit only after the upload: a hook call tries Jev once more without the images, and stays on Jev.
    [Fact]
    public async Task Best_effort_413_retries_jev_once_as_text()
    {
        var handler = new TooLargeWithImagesHandler();
        using var jev = JevImages.Jev(handler);
        var cli = Cli(ContentClass.Transcript, ContentClass.Image).Decides("continue", 0.9);
        var run = new QuestionRun { ImageReader = Reader(("/shots/home.png", ImageBytes.Png(1512, 982, "home"))) };

        var outcome = await run.Ask(AskCheckQuestion.Definition, Input(["/shots/home.png"], bestEffort: true), jev, cli);

        var decided = Assert.IsType<DecidedOutcome>(outcome);
        Assert.Equal(("continue", ProviderName.Jev), (decided.Decision, decided.Model));
        Assert.Empty(cli.Calls);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("true", handler.Requests[0].ImagesHeader);
        Assert.Equal("multipart/form-data", handler.Requests[0].ContentType);
        Assert.Null(handler.Requests[1].ImagesHeader);
        Assert.Empty(handler.Requests[1].Parts);
        Assert.Equal(
            ["jev images dropped: Jev said the images were too large (413); asked jev again as text"],
            JevSkipNotes.For(Assert.Single(run.Log.Decisions)));
    }

    // Jev dropped the images (413, then a text retry) and answered, but under the threshold: the chain goes on, and the next provider is asked as text too (spec 2).
    // Not ask-check: it has fallback rules that settle every below-threshold answer, so its CLI is never asked after Jev.
    [Fact]
    public async Task Best_effort_drop_then_a_jev_answer_below_threshold_asks_the_next_provider_as_text()
    {
        var handler = new TooLargeWithImagesHandler("0.1", "progressing");
        using var jev = JevImages.Jev(handler);
        var cli = Cli(ContentClass.Diff).Decides("progressing", 0.9);
        var run = new QuestionRun { ImageReader = Reader(("/shots/home.png", ImageBytes.Png(1512, 982, "home"))) };
        var input = new JsonObject { ["problem"] = "the page is blank", ["turnDiff"] = "+ render()", ["images"] = new JsonArray("/shots/home.png"), ["imagesBestEffort"] = true };

        var outcome = await run.Ask(TurnProgressQuestion.Definition, input.ToJsonString(), jev, cli);

        var decided = Assert.IsType<DecidedOutcome>(outcome);
        Assert.Equal(("progressing", ProviderName.ClaudeCli), (decided.Decision, decided.Model));
        Assert.Equal(2, handler.Requests.Count);
        Assert.Empty(Assert.Single(cli.Calls).Question.Images);
    }

    // An ask-check input with the given extra fields; the transcript tail is one the deny list does not settle.
    private static string Input(string[]? images = null, bool bestEffort = false)
    {
        var input = new JsonObject { ["transcriptTail"] = Tail };
        if (images is not null)
        {
            input["images"] = new JsonArray([.. images.Select(path => (JsonNode)JsonValue.Create(path))]);
        }

        if (bestEffort)
        {
            input["imagesBestEffort"] = true;
        }

        return input.ToJsonString();
    }

    // Jev as the real adapter covers every class, so only the image routing and the images themselves tell an image call apart.
    private static ScriptedProvider Jev() => new(ProviderName.Jev, [.. ContentClasses.All]);

    private static FakeImageReader Reader(params (string Path, byte[] Bytes)[] files) =>
        new(files.ToDictionary(file => file.Path, file => file.Bytes, StringComparer.Ordinal));

    private static JsonObject Stored(QuestionRun run) =>
        JsonNode.Parse(Assert.Single(run.Log.Decisions).Details!.InputJson)!.AsObject();

    private static (string[] Paths, Dictionary<string, byte[]> Files) Case(string cause)
    {
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        switch (cause)
        {
            case "more-than-four":
                string[] five = ["/shots/1.png", "/shots/2.png", "/shots/3.png", "/shots/4.png", "/shots/5.png"];
                foreach (var path in five)
                {
                    files[path] = ImageBytes.Png(1512, 982, path);
                }

                return (five, files);
            case "relative-path":
                files["/shots/a.png"] = ImageBytes.Png(1512, 982, "a");
                return (["shots/a.png"], files);
            case "missing-file":
                return (["/shots/gone.png"], files);
            case "directory":
                files["/shots/dir/inside.png"] = ImageBytes.Png(1512, 982, "inside");
                return (["/shots/dir"], files);
            case "not-png-or-jpeg":
                files["/shots/a.png"] = ImageBytes.Png(1512, 982, "a");
                files["/shots/b.png"] = ImageBytes.Png(1512, 982, "b");
                files["/shots/a.webp"] = ImageBytes.Gif();
                return (["/shots/a.png", "/shots/b.png", "/shots/a.webp"], files);
            case "over-24-mib":
                files["/shots/big-1.png"] = ImageBytes.Png(1512, 982, new string('x', 13 * MiB));
                files["/shots/big-2.png"] = ImageBytes.Png(1512, 982, new string('y', 13 * MiB));
                return (["/shots/big-1.png", "/shots/big-2.png"], files);
            default:
                throw new ArgumentOutOfRangeException(nameof(cause), cause, "not a case");
        }
    }

    // Jev as a call with images finds it: a 413 for any request that holds images, an answer for one that does not.
    private sealed class TooLargeWithImagesHandler(string confidence = "0.9", string choice = "continue") : HttpMessageHandler
    {
        public List<RecordedJevRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var recorded = await RecordedJevRequest.Capture(request, cancellationToken);
            Requests.Add(recorded);
            return recorded.ImagesHeader is null
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JevImages.Choice(choice, confidence), Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.RequestEntityTooLarge) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        }
    }
}
