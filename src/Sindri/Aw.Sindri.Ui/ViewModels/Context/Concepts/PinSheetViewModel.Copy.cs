namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>The sheet's fixed words (<see cref="PinSheetViewModel"/>, continued): properties because the view binds them.</summary>
internal sealed partial class PinSheetViewModel
{
#pragma warning disable CA1822, MA0041
    /// <summary>The name field's label.</summary>
    public string NameLabel => Labels.PinSheetName;

    /// <summary>The definition field's label.</summary>
    public string DefinitionLabel => Labels.PinSheetDefinition;

    /// <summary>The line under the definition.</summary>
    public string DefinitionHint => Labels.PinSheetDefinitionHint;

    /// <summary>The words field's label.</summary>
    public string WordsLabel => Labels.PinSheetWords;

    /// <summary>The box a word is typed in.</summary>
    public string AddWordBox => Labels.PinSheetAddWordBox;

    /// <summary>The button beside it.</summary>
    public string AddWordText => Labels.PinSheetAddWord;

    /// <summary>The features field's label.</summary>
    public string FeaturesLabel => Labels.PinSheetFeatures;

    /// <summary>The picker's search box.</summary>
    public string FindFeatureLabel => Labels.PinSheetFindFeature;

    /// <summary>The picker's retry button.</summary>
    public string TryAgainText => Labels.PinSheetTryAgain;

    /// <summary>The Unpin button that asks.</summary>
    public string UnpinText => Labels.PinSheetUnpin;

    /// <summary>Answers the Unpin question with no.</summary>
    public string KeepPinText => Labels.PinSheetKeepPin;

    /// <summary>The question before a discard.</summary>
    public string DiscardQuestion => Labels.PinSheetDiscardQuestion;

    /// <summary>Discards.</summary>
    public string DiscardText => Labels.PinSheetDiscard;

    /// <summary>Goes back to the fields.</summary>
    public string KeepEditingText => Labels.PinSheetKeepEditing;

    /// <summary>The close button.</summary>
    public string EscText => Labels.PinSheetEsc;
#pragma warning restore CA1822, MA0041
}
