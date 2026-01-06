using BackupChrono.Core.ValueObjects;
using BackupChrono.Infrastructure.Restic;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BackupChrono.UnitTests.Infrastructure.Restic;

public class ResticBrowseParsingTests
{
    private readonly Mock<ILogger<ResticService>> _loggerMock;
    private readonly Mock<IResticClient> _clientMock;
    private readonly ResticService _service;

    public ResticBrowseParsingTests()
    {
        _loggerMock = new Mock<ILogger<ResticService>>();
        _clientMock = new Mock<IResticClient>();
        _service = new ResticService(_clientMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task BrowseBackup_ShouldParseTypicalResticOutput()
    {
        // Arrange - typical restic ls --json output
        var resticOutput = @"{""time"":""2025-01-04T16:38:13.123456789Z"",""tree"":""abc123"",""paths"":[""/""],""hostname"":""server"",""username"":""user""}
{""name"":""S"",""type"":""dir"",""path"":""/S"",""uid"":0,""gid"":0,""size"":0,""mode"":2147484159,""mtime"":""2024-12-15T10:30:00Z"",""atime"":""2024-12-15T10:30:00Z"",""ctime"":""2024-12-15T10:30:00Z"",""inode"":12345,""struct_type"":""node""}
{""name"":""System Volume Information"",""type"":""dir"",""path"":""/S/System Volume Information"",""uid"":0,""gid"":0,""size"":0,""mode"":2147484159,""mtime"":""2024-11-01T08:00:00Z"",""atime"":""2024-11-01T08:00:00Z"",""ctime"":""2024-11-01T08:00:00Z"",""inode"":23456,""struct_type"":""node""}
{""name"":""blazorboy"",""type"":""dir"",""path"":""/S/blazorboy"",""uid"":0,""gid"":0,""size"":0,""mode"":2147484159,""mtime"":""2024-12-20T14:25:00Z"",""atime"":""2024-12-20T14:25:00Z"",""ctime"":""2024-12-20T14:25:00Z"",""inode"":34567,""struct_type"":""node""}
{""name"":""file.txt"",""type"":""file"",""path"":""/S/file.txt"",""uid"":0,""gid"":0,""size"":1024,""mode"":33206,""mtime"":""2024-12-01T12:00:00Z"",""atime"":""2024-12-01T12:00:00Z"",""ctime"":""2024-12-01T12:00:00Z"",""inode"":45678,""struct_type"":""node""}";

        _clientMock
            .Setup(c => c.ExecuteCommand(
                It.Is<string[]>(args => args[0] == "ls" && args[1] == "f7229325" && args[2] == "--json"),
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<Action<string>?>(),
                It.IsAny<string?>(),
                It.IsAny<Action<string>?>()))
            .ReturnsAsync(resticOutput);

        // Act
        var result = await _service.BrowseBackup("f7229325", "/", "./repositories/device/share");

        // Assert
        var files = result.ToList();
        Assert.Single(files); // Should get only 1 item directly in / (the S folder)

        var sFolder = files.FirstOrDefault(f => f.Name == "S");
        Assert.NotNull(sFolder);
        Assert.True(sFolder.IsDirectory);
        Assert.Equal("/S", sFolder.Path);
    }

    [Fact]
    public async Task BrowseBackup_ShouldHandleModeAsString()
    {
        // Arrange - some restic versions might return mode as string
        var resticOutput = @"{""time"":""2025-01-04T16:38:13.123456789Z"",""tree"":""abc123"",""paths"":[""/""],""hostname"":""server"",""username"":""user""}
{""name"":""file.txt"",""type"":""file"",""path"":""/file.txt"",""uid"":0,""gid"":0,""size"":1024,""mode"":""33206"",""mtime"":""2024-12-01T12:00:00Z"",""atime"":""2024-12-01T12:00:00Z"",""ctime"":""2024-12-01T12:00:00Z"",""inode"":45678,""struct_type"":""node""}";

        _clientMock
            .Setup(c => c.ExecuteCommand(
                It.Is<string[]>(args => args[0] == "ls" && args[1] == "test123" && args[2] == "--json"),
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<Action<string>?>(),
                It.IsAny<string?>(),
                It.IsAny<Action<string>?>()))
            .ReturnsAsync(resticOutput);

        // Act
        var result = await _service.BrowseBackup("test123", "/", "./repositories/device/share");

        // Assert
        var files = result.ToList();
        Assert.Single(files);
        Assert.Equal("file.txt", files[0].Name);
    }

    [Fact]
    public async Task BrowseBackup_ShouldHandleMissingOptionalFields()
    {
        // Arrange - minimal JSON with only required fields
        var resticOutput = @"{""time"":""2025-01-04T16:38:13.123456789Z"",""tree"":""abc123"",""paths"":[""/""],""hostname"":""server"",""username"":""user""}
{""name"":""file.txt"",""type"":""file"",""path"":""/file.txt"",""struct_type"":""node""}
{""name"":""folder"",""type"":""dir"",""path"":""/folder"",""struct_type"":""node""}";

        _clientMock
            .Setup(c => c.ExecuteCommand(
                It.Is<string[]>(args => args[0] == "ls" && args[1] == "test123" && args[2] == "--json"),
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<Action<string>?>(),
                It.IsAny<string?>(),
                It.IsAny<Action<string>?>()))
            .ReturnsAsync(resticOutput);

        // Act
        var result = await _service.BrowseBackup("test123", "/", "./repositories/device/share");

        // Assert
        var files = result.ToList();
        Assert.Equal(2, files.Count);
        
        var file = files.FirstOrDefault(f => f.Name == "file.txt");
        Assert.NotNull(file);
        Assert.False(file.IsDirectory);
        Assert.Equal(0, file.Size); // Default when size is missing
        Assert.Null(file.Permissions); // Default when mode is missing

        var folder = files.FirstOrDefault(f => f.Name == "folder");
        Assert.NotNull(folder);
        Assert.True(folder.IsDirectory);
    }

    [Fact]
    public async Task BrowseBackup_ShouldFilterByPath_RootLevel()
    {
        // Arrange
        var resticOutput = @"{""time"":""2025-01-04T16:38:13.123456789Z"",""tree"":""abc123"",""paths"":[""/""],""hostname"":""server"",""username"":""user""}
{""name"":""S"",""type"":""dir"",""path"":""/S"",""mode"":2147484159,""struct_type"":""node""}
{""name"":""file.txt"",""type"":""file"",""path"":""/S/file.txt"",""mode"":33206,""size"":1024,""struct_type"":""node""}
{""name"":""subfolder"",""type"":""dir"",""path"":""/S/subfolder"",""mode"":2147484159,""struct_type"":""node""}
{""name"":""nested.txt"",""type"":""file"",""path"":""/S/subfolder/nested.txt"",""mode"":33206,""size"":512,""struct_type"":""node""}";

        _clientMock
            .Setup(c => c.ExecuteCommand(
                It.Is<string[]>(args => args[0] == "ls" && args[1] == "test123" && args[2] == "--json"),
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<Action<string>?>(),
                It.IsAny<string?>(),
                It.IsAny<Action<string>?>()))
            .ReturnsAsync(resticOutput);

        // Act - browse root
        var result = await _service.BrowseBackup("test123", "/", "./repositories/device/share");

        // Assert - should only get items directly in /
        var files = result.ToList();
        Assert.Single(files);
        Assert.Equal("S", files[0].Name);
        Assert.Equal("/S", files[0].Path);
    }

    [Fact]
    public async Task BrowseBackup_ShouldFilterByPath_Subfolder()
    {
        // Arrange
        var resticOutput = @"{""time"":""2025-01-04T16:38:13.123456789Z"",""tree"":""abc123"",""paths"":[""/""],""hostname"":""server"",""username"":""user""}
{""name"":""S"",""type"":""dir"",""path"":""/S"",""mode"":2147484159,""struct_type"":""node""}
{""name"":""file.txt"",""type"":""file"",""path"":""/S/file.txt"",""mode"":33206,""size"":1024,""struct_type"":""node""}
{""name"":""subfolder"",""type"":""dir"",""path"":""/S/subfolder"",""mode"":2147484159,""struct_type"":""node""}
{""name"":""nested.txt"",""type"":""file"",""path"":""/S/subfolder/nested.txt"",""mode"":33206,""size"":512,""struct_type"":""node""}";

        _clientMock
            .Setup(c => c.ExecuteCommand(
                It.Is<string[]>(args => args[0] == "ls" && args[1] == "test123" && args[2] == "--json"),
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<Action<string>?>(),
                It.IsAny<string?>(),
                It.IsAny<Action<string>?>()))
            .ReturnsAsync(resticOutput);

        // Act - browse /S
        var result = await _service.BrowseBackup("test123", "/S", "./repositories/device/share");

        // Assert - should only get items directly in /S
        var files = result.ToList();
        Assert.Equal(2, files.Count);
        
        var file = files.FirstOrDefault(f => f.Name == "file.txt");
        Assert.NotNull(file);
        Assert.Equal("/S/file.txt", file.Path);
        Assert.Equal(1024, file.Size);

        var folder = files.FirstOrDefault(f => f.Name == "subfolder");
        Assert.NotNull(folder);
        Assert.Equal("/S/subfolder", folder.Path);
        Assert.True(folder.IsDirectory);
    }

    [Fact]
    public async Task BrowseBackup_ShouldHandleLargeFileSize()
    {
        // Arrange
        var resticOutput = @"{""time"":""2025-01-04T16:38:13.123456789Z"",""tree"":""abc123"",""paths"":[""/""],""hostname"":""server"",""username"":""user""}
{""name"":""large.iso"",""type"":""file"",""path"":""/large.iso"",""size"":5368709120,""mode"":33206,""struct_type"":""node""}";

        _clientMock
            .Setup(c => c.ExecuteCommand(
                It.Is<string[]>(args => args[0] == "ls" && args[1] == "test123" && args[2] == "--json"),
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<Action<string>?>(),
                It.IsAny<string?>(),
                It.IsAny<Action<string>?>()))
            .ReturnsAsync(resticOutput);

        // Act
        var result = await _service.BrowseBackup("test123", "/", "./repositories/device/share");

        // Assert
        var files = result.ToList();
        Assert.Single(files);
        Assert.Equal(5368709120, files[0].Size); // 5 GB
    }
}
