using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Library;
using Aw.Context.Infrastructure.Git;
using Aw.Context.Infrastructure.Linear;
using Aw.Context.Infrastructure.Sources;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Linear;
using Aw.Context.Tests.Map;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Sync;

/// <summary>The library feed in the record feed catalog (PR review page spec 2026-11-04 §5.2): it runs with no repo in scope, beside repo-docs when there are repos, and only when its folders pass the path rules.</summary>
public sealed class LibraryCatalogTests
{
    private static readonly LibraryHost _host = new() { Home = "/home/owner", StateDefault = "/home/owner/.agentic-workflow" };

    private static ContextRecordFeedCatalog Catalog(bool withLibrary = true)
    {
        var clock = new FakeTimeProvider();
        var linear = new LinearSources(new LinearSourceParts(new HttpClient(new FakeLinearProjects([])), static _ => "fixture-key", clock, new CapturingLogger<LinearClient>()));
        return new ContextRecordFeedCatalog(new FakeRecordStore(), new GitCli(), linear, external: null, withLibrary ? new LibraryParts(_host, new FakeLibraryStatus(), clock) : null);
    }

    private static List<string> Sources(ContextRecordFeedCatalog catalog, ContextSettings settings, IReadOnlyList<ContextRepo> repos) =>
        [.. catalog.For(settings, repos, new FakeScrubberFactory().For([]).Value).Select(static f => f.Source.Value)];

    [Fact]
    public void The_library_feed_runs_with_no_repo_in_scope()
    {
        var sources = Sources(Catalog(), new ContextSettings(), []);

        Assert.Equal([LibrarySource.Id.Value], sources);
    }

    [Fact]
    public void With_repos_the_library_feed_follows_repo_docs()
    {
        var sources = Sources(Catalog(), new ContextSettings(), MapWorld.Repos);

        Assert.Equal([RepoDocs.Id.Value, LibrarySource.Id.Value], sources);
    }

    [Fact]
    public void Without_library_parts_no_repo_still_means_no_feed() =>
        Assert.Empty(Sources(Catalog(withLibrary: false), new ContextSettings(), []));

    [Fact]
    public void A_profile_that_turns_the_aw_root_off_and_names_no_vault_has_no_library_feed()
    {
        var settings = new ContextSettings { Library = new LibrarySettings { StateDir = LibrarySettings.Off } };

        Assert.Empty(Sources(Catalog(), settings, []));
    }

    [Fact]
    public void A_root_the_path_rules_refuse_is_left_out_not_read()
    {
        var home = new LibrarySettings { StateDir = "/home/owner" };   // the home folder itself

        Assert.Empty(Sources(Catalog(), new ContextSettings { Library = home }, []));
    }
}
