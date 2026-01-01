using FluentAssertions;
using System.Text.Json;

namespace BackupChrono.IntegrationTests.Controllers;

public class HealthControllerTests : IAsyncLifetime
{
    private BackupChronoWebApplicationFactory _factory = null!;
    private HttpClient _httpClient = null!;

    public async Task InitializeAsync()
    {
        _factory = new BackupChronoWebApplicationFactory();
        _httpClient = _factory.CreateClient();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _httpClient.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Get_ReturnsHealthStatus()
    {
        // Act
        var response = await _httpClient.GetAsync("health");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        var root = json.RootElement;

        root.GetProperty("status").GetString().Should().BeOneOf("Healthy", "Degraded", "Unhealthy");
        root.GetProperty("timestamp").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("version").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("uptime").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Get_IncludesHealthChecks()
    {
        // Act
        var response = await _httpClient.GetAsync("health");
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        var root = json.RootElement;

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        root.TryGetProperty("checks", out var checksElement).Should().BeTrue();
        checksElement.GetArrayLength().Should().BeGreaterThan(0);

        // Each check should have name and status
        foreach (var check in checksElement.EnumerateArray())
        {
            check.TryGetProperty("name", out _).Should().BeTrue();
            check.TryGetProperty("status", out _).Should().BeTrue();
        }
    }

    [Fact]
    public async Task GetDetailed_ReturnsDetailedHealthStatus()
    {
        // Act
        var response = await _httpClient.GetAsync("health/detailed");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        var root = json.RootElement;

        root.GetProperty("status").GetString().Should().BeOneOf("Healthy", "Degraded", "Unhealthy");
        root.TryGetProperty("checks", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Get_HealthStatusIsConsistent()
    {
        // Act
        var response = await _httpClient.GetAsync("health");
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        var root = json.RootElement;

        var status = root.GetProperty("status").GetString();
        var checks = root.GetProperty("checks");

        // Assert - Status should reflect check results
        if (status == "Unhealthy")
        {
            var hasCritical = checks.EnumerateArray().Any(c => c.GetProperty("status").GetString() == "Critical");
            hasCritical.Should().BeTrue("Unhealthy status should have at least one Critical check");
        }
        else if (status == "Degraded")
        {
            var hasWarning = checks.EnumerateArray().Any(c => c.GetProperty("status").GetString() == "Warning");
            hasWarning.Should().BeTrue("Degraded status should have at least one Warning check");
        }
    }
}
