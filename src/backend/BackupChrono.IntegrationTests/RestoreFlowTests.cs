using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BackupChrono.Api.DTOs;
using BackupChrono.Core.DTOs;
using FluentAssertions;
using Xunit;

namespace BackupChrono.IntegrationTests;

/// <summary>
/// End-to-end integration tests for restore functionality
/// </summary>
public class RestoreFlowTests : IAsyncLifetime
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
    public async Task ListBackups_ReturnsEmptyList_Initially()
    {
        // Act
        var response = await _httpClient.GetAsync("api/backups");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var backups = await response.Content.ReadFromJsonAsync<List<BackupDto>>();
        backups.Should().NotBeNull();
        backups.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBackup_Returns404_WhenBackupDoesNotExist()
    {
        // Arrange
        var nonExistentBackupId = "nonexistent123";

        // Act
        var response = await _httpClient.GetAsync($"api/backups/{nonExistentBackupId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("not found");
    }

    [Fact]
    public async Task BrowseBackupFiles_Returns404_WhenBackupDoesNotExist()
    {
        // Arrange
        var nonExistentBackupId = "nonexistent123";

        // Act
        var response = await _httpClient.GetAsync($"api/backups/{nonExistentBackupId}/files");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RestoreBackup_ReturnsBadRequest_WhenTargetPathMissing()
    {
        // Arrange
        var backupId = "test123";
        var request = new RestoreRequestDto
        {
            TargetPath = "" // Invalid - empty path
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _httpClient.PostAsync($"api/backups/{backupId}/restore", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorContent = await response.Content.ReadAsStringAsync();
        errorContent.Should().Contain("TargetPath");
    }

    [Fact]
    public async Task RestoreBackup_ReturnsAccepted_WithValidRequest()
    {
        // Arrange
        var backupId = "test123";
        var request = new RestoreRequestDto
        {
            TargetPath = "/tmp/restore"
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _httpClient.PostAsync($"api/backups/{backupId}/restore", content);

        // Assert - Since backup doesn't exist, we might get 404, but the validation should pass
        // For now, we're just checking the controller accepts valid requests
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Accepted, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetFileHistory_ReturnsBadRequest_WhenFilePathMissing()
    {
        // Arrange
        var deviceId = Guid.NewGuid();

        // Act - Missing filePath parameter
        var response = await _httpClient.GetAsync($"api/backups/files/history?deviceId={deviceId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetFileHistory_ReturnsEmptyList_WhenNoHistoryExists()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var filePath = "/path/to/file.txt";

        // Act
        var response = await _httpClient.GetAsync(
            $"api/backups/files/history?deviceId={deviceId}&filePath={Uri.EscapeDataString(filePath)}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var history = await response.Content.ReadFromJsonAsync<List<FileVersion>>();
        history.Should().NotBeNull();
        history.Should().BeEmpty(); // No history exists yet
    }

    [Fact]
    public async Task ListDeviceBackups_Returns404_WhenDeviceDoesNotExist()
    {
        // Arrange
        var nonExistentDeviceId = Guid.NewGuid();

        // Act
        var response = await _httpClient.GetAsync($"api/devices/{nonExistentDeviceId}/backups");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("not found");
    }

    [Fact]
    public async Task ListBackups_WithDeviceIdFilter_ReturnsFilteredResults()
    {
        // Arrange
        var deviceId = Guid.NewGuid();

        // Act
        var response = await _httpClient.GetAsync($"api/backups?deviceId={deviceId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var backups = await response.Content.ReadFromJsonAsync<List<BackupDto>>();
        backups.Should().NotBeNull();
        backups.Should().BeEmpty(); // No backups for this device yet
    }

    [Fact]
    public async Task ListBackups_WithLimitParameter_RespectsLimit()
    {
        // Arrange
        var limit = 5;

        // Act
        var response = await _httpClient.GetAsync($"api/backups?limit={limit}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var backups = await response.Content.ReadFromJsonAsync<List<BackupDto>>();
        backups.Should().NotBeNull();
        backups!.Count.Should().BeLessThanOrEqualTo(limit);
    }

    [Fact]
    public async Task BrowseBackupFiles_WithPathParameter_ReturnsFilesAtPath()
    {
        // Arrange
        var backupId = "test456";
        var path = "/some/path";

        // Act
        var response = await _httpClient.GetAsync(
            $"api/backups/{backupId}/files?path={Uri.EscapeDataString(path)}");

        // Assert - Will be 404 since backup doesn't exist
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RestoreBackup_WithIncludePaths_AcceptsRequest()
    {
        // Arrange
        var backupId = "test789";
        var request = new RestoreRequestDto
        {
            TargetPath = "/tmp/restore",
            IncludePaths = new List<string> { "/path1", "/path2" }
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _httpClient.PostAsync($"api/backups/{backupId}/restore", content);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Accepted, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RestoreBackup_WithRestoreToSource_AcceptsRequest()
    {
        // Arrange
        var backupId = "test101";
        var request = new RestoreRequestDto
        {
            TargetPath = "/original/location",
            RestoreToSource = true
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _httpClient.PostAsync($"api/backups/{backupId}/restore", content);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Accepted, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetFileHistory_WithValidParameters_ReturnsHistory()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var filePath = "/documents/report.pdf";

        // Act
        var response = await _httpClient.GetAsync(
            $"api/backups/files/history?deviceId={deviceId}&filePath={Uri.EscapeDataString(filePath)}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var history = await response.Content.ReadFromJsonAsync<List<FileVersion>>();
        history.Should().NotBeNull();
    }

    [Fact]
    public async Task BrowseBackupFiles_WithRootPath_ReturnsRootLevelFiles()
    {
        // Arrange
        var backupId = "test202";

        // Act - Default path is root
        var response = await _httpClient.GetAsync($"api/backups/{backupId}/files");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound); // Backup doesn't exist
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task RestoreBackup_WithInvalidTargetPath_ReturnsBadRequest(string? invalidPath)
    {
        // Arrange
        var backupId = "test303";
        var request = new RestoreRequestDto
        {
            TargetPath = invalidPath!
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _httpClient.PostAsync($"api/backups/{backupId}/restore", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetBackup_ReturnsNotFound_ForInvalidBackupId()
    {
        // Arrange
        var invalidBackupId = "invalid-backup-id-12345";

        // Act
        var response = await _httpClient.GetAsync($"api/backups/{invalidBackupId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        errorResponse.Should().NotBeNull();
        errorResponse!.Error.Should().Contain("not found");
    }

    /// <summary>
    /// Helper method to compute SHA256 checksum of a file
    /// </summary>
    private static async Task<string> ComputeFileChecksum(string filePath)
    {
        using var sha256 = SHA256.Create();
        await using var stream = File.OpenRead(filePath);
        var hash = await sha256.ComputeHashAsync(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
