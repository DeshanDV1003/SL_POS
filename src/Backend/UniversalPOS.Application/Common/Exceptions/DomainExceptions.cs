namespace UniversalPOS.Application.Common.Exceptions;

/// <summary>Base for exceptions the API's exception middleware maps to a specific, safe HTTP response.</summary>
public abstract class AppException : Exception
{
    protected AppException(string message) : base(message) { }
}

public class InvalidCredentialsException : AppException
{
    public InvalidCredentialsException() : base("Invalid username or password.") { }
}

public class AccountLockedOutException : AppException
{
    public AccountLockedOutException(DateTime untilUtc) : base($"Account is locked until {untilUtc:u}.") { }
}

public class NotFoundException : AppException
{
    public NotFoundException(string entityName, object key) : base($"{entityName} ({key}) was not found.") { }
}

public class ForbiddenException : AppException
{
    public ForbiddenException(string message) : base(message) { }
}

public class ConflictException : AppException
{
    public ConflictException(string message) : base(message) { }
}

public class ValidationFailedException : AppException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationFailedException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }
}
