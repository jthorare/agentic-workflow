namespace Aw.Sindri.Core.Messages;

/// <summary>One span of mrkdwn text.</summary>
/// <param name="Kind">What the span is.</param>
/// <param name="Text">Its text, without the marks around it.</param>
public sealed record MrkdwnSpan(MrkdwnKind Kind, string Text);
