using Aw.Context.Core.Linear;
using Aw.Context.Testing.Fixtures;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Tests.Secrets;

/// <summary>
/// Sindri's pointer parser and Context's 'context.sources.linear.token' check agree: every pointer Sindri accepts passes
/// profile validate too, so a pointer is never refused there and then resolved fine by doctor. The list is
/// <see cref="SecretPointerSamples"/>.
/// </summary>
public sealed class SecretPointerAgreementTests
{
    public static TheoryData<string> Pointers => [.. SecretPointerSamples.Accepted];

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Pointers))]
    public void Every_pointer_sindri_parses_passes_contexts_check(string text)
    {
        var parsed = SecretPointer.Parse(text);
        var settings = LinearSettings.Create(url: null, text, teams: []);

        Assert.True(parsed.IsSuccess, parsed.IsError ? parsed.Error.Message : null);
        Assert.True(settings.IsSuccess, settings.IsError ? settings.Error.Message : null);
    }
}
