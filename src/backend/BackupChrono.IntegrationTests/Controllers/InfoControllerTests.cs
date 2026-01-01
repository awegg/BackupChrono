using FluentAssertions;
using System.Text.Json;

namespace BackupChrono.IntegrationTests.Controllers;

public class InfoControllerTests : IAsyncLifetime
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
    public async Task Get_ReturnsApiInfo()
    {
        // Act
        var response = await _httpClient.GetAsync("api/info");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        var root = json.RootElement;

        root.GetProperty("name").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("version").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("timestamp").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Get_ReturnsCurrentUtcTimestamp()
    {
        // Act
        var response = await _httpClient.GetAsync("api/info");
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        var timestampStr = json.RootElement.GetProperty("timestamp").GetString();

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        DateTime.TryParse(timestampStr, out var timestamp).Should().BeTrue();
        
        // Timestamp should be recent (within last minute)
        var timeDiff = DateTime.UtcNow - timestamp;
        timeDiff.TotalSeconds.Should().BeLessThan(60);
    }
}
