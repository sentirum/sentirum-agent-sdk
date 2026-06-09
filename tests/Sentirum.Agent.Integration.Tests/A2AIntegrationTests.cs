using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sentirum.Agent.AspNetCore;
using Sentirum.Agent.Testing;
using Xunit;

namespace Sentirum.Agent.Integration.Tests;

/// <summary>
/// A2A protocol compliance tests.
/// </summary>
public sealed class A2AIntegrationTests : IAsyncLifetime, IDisposable
{
    private IHost? _host;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder([]);
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        builder.Services.AddSentirumCore();

        var json = await File.ReadAllTextAsync(Path.Combine("Fixtures", "a2a-greeting.json"));
        builder.Services.AddReplayAgent("a2a-agent", json);

        var app = builder.Build();

        app.MapA2AEndpoints("a2a-agent", new AgentCard
        {
            Name = "Test Agent",
            Description = "A2A integration test agent",
            Url = "http://localhost",
        });

        _host = app;
        await _host.StartAsync();

        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
        var address = addresses?.FirstOrDefault() ?? "http://127.0.0.1:5000";

        _client = new HttpClient { BaseAddress = new Uri(address) };
    }

    public async Task DisposeAsync()
    {
        Dispose();
        await Task.CompletedTask;
    }

    public void Dispose()
    {
        _host?.Dispose();
        _client?.Dispose();
    }

    [Fact]
    public async Task AgentCard_ReturnsValidJson()
    {
        var response = await _client!.GetAsync("/.well-known/agent.json");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("name").GetString().Should().Be("Test Agent");
        doc.RootElement.GetProperty("description").GetString().Should().Be("A2A integration test agent");
        doc.RootElement.GetProperty("version").GetString().Should().Be("1.0");
    }

    [Fact]
    public async Task CreateTask_Returns202()
    {
        var request = new { message = new { role = "user", text = "hello" } };
        var content = JsonContent.Create(request);

        var response = await _client!.PostAsync("/a2a/tasks", content);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
        doc.RootElement.GetProperty("status").GetInt32().Should().BeOneOf(0, 1, 2, 3); // Submitted, Working, Completed, Failed
    }
}
