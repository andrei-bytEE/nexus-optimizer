namespace NexusOptimizer.Models;

/// <summary>Explicit success/failure result. Failures are never reported as success.</summary>
public sealed class OperationResult
{
    public bool Success { get; }
    public string Message { get; }

    private OperationResult(bool success, string message)
    {
        Success = success;
        Message = message;
    }

    public static OperationResult Ok(string message) => new(true, message);
    public static OperationResult Fail(string message) => new(false, message);
}