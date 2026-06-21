namespace TripNest.Core.Exceptions;

/// <summary>
/// Base type for expected, business-rule failures. The middleware maps each to an HTTP status,
/// so services can signal intent (not found / invalid / conflict / forbidden) without controllers
/// hand-mapping every exception to a status code.
/// </summary>
public abstract class DomainException : Exception
{
    public abstract int StatusCode { get; }

    protected DomainException(string message) : base(message) { }
}

/// <summary>404 — the requested resource does not exist.</summary>
public sealed class NotFoundException : DomainException
{
    public override int StatusCode => 404;
    public NotFoundException(string resource) : base($"{resource} not found") { }
}

/// <summary>400 — the request is structurally valid but breaks a business rule.</summary>
public sealed class ValidationException : DomainException
{
    public override int StatusCode => 400;
    public ValidationException(string message) : base(message) { }
}

/// <summary>409 — the request conflicts with current state (e.g. overlapping booking, duplicate).</summary>
public sealed class ConflictException : DomainException
{
    public override int StatusCode => 409;
    public ConflictException(string message) : base(message) { }
}

/// <summary>403 — the caller is authenticated but not allowed to perform this action.</summary>
public sealed class ForbiddenException : DomainException
{
    public override int StatusCode => 403;
    public ForbiddenException(string message) : base(message) { }
}

/// <summary>429 — the caller is being throttled (e.g. OTP resend cooldown) and should retry later.</summary>
public sealed class TooManyRequestsException : DomainException
{
    public override int StatusCode => 429;
    public TooManyRequestsException(string message) : base(message) { }
}
