namespace AuthzLab;

/// <summary>A principal or a resource: a type, an id and attributes.</summary>
public sealed record Entity(string Type, string Id, IReadOnlyDictionary<string, object?> Attributes)
{
    /// <summary>Reads an attribute. A missing one is an evaluation error, never a silent default.</summary>
    public T Get<T>(string name) =>
        Attributes.TryGetValue(name, out var value)
            ? (T)value!
            : throw new MissingAttributeException($"{Type}::{Id} does not have the attribute '{name}'");
}

public sealed class MissingAttributeException(string message) : Exception(message);

/// <summary>authorization = f(subject, action, resource, context).</summary>
public sealed record Request(Entity Subject, string Action, Entity Resource, IReadOnlyDictionary<string, object?>? Context = null);

public enum Effect
{
    Permit,
    Forbid,
}

/// <summary>One rule: when the condition holds, the effect applies.</summary>
public sealed record Policy(string Id, Effect Effect, Func<Request, bool> When);

/// <summary>The answer and why: the policies that decided it and the evaluation errors met on the way.</summary>
public sealed record Decision(bool Allowed, IReadOnlyList<string> Determining, IReadOnlyList<string> Errors);
