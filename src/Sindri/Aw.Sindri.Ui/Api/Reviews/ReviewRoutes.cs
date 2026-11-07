namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>The paths of the review calls (review page spec §6.1, §6.2).</summary>
internal static class ReviewRoutes
{
    internal const string List = "/sindri/reviews";

    /// <summary>POST: save review.login.</summary>
    internal const string Login = "/sindri/reviews/login";

    /// <summary>GET: the design library's state and errors.</summary>
    internal const string Library = "/sindri/library";

    /// <summary>GET: the review page model of one PR (<paramref name="repo"/> is owner/name).</summary>
    internal static string Page(string repo, int number) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"/sindri/reviews/{repo}/{number}");

    /// <summary>POST: send a finding of one PR as a fix task.</summary>
    internal static string Fix(string repo, int number) => Page(repo, number) + "/fix";
}
