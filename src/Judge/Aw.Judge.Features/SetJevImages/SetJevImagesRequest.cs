namespace Aw.Judge.Features.SetJevImages;

/// <summary>The SetJevImages request: 'judge config set providers jev-images true|false'.</summary>
/// <param name="On">Whether images are sent to Jev.</param>
public sealed record SetJevImagesRequest(bool On);
