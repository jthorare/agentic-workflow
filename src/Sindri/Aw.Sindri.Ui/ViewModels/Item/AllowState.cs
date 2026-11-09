namespace Aw.Sindri.Ui.ViewModels.Item;

/// <summary>Where one Allow press stands (states.json 'allow').</summary>
internal enum AllowState
{
    /// <summary>Not pressed.</summary>
    Idle,

    /// <summary>The request is on its way.</summary>
    Sending,

    /// <summary>The allowance is in force.</summary>
    Allowed,

    /// <summary>The server refused.</summary>
    Refused,
}
