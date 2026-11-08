namespace Aw.Sindri.Core.Todos;

/// <summary>The CLI verbs that take a todo id and which todos each resolves a prefix among (todo spec §4.4 "Ids").</summary>
public enum TodoCliVerb
{
    /// <summary>'todo show': every todo.</summary>
    Show,

    /// <summary>'todo edit': open todos.</summary>
    Edit,

    /// <summary>'todo done': open todos.</summary>
    Done,

    /// <summary>'todo reopen': done todos.</summary>
    Reopen,

    /// <summary>'todo remove': open todos.</summary>
    Remove,

    /// <summary>'todo restore': removed todos.</summary>
    Restore,

    /// <summary>'todo keep': open todos.</summary>
    Keep,
}
