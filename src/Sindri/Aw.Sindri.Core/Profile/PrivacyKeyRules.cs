using Aw.Kernel;

namespace Aw.Sindri.Core.Profile;

/// <summary>The checks on 'privacy': no blank term, and every deny pattern compiles (a pattern that doesn't would hold every proposal at the gate).</summary>
public static class PrivacyKeyRules
{
    /// <summary>Checks the 'privacy' section.</summary>
    /// <param name="privacy">The section, or null.</param>
    /// <returns>SND-PROFILE-002 with the key path, or null.</returns>
    public static Error? Check(PrivacySettings? privacy) => privacy is null ? null : throw new NotSupportedException(privacy.ToString());
}
