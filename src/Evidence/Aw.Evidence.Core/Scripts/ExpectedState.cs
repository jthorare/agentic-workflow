using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Scripts;

/// <summary>
/// A machine-checkable state that must hold after a step. JSON carries a leading 'kind' discriminator:
/// text-visible, text-absent, testid-visible, testid-absent, url-path, url-search or input-value.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(TextVisible), "text-visible")]
[JsonDerivedType(typeof(TextAbsent), "text-absent")]
[JsonDerivedType(typeof(TestIdVisible), "testid-visible")]
[JsonDerivedType(typeof(TestIdAbsent), "testid-absent")]
[JsonDerivedType(typeof(UrlPath), "url-path")]
[JsonDerivedType(typeof(UrlSearch), "url-search")]
[JsonDerivedType(typeof(InputValue), "input-value")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public abstract record ExpectedState
{
    /// <summary>A short description for people and the selector-repair prompt.</summary>
    /// <returns>The description.</returns>
    public abstract string Describe();

    /// <summary>The rule this state breaks, or null when it is well formed.</summary>
    /// <returns>The problem, or null.</returns>
    public abstract string? Problem();
}
