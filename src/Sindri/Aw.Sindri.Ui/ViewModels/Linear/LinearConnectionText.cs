namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>Settings' Linear card's own copy (<see cref="Labels"/>) as the view binds it.</summary>
internal sealed class LinearConnectionText
{
    /// <summary>The one instance.</summary>
    internal static LinearConnectionText Instance { get; } = new();

    /// <summary>The Connections group's heading.</summary>
    public string ConnectionsHeading { get; } = Labels.ConnectionsHeading;

    /// <summary>The line under the group's heading.</summary>
    public string ConnectionsIntro { get; } = Labels.ConnectionsIntro;

    /// <summary>The card's name.</summary>
    public string Name { get; } = Labels.LinearConnName;

    /// <summary><see cref="Labels.LinearConnReadHeading"/>.</summary>
    public string ReadHeading { get; } = Labels.LinearConnReadHeading;

    /// <summary><see cref="Labels.LinearConnWriteHeading"/>.</summary>
    public string WriteHeading { get; } = Labels.LinearConnWriteHeading;

    /// <summary><see cref="Labels.LinearConnReadIntro"/>.</summary>
    public string ReadIntro { get; } = Labels.LinearConnReadIntro;

    /// <summary><see cref="Labels.LinearConnReadWhere"/>.</summary>
    public string ReadWhere { get; } = Labels.LinearConnReadWhere;

    /// <summary><see cref="Labels.LinearConnWriteIntro"/>.</summary>
    public string WriteIntro { get; } = Labels.LinearConnWriteIntro;

    /// <summary><see cref="Labels.LinearConnReadKeyName"/>.</summary>
    public string ReadKeyName { get; } = Labels.LinearConnReadKeyName;

    /// <summary><see cref="Labels.LinearConnWriteKeyName"/>.</summary>
    public string WriteKeyName { get; } = Labels.LinearConnWriteKeyName;

    /// <summary><see cref="Labels.LinearConnKeyPlaceholder"/>.</summary>
    public string KeyPlaceholder { get; } = Labels.LinearConnKeyPlaceholder;

    /// <summary><see cref="Labels.LinearConnListTeams"/>.</summary>
    public string ListTeams { get; } = Labels.LinearConnListTeams;

    /// <summary><see cref="Labels.LinearConnTeamsLegend"/>.</summary>
    public string TeamsLegend { get; } = Labels.LinearConnTeamsLegend;

    /// <summary><see cref="Labels.LinearConnCheckAndSave"/>.</summary>
    public string CheckAndSave { get; } = Labels.LinearConnCheckAndSave;

    /// <summary><see cref="Labels.LinearConnReplace"/>.</summary>
    public string Replace { get; } = Labels.LinearConnReplace;

    /// <summary><see cref="Labels.LinearConnCancel"/>.</summary>
    public string Cancel { get; } = Labels.LinearConnCancel;

    /// <summary><see cref="Labels.LinearConnRemove"/>.</summary>
    public string Remove { get; } = Labels.LinearConnRemove;

    /// <summary><see cref="Labels.LinearConnRemoveName"/>.</summary>
    public string RemoveName { get; } = Labels.LinearConnRemoveName;

    /// <summary><see cref="Labels.LinearConnRemoveAsk"/>.</summary>
    public string RemoveAsk { get; } = Labels.LinearConnRemoveAsk;

    /// <summary><see cref="Labels.LinearConnKeep"/>.</summary>
    public string Keep { get; } = Labels.LinearConnKeep;

    /// <summary><see cref="Labels.LinearConnRemoveIt"/>.</summary>
    public string RemoveIt { get; } = Labels.LinearConnRemoveIt;

    /// <summary><see cref="Labels.LinearConnReview"/>.</summary>
    public string Review { get; } = Labels.LinearConnReview;
}
