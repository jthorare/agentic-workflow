namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>The body of POST /sindri/linear/issues/{key}/comments.</summary>
/// <param name="ClientId">The app's uuid, made at the box's first character; a retry sends the same one.</param>
/// <param name="Body">The text, 1 to 65536 characters.</param>
internal sealed record LinearCommentBody(string ClientId, string Body);
