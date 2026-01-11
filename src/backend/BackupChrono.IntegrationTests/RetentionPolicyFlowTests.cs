using System.Net;
using System.Net.Http.Json;
using System.Linq;
using BackupChrono.Core.DTOs;
using BackupChrono.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace BackupChrono.IntegrationTests;

public class RetentionPolicyFlowTests : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _factory = new BackupChronoWebApplicationFactory();
        _client = _factory.CreateClient();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task ExecuteRetentionPolicy_ReturnsResults()
    {
        var response = await _client.PostAsync("api/retention-policy/execute?dryRun=true", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<List<RetentionRunResult>>();
        payload.Should().NotBeNull();
        // Results may be empty if no devices configured, which is OK for integration test
        payload.Should().BeOfType<List<RetentionRunResult>>();
    }

    [Fact]
    public async Task GetLastRun_ReturnsSummary()
    {
        var response = await _client.GetAsync("api/retention-policy/last-run");

        // API returns 204 No Content if retention has never been run, 200 OK otherwise
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var summary = await response.Content.ReadFromJsonAsync<RetentionLastRun>();
            summary.Should().NotBeNull();
        }
    }
}
