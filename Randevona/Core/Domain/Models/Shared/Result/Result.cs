namespace Domain.Models.Shared.Result
{
    public sealed class Result<T>
    {
        public bool IsSuccess { get; }
        public T? Value { get; }
        public Error? Error { get; }

        private Result(T value) { IsSuccess = true; Value = value; }
        private Result(Error error) { IsSuccess = false; Error = error; }

        public static implicit operator Result<T>(T value) => new(value);
        public static implicit operator Result<T>(Error error) => new(error);
    }

    public sealed class Result
    {
        public bool IsSuccess { get; }
        public Error? Error { get; }

        private Result(bool isSuccess, Error? error)
        {
            IsSuccess = isSuccess;
            Error = error;
        }

        public static Result Success() => new(true, null);
        public static Result Fail(Error error) => new(false, error);

        public static implicit operator Result(Error error) => new(false, error);
    }
}

