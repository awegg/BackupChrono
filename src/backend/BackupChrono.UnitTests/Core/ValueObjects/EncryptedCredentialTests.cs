using BackupChrono.Core.ValueObjects;
using Xunit;

namespace BackupChrono.UnitTests.Core.ValueObjects;

public class EncryptedCredentialTests
{
    [Fact]
    public void Constructor_EncryptsPlaintextValue()
    {
        // Arrange
        const string plaintext = "mySecretPassword123!";
        
        // Act
        var credential = new EncryptedCredential(plaintext);
        
        // Assert
        Assert.NotEmpty(credential.EncryptedValue);
        Assert.NotEqual(plaintext, credential.EncryptedValue);
        Assert.Equal(plaintext, credential.GetPlaintext());
    }

    [Fact]
    public void GetPlaintext_ReturnsOriginalValue()
    {
        // Arrange
        const string plaintext = "anotherPassword456@";
        var credential = new EncryptedCredential(plaintext);
        
        // Act
        var decrypted = credential.GetPlaintext();
        
        // Assert
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void SetPlaintext_UpdatesEncryptedValue()
    {
        // Arrange
        var credential = new EncryptedCredential("initial");
        var originalEncrypted = credential.EncryptedValue;
        
        // Act
        credential.SetPlaintext("updated");
        
        // Assert
        Assert.NotEqual(originalEncrypted, credential.EncryptedValue);
        Assert.Equal("updated", credential.GetPlaintext());
    }

    [Fact]
    public void SetEncryptedValue_ClearsCachedPlaintext()
    {
        // Arrange
        var credential1 = new EncryptedCredential("password123");
        var encryptedValue = credential1.EncryptedValue;
        
        var credential2 = new EncryptedCredential();
        
        // Act
        credential2.SetEncryptedValue(encryptedValue);
        
        // Assert
        Assert.Equal("password123", credential2.GetPlaintext());
    }

    [Fact]
    public void ToString_HidesPlaintextValue()
    {
        // Arrange
        var credential = new EncryptedCredential("secretPassword");
        
        // Act
        var stringRepresentation = credential.ToString();
        
        // Assert
        Assert.DoesNotContain("secretPassword", stringRepresentation);
        Assert.Equal("***ENCRYPTED***", stringRepresentation);
    }

    [Fact]
    public void EncryptedValue_DifferentEachTime()
    {
        // Arrange & Act
        var credential1 = new EncryptedCredential("same");
        var credential2 = new EncryptedCredential("same");
        
        // Assert
        // Due to random nonce, encrypted values should be different even for same plaintext
        Assert.NotEqual(credential1.EncryptedValue, credential2.EncryptedValue);
        Assert.Equal(credential1.GetPlaintext(), credential2.GetPlaintext());
    }

    [Fact]
    public void EmptyString_HandledCorrectly()
    {
        // Arrange & Act
        var credential = new EncryptedCredential(string.Empty);
        
        // Assert
        Assert.Equal(string.Empty, credential.GetPlaintext());
        Assert.Equal(string.Empty, credential.EncryptedValue);
    }

    [Fact]
    public void SpecialCharacters_PreservedAfterEncryption()
    {
        // Arrange
        const string complex = "p@$$w0rd!#%^&*(){}[]<>?/\\|~`+-=_";
        
        // Act
        var credential = new EncryptedCredential(complex);
        
        // Assert
        Assert.Equal(complex, credential.GetPlaintext());
    }

    [Fact]
    public void Unicode_PreservedAfterEncryption()
    {
        // Arrange
        const string unicode = "密码🔒κωδικός пароль";
        
        // Act
        var credential = new EncryptedCredential(unicode);
        
        // Assert
        Assert.Equal(unicode, credential.GetPlaintext());
    }
}
