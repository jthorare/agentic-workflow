namespace Aw.Sindri.Ui.ViewModels.Item;

/// <summary>Where one Allow press stands (states.json 'allow').</summary>
internal enum AllowState
{
    /// <summary>Not pressed.</summary>
    Idle,

    /// <summary>The request is on its way.</summary>
    Sending,

    /// <summary>The draft edit waits for the owner's approval.</summary>
    NeedsApproval,

    /// <summary>The approval went through.</summary>
    Allowed,

    /// <summary>The server refused.</summary>
    Refused,
}
