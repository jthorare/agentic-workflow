namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>POST /sindri/library/aw's body (review page spec §6.3): turns the built-in aw folder on or off.</summary>
/// <param name="On">True to read it.</param>
/// <param name="Approve">True to approve the profile change when it is the only waiting one.</param>
internal sealed record LibraryAwSwitch(bool On, bool Approve);
