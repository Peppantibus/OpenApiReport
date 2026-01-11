namespace OpenApiReport.Core;

public sealed class OpenApiSpec
{
    public Dictionary<string, PathItem> Paths { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, Schema> Schemas { get; } = new(StringComparer.Ordinal);
}

public sealed class PathItem
{
    public Dictionary<string, Operation> Operations { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class Operation
{
    public required string Method { get; init; }
    public required string Path { get; init; }
    public string? OperationId { get; init; }
    public List<string> Tags { get; } = new();
    public List<Parameter> Parameters { get; } = new();
    public RequestBody? RequestBody { get; init; }
    public Dictionary<string, Response> Responses { get; } = new(StringComparer.Ordinal);
}

public sealed class Parameter
{
    public required string Name { get; init; }
    public required string In { get; init; }
    public bool Required { get; init; }
    public Schema? Schema { get; init; }
}

public sealed class RequestBody
{
    public bool Required { get; init; }
    public Dictionary<string, MediaType> Content { get; } = new(StringComparer.Ordinal);
}

public sealed class Response
{
    public required string StatusCode { get; init; }
    public Dictionary<string, MediaType> Content { get; } = new(StringComparer.Ordinal);
}

public sealed class MediaType
{
    public Schema? Schema { get; init; }
}

public sealed class Schema
{
    public string? Ref { get; init; }
    public string? Type { get; init; }
    public string? Format { get; init; }
    public Dictionary<string, Schema> Properties { get; } = new(StringComparer.Ordinal);
    public HashSet<string> Required { get; } = new(StringComparer.Ordinal);
    public List<string> Enum { get; } = new();
}
