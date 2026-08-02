using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace UnitTests.Api;

public sealed class DeploymentApiTests
{
    private sealed record HealthResponse(string Status);

    private static HttpRequestMessage CreatePreflightRequest(string origin)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Options, "/instruments");

        request.Headers.TryAddWithoutValidation(
            "Origin", origin);
        request.Headers.TryAddWithoutValidation(
            "Access-Control-Request-Method", "GET");

        return request;
    }

    [Fact]
    public async Task Health_ReturnsHealthyResponse()
    {
        using var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();
        using var response = await client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var health = await response.Content.ReadFromJsonAsync<HealthResponse>();
        health.Should().Be(new HealthResponse("healthy"));
    }

    [Fact]
    public async Task Cors_AllowsConfiguredOrigin()
    {
        const string allowedOrigin = "http://localhost:4200";

        using var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();
        using var request = CreatePreflightRequest(allowedOrigin);
        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        response.Headers
            .GetValues("Access-Control-Allow-Origin")
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .Be(allowedOrigin);
    }

    [Fact]
    public async Task Cors_DoesNotAllowUnconfiguredOrigin()
    {
        const string rejectedOrigin = "https://unexpected.example";

        using var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();
        using var request = CreatePreflightRequest(rejectedOrigin);
        using var response = await client.SendAsync(request);

        response.Headers
            .Contains("Access-Control-Allow-Origin")
            .Should()
            .BeFalse();
    }
}
