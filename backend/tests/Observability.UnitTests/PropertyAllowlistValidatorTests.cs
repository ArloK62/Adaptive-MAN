using System.Text.Json;
using FluentAssertions;
using Observability.Application.Ingestion;
using Xunit;

namespace Observability.UnitTests;

public class PropertyAllowlistValidatorTests
{
    private readonly PropertyAllowlistValidator _v = new();

    private static Dictionary<string, JsonElement> Props(object obj)
    {
        var json = JsonSerializer.Serialize(obj);
        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
    }

    [Fact]
    public void PageViewed_WithAllowedProps_IsValid()
    {
        var result = _v.Validate("page_viewed", Props(new
        {
            normalized_route = "/patients/:id",
            feature_area = "patients",
            release_sha = "abc1234"
        }));

        result.IsValid.Should().BeTrue();
        result.SafeProperties.Should().ContainKeys("normalized_route", "feature_area", "release_sha");
    }

    [Fact]
    public void PageViewed_WithoutRequired_IsInvalid()
    {
        var result = _v.Validate("page_viewed", Props(new { feature_area = "patients" }));

        result.IsValid.Should().BeFalse();
        result.Reason.Should().Be("missing_required");
        result.RejectedField.Should().Be("normalized_route");
    }

    [Fact]
    public void UnknownProperty_IsSilentlyDropped()
    {
        var result = _v.Validate("page_viewed", Props(new
        {
            normalized_route = "/x",
            cat_breed = "tabby" // unknown — should drop, not violate
        }));

        result.IsValid.Should().BeTrue();
        result.SafeProperties.Should().NotContainKey("cat_breed");
    }

    [Theory]
    [InlineData("email", "x@y.com")]
    [InlineData("username", "alice")]
    [InlineData("stack_trace", "at Foo.Bar()")]
    [InlineData("exception_message", "boom")]
    [InlineData("raw_url", "/patients/123?q=1")]
    public void ForbiddenField_IsRejected(string field, string value)
    {
        var props = new Dictionary<string, JsonElement>
        {
            ["normalized_route"] = JsonDocument.Parse("\"/x\"").RootElement,
            [field] = JsonDocument.Parse($"\"{value}\"").RootElement,
        };

        var result = _v.Validate("page_viewed", props);

        result.IsValid.Should().BeFalse();
        result.Reason.Should().Be("forbidden_field");
        result.RejectedField.Should().Be(field);
    }

    [Fact]
    public void UnknownEvent_IsRejected()
    {
        var result = _v.Validate("nope", null);

        result.IsValid.Should().BeFalse();
        result.Reason.Should().Be("unknown_event");
    }

    [Fact]
    public void EveryPhase1Event_HasACatalogEntry()
    {
        var expected = new[]
        {
            "auth_login_success", "auth_logout", "page_viewed",
            "api_request_failed", "frontend_exception",
            "server_error_occurred", "background_job_failed",
            "dev_smoke_test"
        };

        foreach (var name in expected)
        {
            EventCatalog.Phase1.Should().ContainKey(name);
        }
    }
}
