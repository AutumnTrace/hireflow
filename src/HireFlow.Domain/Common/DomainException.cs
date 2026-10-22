namespace HireFlow.Domain.Common;

/// <summary>
/// Base type for every business-rule violation raised by the domain.
/// The API layer maps these to typed GraphQL errors instead of generic failures.
/// </summary>
public abstract class DomainException(string message) : Exception(message);

/// <summary>An input value broke an invariant (empty name, inverted salary range, ...).</summary>
public sealed class DomainValidationException(string field, string message) : DomainException(message)
{
    public string Field { get; } = field;
}

/// <summary>A referenced aggregate does not exist.</summary>
public sealed class EntityNotFoundException(string entity, Guid id)
    : DomainException($"{entity} '{id}' was not found.")
{
    public string Entity { get; } = entity;

    public Guid EntityId { get; } = id;
}
