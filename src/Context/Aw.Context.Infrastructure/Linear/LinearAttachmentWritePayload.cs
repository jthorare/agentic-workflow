namespace Aw.Context.Infrastructure.Linear;

/// <summary>What attachmentLinkURL answers.</summary>
/// <param name="Success">The Success Linear serves; false is a refusal even in a 200.</param>
/// <param name="Attachment">The Attachment Linear serves.</param>
internal sealed record LinearAttachmentWritePayload(bool Success = false, LinearIssueNode? Attachment = null);
