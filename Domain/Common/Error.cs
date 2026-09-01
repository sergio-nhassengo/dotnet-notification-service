using System.Collections.Generic;

namespace Domain.Common;

public enum ErrorType
{
    Failure,
    Validation,
    NotFound,
    Conflict,
    Unauthorized
}

public record Error(string Code, string Message, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error EntityNotFound(string entityName, object key) =>
        new($"{entityName}.NotFound", $"Entity \"{entityName}\" ({key}) was not found.", ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
}

public sealed record ValidationError(Error[] Errors)
    : Error("Validation.General", "One or more validation errors occurred.", ErrorType.Validation)
{
    public static ValidationError FromErrors(IReadOnlyCollection<Error> errors) => new([..errors]);
}
