using System.Net;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Records;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Projects.Cards;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Forge;
using Aw.Sindri.Tests.ApiGoldens;
using Aw.Sindri.Tests.Callers;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.Reviews;

/// <summary>
/// A stored PR record whose <c language="text">source_record.repo</c> isn't a profile repo name ('acme/api') is a store fault, not a request fault (#2229): the Reviews list read answers a 5xx whose detail names the column and the
/// record's key, never the 400 SND-CLI-003 that <c language="text">ValueObjectValidation</c> keeps for bad route and query ids.
/// </summary>
public sealed class StoredRecordBadRepoTests
{
    private const string Key = "github:pr/acme/api/7";
    private const string Profile = ItemScenarios.ProfileYaml + ReviewScenarios.GithubContext
        + "review:\n  login: joi-example\n  projects:\n    - { project: " + ReviewScenarios.SindriProject + ", repos: [acme/api] }\n";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<GoldenHost> Start() =>
        GoldenHost.Start(temporary: true, Ct, Profile, extra: services =>
        {
            _ = services.Replace(ServiceDescriptor.Singleton<IReviewProjects>(new FakeReviewProjects().With(ReviewScenarios.SindriProject, "Sindri")));
            _ = services.Replace(ServiceDescriptor.Singleton<IProjectPages>(new ReviewScenarios.OneProjectPage()));
            _ = services.Replace(ServiceDescriptor.Singleton<IPullRequests>(new FakePullRequests(["github.com"]) { SelfLogin = "joi-example" }));
        });

    private static async Task Seed(GoldenHost host)
    {
        await ViewScenarios.Approve(host, Ct);
        await ViewScenarios.CatchUp(host, Ct);
        var pull = RecordSamples.Record(Key, source: RecordSamples.Github) with
        {
            Kind = RecordKind.Pr,
            Audience = AudienceKey.From("repo:acme/api"),
            State = RecordState.Open,
        };
        var batch = RecordSamples.Batch(RecordSamples.Github, 1, RecordSamples.Write(pull, "body"));
        _ = (await host.App.Services.GetRequiredService<IRecordStore>().Commit(batch, Ct)).Value;
    }

    private static void SetRepo(GoldenHost host, string? repo)
    {
        using var connection = new SqliteConnection($"Data Source={Path.Combine(host.Home, "context", "context.db")}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE source_record SET repo = @repo WHERE key = @key";
        _ = command.Parameters.AddWithValue("@repo", (object?)repo ?? DBNull.Value);
        _ = command.Parameters.AddWithValue("@key", Key);
        Assert.Equal(expected: 1, command.ExecuteNonQuery());
    }

    private static async Task<Answer> ReadList(GoldenHost host) =>
        await host.Http.Ask(Get("/sindri/reviews", ItemScenarios.Human(host)));

    [Fact, Trait("Lane", "integration")]
    public async Task A_stored_repo_that_is_not_a_profile_repo_name_answers_a_store_fault_naming_the_column_and_the_key()
    {
        await using var host = await Start();
        await Seed(host);
        SetRepo(host, "acme/api");

        var answer = await ReadList(host);

        Assert.True((int)answer.Status >= 500, userMessage: $"expected a 5xx, got {answer.Status}: {answer.Body}");
        Assert.NotEqual("SND-CLI-003", answer.Code, StringComparer.Ordinal);
        Assert.Contains("source_record.repo", answer.Body, StringComparison.Ordinal);
        Assert.Contains(Key, answer.Body, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_same_read_with_no_stored_repo_answers_200()
    {
        await using var host = await Start();
        await Seed(host);
        SetRepo(host, repo: null);

        var answer = await ReadList(host);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
    }
}
