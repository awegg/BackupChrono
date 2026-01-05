namespace BackupChrono.Infrastructure.Restic;

/// <summary>
/// Interface for executing restic commands.
/// </summary>
public interface IResticClient
{
    string RepositoryPath { get; }

    Task<string> ExecuteCommand(
        string[] args,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null,
        Action<string>? onOutputLine = null,
        string? repositoryPathOverride = null,
        Action<string>? onErrorLine = null);

    Task<byte[]> ExecuteCommandBinary(
        string[] args,
        CancellationToken cancellationToken = default,
        string? repositoryPathOverride = null);

    Task<Stream> ExecuteCommandStream(
        string[] args,
        CancellationToken cancellationToken = default,
        string? repositoryPathOverride = null);

    IAsyncEnumerable<T> ExecuteCommandJsonStream<T>(
        string[] args,
        CancellationToken cancellationToken = default,
        string? repositoryPathOverride = null);

    Task<T?> ExecuteCommandJson<T>(
        string[] args,
        CancellationToken cancellationToken = default);
}
