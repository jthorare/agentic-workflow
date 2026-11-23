namespace Aw.Sindri.Core.Messages;

/// <summary>The one refusal every Messages stub throws until its phase builds it (messages spec §14, M1s): the phase name says who builds it.</summary>
internal static class MessageStubs
{
    /// <summary>The exception a stub throws.</summary>
    /// <param name="member">The stubbed member.</param>
    /// <param name="phase">The phase that builds it ('M1b').</param>
    /// <returns>The exception to throw.</returns>
    internal static NotSupportedException NotBuilt(string member, string phase) =>
        new($"{member}{NotBuiltText}{phase}");

    /// <summary>The words every Messages stub's refusal holds, Core's and Features' alike ('ResolveLinks is not built yet: messages spec §14 phase M1g'); the red tests skip on them only.</summary>
    internal const string NotBuiltText = " is not built yet: messages spec §14 phase ";
}
