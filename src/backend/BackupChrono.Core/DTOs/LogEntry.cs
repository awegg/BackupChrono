namespace BackupChrono.Core.DTOs;

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
    public string? SourceContext { get; set; }
}

public class LogQueryParameters
{
    public string? Level { get; set; }
    public string? Search { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int Limit { get; set; } = 100;
}
