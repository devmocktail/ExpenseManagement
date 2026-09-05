namespace ExpenseManagement.Domain.Exceptions;

/// <summary>
/// Base for errors that represent a broken business rule rather than a bug.
/// The API's exception middleware maps these to 4xx responses; anything that
/// is not a DomainException becomes a 500 and is logged with a stack trace.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }

    /// <summary>Machine-readable code the mobile client can branch on.</summary>
    public abstract string ErrorCode { get; }
}

/// <summary>The requested row does not exist, or does not belong to the caller.</summary>
public sealed class NotFoundException : DomainException
{
    public NotFoundException(string entity, object key)
        : base($"{entity} '{key}' was not found.") { }

    public NotFoundException(string message) : base(message) { }

    public override string ErrorCode => "not_found";
}

/// <summary>The request conflicts with current state, e.g. a duplicate name.</summary>
public sealed class ConflictException : DomainException
{
    public ConflictException(string message) : base(message) { }

    public override string ErrorCode => "conflict";
}

/// <summary>The caller is authenticated but not allowed to touch this resource.</summary>
public sealed class ForbiddenException : DomainException
{
    public ForbiddenException(string message = "You do not have access to this resource.")
        : base(message) { }

    public override string ErrorCode => "forbidden";
}

/// <summary>A business rule rejected an otherwise well-formed request.</summary>
public sealed class BusinessRuleException : DomainException
{
    public BusinessRuleException(string message, string errorCode = "business_rule_violation")
        : base(message) => ErrorCode = errorCode;

    public override string ErrorCode { get; }
}
