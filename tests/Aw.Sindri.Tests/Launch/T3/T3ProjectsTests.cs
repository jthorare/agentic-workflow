using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launch.T3;

/// <summary>A repo resolves to the T3 project it belongs to: its folder first, then its main checkout's, then the same repository elsewhere.</summary>
[Lane(TestLane.Integration)]
public sealed class T3ProjectsTests
{
    private static readonly T3Project _work = new("work", "/srv/work", "github.com/me/work");
    private static readonly T3Project _nested = new("nested", "/srv/work/packages/app", RepositoryKey: null);
    private static readonly T3Project _other = new("other", "/srv/other", "github.com/me/other");

    private static T3RepoIdentity Plain(string path) => new(path, RepositoryKey: null);

    [Fact]
    public void A_folder_inside_a_project_belongs_to_it_and_the_deepest_project_wins()
    {
        Expect.Equal(_work, T3Projects.Resolve([_work, _nested], "/srv/work/docs", Plain("/srv/work/docs")));
        Expect.Equal(_nested, T3Projects.Resolve([_work, _nested], "/srv/work/packages/app/src", Plain("/srv/work/packages/app/src")));
    }

    [Fact]
    public void A_linked_worktree_belongs_to_its_main_checkouts_project()
    {
        var worktree = "/home/me/.t3/worktrees/work/feature";

        Expect.Equal(_work, T3Projects.Resolve([_other, _work], worktree, new T3RepoIdentity("/srv/work", RepositoryKey: null)));
    }

    [Fact]
    public void Another_clone_belongs_to_the_project_of_the_same_repository()
    {
        var clone = "/elsewhere/work-clone";

        Expect.Equal(_work, T3Projects.Resolve([_other, _work], clone, new T3RepoIdentity(clone, "github.com/me/work")));
    }

    [Fact]
    public void The_folder_beats_the_repository()
    {
        var sameKeyElsewhere = new T3Project("clone", "/elsewhere/work-clone", "github.com/me/work");

        Expect.Equal(sameKeyElsewhere, T3Projects.Resolve([_work, sameKeyElsewhere], "/elsewhere/work-clone", new T3RepoIdentity("/elsewhere/work-clone", "github.com/me/work")));
    }

    [Fact]
    public void A_repo_no_project_knows_belongs_to_none() =>
        Expect.True(T3Projects.Resolve([_work, _other], "/tmp/scratch", new T3RepoIdentity("/tmp/scratch", "github.com/someone/else")) is null);
}
