using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Features.ListReviews;

/// <summary>Who the Reviews tab reads as.</summary>
/// <param name="Connect">The connect action, or null when Reviews can read.</param>
/// <param name="Login">The login (the profile's override, else the connection's), or null.</param>
/// <param name="Connection">The GitHub connection's name, or null.</param>
/// <param name="Miss">Why the connection gave no login.</param>
internal sealed record ReviewIdentity(ReviewConnect? Connect, string? Login, string? Connection = null, GitHubLoginMiss Miss = GitHubLoginMiss.None);
