namespace Domain.Models.Shared.Result
{
    public sealed record Error(string Code, string Message, ErrorType Type, Exception? Exception = null);
}
