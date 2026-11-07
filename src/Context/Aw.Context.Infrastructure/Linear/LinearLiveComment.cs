namespace Aw.Context.Infrastructure.Linear;

/// <summary>A comment as the live read selects it: its text and the issue it is on.</summary>
/// <param name="Id">The Id Linear serves.</param>
/// <param name="Body">The Body Linear serves.</param>
/// <param name="Issue">The Issue Linear serves.</param>
internal sealed record LinearLiveComment(string Id, string Body, LinearNamed? Issue = null);
