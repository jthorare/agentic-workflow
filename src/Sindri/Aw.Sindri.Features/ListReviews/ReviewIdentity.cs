using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Features.ListReviews;

/// <summary>Who the Reviews tab reads as.</summary>
/// <param name="Connect">The connect action, or null when Reviews can read.</param>
/// <param name="Login">The login (the profile's override, else the connection's), or null.</param>
internal sealed record ReviewIdentity(ReviewConnect? Connect, string? Login);
