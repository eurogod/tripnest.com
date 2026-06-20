namespace TripNest.Core.Response;

public class ApiResponse<T> where T : class
{
    public string Message { get; set; }

    public int StatusCode { get; set; }

    public T? Data { get; set; }

    public bool Success => StatusCode is >= 200 and < 300;

    public ApiResponse(string message, int statusCode, T? data)
    {
        Message = message;
        StatusCode = statusCode;
        Data = data;
    }

    public static ApiResponse<T> Ok(string message, T? data = null)
        => new(message, 200, data);

    public static ApiResponse<T> Created(string type, T data)
        => new($"{type} is created", 201, data);

    public static ApiResponse<T> NotFound(string type)
        => new($"{type} not found", 404, null);

    public static ApiResponse<T> UnAuthorized()
        => new("UnAuthorized", 401, null);

    public static ApiResponse<T> Forbidden(string message = "Forbidden")
        => new(message, 403, null);

    public static ApiResponse<T> FailedDependency()
        => new("Failed to connect please try again later", 424, null);

    public static ApiResponse<T> Conflict(string type)
        => new($"{type} already exist", 409, null);

    public static ApiResponse<T> BadRequest(string message)
        => new(message, 400, null);

    public static ApiResponse<T> TooManyRequests(string message)
        => new(message, 429, null);

    public static ApiResponse<T> InternalServerError(string message = "An error occurred")
        => new(message, 500, null);
}
