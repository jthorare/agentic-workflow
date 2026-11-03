namespace Aw.Judge.Core.Images;

/// <summary>An <see cref="IImageFolder"/> that is removed on dispose.</summary>
public interface IImageWorkspace : IImageFolder, IDisposable;
