namespace Aw.Evidence.Core.Desktop;

/// <summary>A running <see cref="IStaticSite"/>; disposing stops it.</summary>
public interface IServedSite : IAsyncDisposable
{
    /// <summary>The origin it answers on ('http://127.0.0.1:PORT').</summary>
    public string Origin { get; }
}
