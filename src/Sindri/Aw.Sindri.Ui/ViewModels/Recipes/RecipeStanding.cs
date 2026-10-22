namespace Aw.Sindri.Ui.ViewModels.Recipes;

/// <summary>Where a workflow stands, in the three words a person sees; only an approved one starts a run.</summary>
internal enum RecipeStanding
{
    /// <summary>Approved: it can start a run.</summary>
    Approved,

    /// <summary>Registered but not approved (also any standing this build doesn't know).</summary>
    NotApproved,

    /// <summary>Approved once and since revoked.</summary>
    Revoked,
}
