namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>The whole map's search filter (<see cref="WholeMapViewModel"/>, continued): while a search is on, only its matches and their direct links are drawn and can be hit.</summary>
internal sealed partial class WholeMapViewModel
{
    private (WholeMapGeometry Source, HashSet<string> Keep, WholeMapGeometry Shown)? _filtered;

    private HashSet<string>? Kept => _searchSet ?? _zoomSet;

    /// <summary>What is drawn and hit: the whole geometry, or while a search is on or the camera is on a query's answer, the matches and their direct links; null before the first answer.</summary>
    internal WholeMapGeometry? Shown => Drilled(Searched);

    // The geometry before a drill cuts it: the whole one, or the search's or the camera's set.
    private WholeMapGeometry? Searched
    {
        get
        {
            if (Geometry is not { } geometry || Kept is not { } keep)
            {
                return Geometry;
            }

            if (_filtered is not { } cached || !ReferenceEquals(cached.Source, geometry) || !ReferenceEquals(cached.Keep, keep))
            {
                _filtered = cached = (geometry, keep, WholeMapFilter.Keep(geometry, keep));
            }

            return cached.Shown;
        }
    }

    /// <summary>The box the camera fits and the minimap shows: the filtered set while a search is on, else the whole world.</summary>
    internal WholeRect? FitBox => Geometry is { } geometry && Shown is { } shown ? (Kept is null && !IsDrilled ? geometry.World : WholeMapFilter.FitBox(shown, geometry.World)) : null;
}
