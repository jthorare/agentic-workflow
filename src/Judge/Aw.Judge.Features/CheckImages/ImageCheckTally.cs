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
            $"{label,-12}broken judged looks-right {BrokenJudgedRight}/{Broken} · right pages judged looks-right {RightJudgedRight}/{Right} · {seconds} s per call");
    }
}
