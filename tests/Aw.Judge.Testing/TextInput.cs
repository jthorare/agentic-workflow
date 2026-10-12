namespace Aw.Judge.Testing;

/// <summary>The test question's input: {"text": string}, required; unknown fields are dropped.</summary>
/// <param name="Text">The text.</param>
public sealed record TextInput(string Text);
