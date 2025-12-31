using BackupChrono.Core.ValueObjects;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace BackupChrono.Infrastructure.Git;

/// <summary>
/// YamlDotNet converter that automatically encrypts/decrypts EncryptedCredential values
/// when serializing/deserializing YAML files.
/// </summary>
public class YamlEncryptedCredentialConverter : IYamlTypeConverter
{
    public bool Accepts(Type type)
    {
        return type == typeof(EncryptedCredential);
    }

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        var scalar = parser.Consume<Scalar>();
        var encryptedValue = scalar.Value;
        
        var credential = new EncryptedCredential();
        credential.SetEncryptedValue(encryptedValue);
        
        return credential;
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        if (value is EncryptedCredential credential)
        {
            // Write the encrypted value to YAML
            emitter.Emit(new Scalar(credential.EncryptedValue));
        }
        else
        {
            emitter.Emit(new Scalar(string.Empty));
        }
    }
}
