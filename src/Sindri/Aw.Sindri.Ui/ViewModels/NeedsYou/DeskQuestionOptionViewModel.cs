namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>One answer a question offers: choosing it fills the question's answer with its words.</summary>
internal sealed class DeskQuestionOptionViewModel
{
    private readonly DeskQuestionViewModel _question;

    internal DeskQuestionOptionViewModel(DeskQuestionViewModel question, string text)
    {
        (_question, Text) = (question, text);
    }

    /// <summary>The option in words.</summary>
    public string Text { get; }

    /// <summary>Fills the question's answer with this option.</summary>
    internal void Choose() => _question.Answer = Text;
}
