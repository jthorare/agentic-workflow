using System.Net;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Approvals;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Approvals;

/// <summary>
/// The approve-all dialog over the fake API. The plan, job and catalog row are spec-shaped (Golden/SpecShaped), PROVISIONAL until the
/// engine's recipes-approve-all goldens land (setup-once spec §8).
/// </summary>
internal sealed class ApproveAllFixture
{
    internal static readonly Uri Api = new("http://127.0.0.1:7410/");

    internal ApproveAllFixture(string catalog = "extra-approve-all-ready")
    {
        Server = new FakeSindriApi { Catalog = RouteAnswer.SpecShaped("commands", catalog) };
    }

    internal FakeSindriApi Server { get; }

    internal FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    internal static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A dialog over the catalog as served.</summary>
    internal async Task<ApproveAllViewModel> Dialog()
    {
        var catalog = new CatalogViewModel(new FakeClipboard(), Clock);
        catalog.Show(Api, Server, (await Server.Commands(Api, Token)).Value);
        return new ApproveAllViewModel(catalog, () => Api, Server, Clock);
    }

    /// <summary>A dialog opened on the plan.</summary>
    internal async Task<ApproveAllViewModel> Opened()
    {
        var dialog = await Dialog();
        await dialog.Open(Token);
        return dialog;
    }

    /// <summary>Clicks Approve and lets the answer, and the job it starts, finish.</summary>
    internal async Task Approve(ApproveAllViewModel dialog) => await Finish(dialog.Approve());

    /// <summary>Lets a task that waits on the clock finish: the clock moves a beat at a time, bounded, so a job that never ends fails the test instead of hanging it.</summary>
    internal async Task Finish(Task task)
    {
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(Token);
        bound.CancelAfter(TimeSpan.FromSeconds(20));
        while (!task.IsCompleted)
        {
            Clock.Advance(TimeSpan.FromMilliseconds(500));
            await Task.Delay(1, bound.Token);
        }

        await task;
    }

    /// <summary>The refusal golden as the route answers it.</summary>
    internal static RouteAnswer Refusal(string slug, string scenario) => RouteAnswer.Problem(slug, scenario);

    /// <summary>A 503 the views answer while behind: a read the job's polling cannot use.</summary>
    internal static RouteAnswer Unreadable => RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-API-011", "The views are behind.");
}
