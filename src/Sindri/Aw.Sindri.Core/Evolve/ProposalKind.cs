namespace Aw.Sindri.Core.Evolve;

/// <summary>What a proposal changes (issue 3787). A proposal is a typed record; nothing edits the repo directly.</summary>
internal enum ProposalKind
{
    /// <summary>An edit to one skill's files under skills/&lt;name&gt;/.</summary>
    SkillEdit,

    /// <summary>A style rule: a rule record under rules/engineering/ plus its check.</summary>
    StyleRule,

    /// <summary>A change to docs/.</summary>
    Docs,

    /// <summary>Any other repo change; always needs approval.</summary>
    Code,
}
