namespace ChatApp.Api.Common;

public readonly struct Result
{
    public bool IsSuccess { get; }
    public AppError? Error { get; }

    private Result(bool ok, AppError? err)
    {
        IsSuccess = ok;
        Error = err;
    }

    public static Result Ok() => new(true, null);
    public static Result Fail(AppError error) => new(false, error);
}

public readonly struct Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public AppError? Error { get; }

    private Result(bool ok, T? value, AppError? err)
    {
        IsSuccess = ok;
        Value = value;
        Error = err;
    }

    public static Result<T> Ok(T value) => new(true, value, null);
    public static Result<T> Fail(AppError error) => new(false, default, error);
}