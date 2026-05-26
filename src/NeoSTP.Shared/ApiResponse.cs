namespace NeoSTP.Shared;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public List<string> Errors { get; set; } = new();
    public string? TraceId { get; set; }

    public static ApiResponse<T> Ok(T data, string? message = null, string? traceId = null)
        => new() { Success = true, Data = data, Message = message, TraceId = traceId };

    public static ApiResponse<T> Fail(string message, IEnumerable<string>? errors = null, string? traceId = null)
        => new()
        {
            Success = false,
            Message = message,
            Errors = errors?.ToList() ?? new List<string>(),
            TraceId = traceId
        };
}

public class ApiResponse : ApiResponse<object>
{
    public static ApiResponse Ok(string? message = null, string? traceId = null)
        => new() { Success = true, Message = message, TraceId = traceId };

    public static new ApiResponse Fail(string message, IEnumerable<string>? errors = null, string? traceId = null)
        => new()
        {
            Success = false,
            Message = message,
            Errors = errors?.ToList() ?? new List<string>(),
            TraceId = traceId
        };
}
