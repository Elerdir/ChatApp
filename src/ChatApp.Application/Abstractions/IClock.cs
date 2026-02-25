namespace ChatApp.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}