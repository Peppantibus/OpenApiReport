using System.Text.Json;

namespace OpenApiReport.Core;

public static class OpenApiParser
{
    private static readonly HashSet<string> HttpMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "get", "post", "put", "delete", "patch", "options", "head", "trace"
    };

    public static OpenApiSpec ParseFile(string path)
    {
        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);
        return Parse(document.RootElement);
    }

    public static OpenApiSpec Parse(JsonElement root)
    {
        var spec = new OpenApiSpec();

        if (root.TryGetProperty("paths", out var pathsElement) && pathsElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var pathProperty in pathsElement.EnumerateObject())
            {
                var pathItem = new PathItem();
                if (pathProperty.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (var operationProperty in pathProperty.Value.EnumerateObject())
                {
                    if (!HttpMethods.Contains(operationProperty.Name))
                    {
                        continue;
                    }

                    if (operationProperty.Value.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var operation = ParseOperation(pathProperty.Name, operationProperty.Name, operationProperty.Value);
                    pathItem.Operations[operation.Method] = operation;
                }

                if (pathItem.Operations.Count > 0)
                {
                    spec.Paths[pathProperty.Name] = pathItem;
                }
            }
        }

        if (root.TryGetProperty("components", out var componentsElement)
            && componentsElement.ValueKind == JsonValueKind.Object
            && componentsElement.TryGetProperty("schemas", out var schemasElement)
            && schemasElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var schemaProperty in schemasElement.EnumerateObject())
            {
                if (schemaProperty.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                spec.Schemas[schemaProperty.Name] = ParseSchema(schemaProperty.Value);
            }
        }

        return spec;
    }

    private static Operation ParseOperation(string path, string method, JsonElement element)
    {
        var operation = new Operation
        {
            Method = method.ToLowerInvariant(),
            Path = path,
            OperationId = element.TryGetProperty("operationId", out var operationIdElement) && operationIdElement.ValueKind == JsonValueKind.String
                ? operationIdElement.GetString()
                : null
        };

        if (element.TryGetProperty("tags", out var tagsElement) && tagsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var tag in tagsElement.EnumerateArray())
            {
                if (tag.ValueKind == JsonValueKind.String)
                {
                    operation.Tags.Add(tag.GetString() ?? string.Empty);
                }
            }
        }

        if (element.TryGetProperty("parameters", out var parametersElement) && parametersElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var parameterElement in parametersElement.EnumerateArray())
            {
                if (parameterElement.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var parameter = ParseParameter(parameterElement);
                if (parameter is not null)
                {
                    operation.Parameters.Add(parameter);
                }
            }
        }

        if (element.TryGetProperty("requestBody", out var requestBodyElement) && requestBodyElement.ValueKind == JsonValueKind.Object)
        {
            operation.RequestBody = ParseRequestBody(requestBodyElement);
        }

        if (element.TryGetProperty("responses", out var responsesElement) && responsesElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var responseProperty in responsesElement.EnumerateObject())
            {
                if (responseProperty.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var response = ParseResponse(responseProperty.Name, responseProperty.Value);
                operation.Responses[response.StatusCode] = response;
            }
        }

        return operation;
    }

    private static Parameter? ParseParameter(JsonElement element)
    {
        if (!element.TryGetProperty("name", out var nameElement) || nameElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        if (!element.TryGetProperty("in", out var inElement) || inElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var required = element.TryGetProperty("required", out var requiredElement) && requiredElement.ValueKind == JsonValueKind.True;
        Schema? schema = null;
        if (element.TryGetProperty("schema", out var schemaElement) && schemaElement.ValueKind == JsonValueKind.Object)
        {
            schema = ParseSchema(schemaElement);
        }

        return new Parameter
        {
            Name = nameElement.GetString() ?? string.Empty,
            In = inElement.GetString() ?? string.Empty,
            Required = required,
            Schema = schema
        };
    }

    private static RequestBody ParseRequestBody(JsonElement element)
    {
        var requestBody = new RequestBody
        {
            Required = element.TryGetProperty("required", out var requiredElement) && requiredElement.ValueKind == JsonValueKind.True
        };

        if (element.TryGetProperty("content", out var contentElement) && contentElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var contentProperty in contentElement.EnumerateObject())
            {
                if (contentProperty.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var mediaType = ParseMediaType(contentProperty.Value);
                requestBody.Content[contentProperty.Name] = mediaType;
            }
        }

        return requestBody;
    }

    private static Response ParseResponse(string statusCode, JsonElement element)
    {
        var response = new Response
        {
            StatusCode = statusCode
        };

        if (element.TryGetProperty("content", out var contentElement) && contentElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var contentProperty in contentElement.EnumerateObject())
            {
                if (contentProperty.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                response.Content[contentProperty.Name] = ParseMediaType(contentProperty.Value);
            }
        }

        return response;
    }

    private static MediaType ParseMediaType(JsonElement element)
    {
        Schema? schema = null;
        if (element.TryGetProperty("schema", out var schemaElement) && schemaElement.ValueKind == JsonValueKind.Object)
        {
            schema = ParseSchema(schemaElement);
        }

        return new MediaType { Schema = schema };
    }

    private static Schema ParseSchema(JsonElement element)
    {
        if (element.TryGetProperty("$ref", out var refElement) && refElement.ValueKind == JsonValueKind.String)
        {
            return new Schema { Ref = refElement.GetString() };
        }

        var schema = new Schema
        {
            Type = element.TryGetProperty("type", out var typeElement) && typeElement.ValueKind == JsonValueKind.String
                ? typeElement.GetString()
                : null,
            Format = element.TryGetProperty("format", out var formatElement) && formatElement.ValueKind == JsonValueKind.String
                ? formatElement.GetString()
                : null
        };

        if (element.TryGetProperty("enum", out var enumElement) && enumElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var enumValue in enumElement.EnumerateArray())
            {
                if (enumValue.ValueKind == JsonValueKind.String)
                {
                    schema.Enum.Add(enumValue.GetString() ?? string.Empty);
                }
                else
                {
                    schema.Enum.Add(enumValue.ToString());
                }
            }
        }

        if (element.TryGetProperty("required", out var requiredElement) && requiredElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var requiredValue in requiredElement.EnumerateArray())
            {
                if (requiredValue.ValueKind == JsonValueKind.String)
                {
                    schema.Required.Add(requiredValue.GetString() ?? string.Empty);
                }
            }
        }

        if (element.TryGetProperty("properties", out var propertiesElement) && propertiesElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in propertiesElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Object)
                {
                    schema.Properties[property.Name] = ParseSchema(property.Value);
                }
            }
        }

        return schema;
    }
}
