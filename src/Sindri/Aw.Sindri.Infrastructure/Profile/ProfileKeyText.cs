using Aw.Kernel;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>The text edit behind 'profile set': one scalar key of a profile's YAML, every other byte as it was.</summary>
internal static class ProfileKeyText
{
    /// <summary>Sets <paramref name="keyPath"/> to <paramref name="value"/> in <paramref name="text"/>.</summary>
    /// <param name="text">The profile file's text.</param>
    /// <param name="keyPath">The dotted key.</param>
    /// <param name="value">The value as typed.</param>
    /// <returns>The edited text (equal to <paramref name="text"/> when the key already has the value); SND-CLI-003 for a bad key; SND-PROFILE-002 for a layout it won't edit.</returns>
    internal static Result<string> Edit(string text, string keyPath, string value) => throw new NotSupportedException();

    /// <summary>The YAML scalar for <paramref name="value"/>: booleans and numbers plain, a plain-safe word plain, anything else quoted.</summary>
    /// <param name="value">The value as typed.</param>
    /// <returns>The scalar text.</returns>
    internal static string Scalar(string value) => throw new NotSupportedException();
}
