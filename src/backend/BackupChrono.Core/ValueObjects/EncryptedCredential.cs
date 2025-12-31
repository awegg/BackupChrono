using System.Security.Cryptography;
using System.Text;

namespace BackupChrono.Core.ValueObjects;

/// <summary>
/// Represents an encrypted credential that is automatically encrypted when serialized
/// and decrypted when deserialized.
/// </summary>
public class EncryptedCredential
{
    private string _plaintext = string.Empty;
    
    /// <summary>
    /// The encrypted value in Base64 format (for serialization).
    /// </summary>
    public string EncryptedValue { get; private set; } = string.Empty;

    /// <summary>
    /// Creates an empty encrypted credential.
    /// </summary>
    public EncryptedCredential()
    {
    }

    /// <summary>
    /// Creates an encrypted credential from a plaintext value.
    /// </summary>
    public EncryptedCredential(string plaintext)
    {
        SetPlaintext(plaintext);
    }

    /// <summary>
    /// Gets the plaintext value (decrypts on demand).
    /// </summary>
    public string GetPlaintext()
    {
        if (string.IsNullOrEmpty(EncryptedValue))
            return string.Empty;

        if (!string.IsNullOrEmpty(_plaintext))
            return _plaintext;

        _plaintext = Decrypt(EncryptedValue);
        return _plaintext;
    }

    /// <summary>
    /// Sets the plaintext value (encrypts immediately).
    /// </summary>
    public void SetPlaintext(string plaintext)
    {
        _plaintext = plaintext ?? string.Empty;
        EncryptedValue = Encrypt(_plaintext);
    }

    /// <summary>
    /// Sets the encrypted value directly (used during deserialization).
    /// </summary>
    public void SetEncryptedValue(string encryptedValue)
    {
        EncryptedValue = encryptedValue ?? string.Empty;
        _plaintext = string.Empty; // Clear cache
    }

    /// <summary>
    /// Encrypts a plaintext string using AES-256-GCM.
    /// </summary>
    private static string Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            return string.Empty;

        // Get encryption key from environment or use default (INSECURE - should be from configuration)
        var key = GetEncryptionKey();
        
        var tagSize = AesGcm.TagByteSizes.MaxSize;
        using var aes = new AesGcm(key, tagSize);
        
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
        RandomNumberGenerator.Fill(nonce);
        
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plainBytes.Length];
        var tag = new byte[tagSize];
        
        aes.Encrypt(nonce, plainBytes, ciphertext, tag);
        
        // Combine: nonce + tag + ciphertext
        var result = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, result, nonce.Length + tag.Length, ciphertext.Length);
        
        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// Decrypts an encrypted string using AES-256-GCM.
    /// </summary>
    private static string Decrypt(string encryptedValue)
    {
        if (string.IsNullOrEmpty(encryptedValue))
            return string.Empty;

        try
        {
            var key = GetEncryptionKey();
            var tagSize = AesGcm.TagByteSizes.MaxSize;
            using var aes = new AesGcm(key, tagSize);
            
            var combined = Convert.FromBase64String(encryptedValue);
            
            var nonceSize = AesGcm.NonceByteSizes.MaxSize;
            
            var nonce = new byte[nonceSize];
            var tag = new byte[tagSize];
            var ciphertext = new byte[combined.Length - nonceSize - tagSize];
            
            Buffer.BlockCopy(combined, 0, nonce, 0, nonceSize);
            Buffer.BlockCopy(combined, nonceSize, tag, 0, tagSize);
            Buffer.BlockCopy(combined, nonceSize + tagSize, ciphertext, 0, ciphertext.Length);
            
            var plainBytes = new byte[ciphertext.Length];
            aes.Decrypt(nonce, ciphertext, tag, plainBytes);
            
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException)
        {
            throw new InvalidOperationException("Failed to decrypt credential. Encryption key may have changed.");
        }
    }

    /// <summary>
    /// Gets the encryption key from environment variable or configuration.
    /// WARNING: This is a placeholder implementation. In production, use a proper key management solution.
    /// </summary>
    private static byte[] GetEncryptionKey()
    {
        // Try to get key from environment variable
        var keyBase64 = Environment.GetEnvironmentVariable("BACKUPCHRONO_ENCRYPTION_KEY");
        
        if (!string.IsNullOrEmpty(keyBase64))
        {
            try
            {
                var key = Convert.FromBase64String(keyBase64);
                if (key.Length == 32) // AES-256 requires 32 bytes
                    return key;
            }
            catch
            {
                // Fall through to default
            }
        }
        
        // INSECURE DEFAULT - Only for development/testing
        // In production, this should fail if no key is configured
        var defaultKey = "DEVELOPMENT_KEY_CHANGE_IN_PRODUCTION_32BYTES!";
        return SHA256.HashData(Encoding.UTF8.GetBytes(defaultKey));
    }

    public override string ToString()
    {
        return "***ENCRYPTED***";
    }
}
