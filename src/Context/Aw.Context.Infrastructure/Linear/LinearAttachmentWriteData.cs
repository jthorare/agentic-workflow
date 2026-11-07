using System.Text.Json.Serialization;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>The data of the AttachmentLinkUrl operation.</summary>
/// <param name="AttachmentLinkUrl">The attachmentLinkURL payload Linear serves.</param>
internal sealed record LinearAttachmentWriteData([property: JsonPropertyName("attachmentLinkURL")] LinearAttachmentWritePayload? AttachmentLinkUrl = null);
