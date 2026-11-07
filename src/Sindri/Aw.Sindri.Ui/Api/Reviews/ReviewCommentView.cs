namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>One comment of a thread.</summary>
internal sealed record ReviewCommentView(string Author, string Body, DateTimeOffset At);
