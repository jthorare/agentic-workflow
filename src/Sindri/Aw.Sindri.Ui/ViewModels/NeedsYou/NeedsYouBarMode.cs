namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>What the inbox's action bar shows (F3 needs-you spec §3).</summary>
internal enum NeedsYouBarMode
{
    /// <summary>The primary action is ready (the prototype's #pending).</summary>
    Pending,

    /// <summary>A was pressed on a request not read to the end: the primary reads "Approve?" and a second A approves (#confirm).</summary>
    Confirming,

    /// <summary>The reason box shows and Send is disabled until there is text (#sendback).</summary>
    SendingBack,

    /// <summary>The request was approved: the card is replaced by the done card (#approved).</summary>
    Approved,
}
