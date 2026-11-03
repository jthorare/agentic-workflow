using System.Globalization;

namespace Aw.Judge.Features.CheckImages;

/// <summary>What one provider did on the cases: how many broken and right pages it called 'looks-right', and how long a call took.</summary>
internal sealed class ImageCheckTally
{
    private TimeSpan _elapsed;

    public int Broken { get; private set; }

    public int BrokenJudgedRight { get; private set; }

    public int Right { get; private set; }

    public int RightJudgedRight { get; private set; }

    /// <summary>Cases the provider passed over because the image is outside its size limits; in neither count.</summary>
    public int TooBig { get; private set; }

    public void SkipTooBig() => TooBig++;

    /// <summary>Cases the provider did not answer (an error, no key, a timeout, a 413); in neither count and not timed, and never a pass.</summary>
    public int Errored { get; private set; }

    public void Error() => Errored++;

    /// <summary>Whether the bar holds: at least one broken case was really judged, none was called looks-right, and nothing errored.</summary>
    public bool BarHolds => Broken > 0 && BrokenJudgedRight == 0 && Errored == 0;

    public void Add(bool pageIsRight, bool judgedRight, TimeSpan elapsed)
    {
        _elapsed += elapsed;
        if (pageIsRight)
        {
            Right++;
            RightJudgedRight += judgedRight ? 1 : 0;
        }
        else
        {
            Broken++;
            BrokenJudgedRight += judgedRight ? 1 : 0;
        }
    }

    public string Line(string label)
    {
        var seconds = (_elapsed / Math.Max(1, Broken + Right)).TotalSeconds.ToString("F1", CultureInfo.InvariantCulture);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{label,-12}broken judged looks-right {BrokenJudgedRight}/{Broken} · right pages judged looks-right {RightJudgedRight}/{Right}{Skipped()}{ErroredText()} · {seconds} s per call");
    }

    private string Skipped() =>
        TooBig == 0 ? "" : string.Create(CultureInfo.InvariantCulture, $" · {TooBig} over its size limits, skipped");

    private string ErroredText() =>
        Errored == 0 ? "" : string.Create(CultureInfo.InvariantCulture, $" · {Errored} errored");
}
