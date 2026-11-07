namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>Where focus goes in the Linear issue sheet when the view model asks.</summary>
internal enum LinearIssueFocus
{
    /// <summary>The title box (after an applied save).</summary>
    Title,

    /// <summary>The conflict banner (after a 409).</summary>
    Banner,

    /// <summary>The Keep editing button of the discard question.</summary>
    KeepEditing,

    /// <summary>The comment box (after a comment is posted).</summary>
    CommentBox,
}
