using BackupChrono.Infrastructure.Restic;
using Xunit;

namespace BackupChrono.UnitTests.Infrastructure.Restic;

public class ResticClientValidationTests
{
    [Fact]
    public void Constructor_ThrowsException_WhenPasswordIsEmpty()
    {
        // Arrange & Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new ResticClient("restic", "/repo", ""));
        
        Assert.Equal("password", exception.ParamName);
        Assert.Contains("cannot be empty", exception.Message);
        Assert.Contains("Restic requires a non-empty password", exception.Message);
    }

    [Fact]
    public void Constructor_ThrowsException_WhenPasswordIsNull()
    {
        // Arrange & Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new ResticClient("restic", "/repo", null!));
        
        Assert.Equal("password", exception.ParamName);
    }

    [Fact]
    public void Constructor_ThrowsException_WhenPasswordIsWhitespace()
    {
        // Arrange & Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new ResticClient("restic", "/repo", "   "));
        
        Assert.Equal("password", exception.ParamName);
    }

    [Fact]
    public void Constructor_ThrowsException_WhenResticPathIsEmpty()
    {
        // Arrange & Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new ResticClient("", "/repo", "password123"));
        
        Assert.Equal("resticPath", exception.ParamName);
        Assert.Contains("Restic binary path cannot be empty", exception.Message);
    }

    [Fact]
    public void Constructor_ThrowsException_WhenRepositoryPathIsEmpty()
    {
        // Arrange & Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new ResticClient("restic", "", "password123"));
        
        Assert.Equal("repositoryPath", exception.ParamName);
        Assert.Contains("Repository path cannot be empty", exception.Message);
    }

    [Fact]
    public void Constructor_Succeeds_WhenAllParametersAreValid()
    {
        // Arrange & Act
        var client = new ResticClient("restic", "/repo", "validPassword123");
        
        // Assert
        Assert.NotNull(client);
    }

    [Theory]
    [InlineData("p")]
    [InlineData("password")]
    [InlineData("complex!P@ssw0rd#123")]
    [InlineData("密码🔒")]
    public void Constructor_Succeeds_WithVariousValidPasswords(string password)
    {
        // Arrange & Act
        var client = new ResticClient("restic", "/repo", password);
        
        // Assert
        Assert.NotNull(client);
    }
}
