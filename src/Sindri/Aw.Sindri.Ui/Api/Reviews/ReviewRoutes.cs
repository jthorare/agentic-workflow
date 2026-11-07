namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>The paths of the review calls (review page spec §6.1, §6.2).</summary>
internal static class ReviewRoutes
{
    internal const string List = "/sindri/reviews";

    /// <summary>POST: save review.login.</summary>
    internal const string Login = "/sindri/reviews/login";

    /// <summary>GET: the design library's state and errors.</summary>
    internal const string Library = "/sindri/library";
}
