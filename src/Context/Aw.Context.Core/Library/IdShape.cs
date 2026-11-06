namespace Aw.Context.Core.Library;

/// <summary>The three outcomes of reading a possible id.</summary>
internal enum IdShape
{
    /// <summary>The text does not start like an id.</summary>
    None,

    /// <summary>A good id.</summary>
    Valid,

    /// <summary>It starts like an id (REQ-, AC-, TP-, TASK-, Ph and a digit) but the rest is wrong.</summary>
    Bad,
}
