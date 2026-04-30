using Adaptive.ObservabilityClient;
using Xunit;

namespace Adaptive.ObservabilityClient.Tests;

public class RouteNormalizerTests
{
    [Theory]
    [InlineData("/users/42", "/users/:id")]
    [InlineData("/users/42?tab=info", "/users/:id")]
    [InlineData("/sessions/550e8400-e29b-41d4-a716-446655440000", "/sessions/:id")]
    [InlineData("/posthog-500-test", "/posthog-500-test")]
    [InlineData("/api/orders/9", "/api/orders/:id")]
    [InlineData("", "/")]
    public void Normalize_works(string input, string expected)
        => Assert.Equal(expected, RouteNormalizer.Normalize(input));

    [Theory]
    [InlineData("/api/auth/login", "auth")]
    [InlineData("/api/users/:id", "users")]
    [InlineData("/api/widgets/:id", "widgets")]
    [InlineData("/dashboard", "other")]
    public void EndpointGroup_works(string normalized, string expected)
        => Assert.Equal(expected, RouteNormalizer.EndpointGroup(normalized));
}
