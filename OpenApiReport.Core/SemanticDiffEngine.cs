namespace OpenApiReport.Core;

public sealed class SemanticDiffEngine
{
    private static readonly ChangeSeverity[] SeverityOrder =
    {
        ChangeSeverity.Breaking,
        ChangeSeverity.Risky,
        ChangeSeverity.Additive,
        ChangeSeverity.Cosmetic
    };

    public IReadOnlyList<ChangeRecord> Diff(OpenApiSpec oldSpec, OpenApiSpec newSpec)
    {
        var changes = new List<ChangeRecord>();

        ComparePaths(oldSpec, newSpec, changes);
        CompareComponentSchemas(oldSpec, newSpec, changes);

        return changes
            .OrderBy(change => Array.IndexOf(SeverityOrder, change.Severity))
            .ThenBy(change => change.Tag, StringComparer.Ordinal)
            .ThenBy(change => change.Endpoint, StringComparer.Ordinal)
            .ThenBy(change => change.Title, StringComparer.Ordinal)
            .ToList();
    }

    private static void ComparePaths(OpenApiSpec oldSpec, OpenApiSpec newSpec, List<ChangeRecord> changes)
    {
        foreach (var oldPath in oldSpec.Paths)
        {
            if (!newSpec.Paths.TryGetValue(oldPath.Key, out var newPathItem))
            {
                foreach (var removedOperation in oldPath.Value.Operations.Values)
                {
                    changes.Add(BuildOperationChange(
                        removedOperation,
                        ChangeSeverity.Breaking,
                        "Operation removed",
                        $"paths.{oldPath.Key}.{removedOperation.Method}",
                        "present",
                        "missing",
                        "This endpoint is no longer available for consumers.",
                        "Update clients or remove usage of this endpoint."));
                }

                continue;
            }

            CompareOperations(oldPath.Key, oldPath.Value, newPathItem, changes);
        }

        foreach (var newPath in newSpec.Paths)
        {
            if (oldSpec.Paths.ContainsKey(newPath.Key))
            {
                continue;
            }

            foreach (var addedOperation in newPath.Value.Operations.Values)
            {
                changes.Add(BuildOperationChange(
                    addedOperation,
                    ChangeSeverity.Additive,
                    "Operation added",
                    $"paths.{newPath.Key}.{addedOperation.Method}",
                    "missing",
                    "present",
                    "A new endpoint is available for consumers.",
                    "Document and monitor adoption for this endpoint."));
            }
        }
    }

    private static void CompareOperations(string path, PathItem oldPathItem, PathItem newPathItem, List<ChangeRecord> changes)
    {
        foreach (var oldOperation in oldPathItem.Operations)
        {
            if (!newPathItem.Operations.TryGetValue(oldOperation.Key, out var newOperation))
            {
                changes.Add(BuildOperationChange(
                    oldOperation.Value,
                    ChangeSeverity.Breaking,
                    "Operation removed",
                    $"paths.{path}.{oldOperation.Key}",
                    "present",
                    "missing",
                    "This endpoint is no longer available for consumers.",
                    "Update clients or remove usage of this endpoint."));

                continue;
            }

            CompareParameters(path, oldOperation.Value, newOperation, changes);
            CompareRequestBody(path, oldOperation.Value, newOperation, changes);
            CompareResponses(path, oldOperation.Value, newOperation, changes);
        }

        foreach (var newOperation in newPathItem.Operations)
        {
            if (oldPathItem.Operations.ContainsKey(newOperation.Key))
            {
                continue;
            }

            changes.Add(BuildOperationChange(
                newOperation.Value,
                ChangeSeverity.Additive,
                "Operation added",
                $"paths.{path}.{newOperation.Key}",
                "missing",
                "present",
                "A new endpoint is available for consumers.",
                "Document and monitor adoption for this endpoint."));
        }
    }

    private static void CompareParameters(string path, Operation oldOperation, Operation newOperation, List<ChangeRecord> changes)
    {
        var oldParams = oldOperation.Parameters.ToDictionary(param => ParamKey(param), StringComparer.OrdinalIgnoreCase);
        var newParams = newOperation.Parameters.ToDictionary(param => ParamKey(param), StringComparer.OrdinalIgnoreCase);

        foreach (var oldParam in oldParams)
        {
            if (!newParams.TryGetValue(oldParam.Key, out var newParam))
            {
                changes.Add(BuildOperationChange(
                    oldOperation,
                    ChangeSeverity.Breaking,
                    "Parameter removed",
                    $"paths.{path}.{oldOperation.Method}.parameters[{oldParam.Value.Name}]",
                    oldParam.Value.Name,
                    "missing",
                    "Clients that relied on this parameter can no longer send it.",
                    "Remove the parameter from client requests."));

                continue;
            }

            if (oldParam.Value.Required != newParam.Required)
            {
                if (!oldParam.Value.Required && newParam.Required)
                {
                    changes.Add(BuildOperationChange(
                        newOperation,
                        ChangeSeverity.Breaking,
                        "Parameter became required",
                        $"paths.{path}.{newOperation.Method}.parameters[{newParam.Name}].required",
                        "false",
                        "true",
                        "Clients must now supply this parameter for requests to succeed.",
                        "Ensure clients send the parameter before deployment."));
                }
                else
                {
                    // Relaxing required parameters is non-breaking and considered additive.
                    changes.Add(BuildOperationChange(
                        newOperation,
                        ChangeSeverity.Additive,
                        "Parameter requirement relaxed",
                        $"paths.{path}.{newOperation.Method}.parameters[{newParam.Name}].required",
                        "true",
                        "false",
                        "Clients may omit this parameter without failing validation.",
                        "Optionally update documentation to reflect optional usage."));
                }
            }

            if (SchemaTypeChanged(oldParam.Value.Schema, newParam.Schema))
            {
                changes.Add(BuildOperationChange(
                    newOperation,
                    ChangeSeverity.Breaking,
                    "Parameter schema changed",
                    $"paths.{path}.{newOperation.Method}.parameters[{newParam.Name}].schema",
                    DescribeSchema(oldParam.Value.Schema),
                    DescribeSchema(newParam.Schema),
                    "The parameter value type is incompatible with previous clients.",
                    "Update clients to match the new parameter type."));
            }
        }

        foreach (var newParam in newParams)
        {
            if (oldParams.ContainsKey(newParam.Key))
            {
                continue;
            }

            if (newParam.Value.Required)
            {
                changes.Add(BuildOperationChange(
                    newOperation,
                    ChangeSeverity.Risky,
                    "Required parameter added",
                    $"paths.{path}.{newOperation.Method}.parameters[{newParam.Value.Name}].required",
                    "missing",
                    "true",
                    "Clients must send this new parameter or their requests will fail.",
                    "Communicate the new required parameter before rollout."));
            }
            else
            {
                changes.Add(BuildOperationChange(
                    newOperation,
                    ChangeSeverity.Additive,
                    "Optional parameter added",
                    $"paths.{path}.{newOperation.Method}.parameters[{newParam.Value.Name}]",
                    "missing",
                    "present",
                    "Clients can optionally send a new parameter.",
                    "Document the new optional parameter for consumers."));
            }
        }
    }

    private static void CompareRequestBody(string path, Operation oldOperation, Operation newOperation, List<ChangeRecord> changes)
    {
        if (oldOperation.RequestBody is null && newOperation.RequestBody is null)
        {
            return;
        }

        if (oldOperation.RequestBody is null && newOperation.RequestBody is not null)
        {
            changes.Add(BuildOperationChange(
                newOperation,
                ChangeSeverity.Additive,
                "Request body added",
                $"paths.{path}.{newOperation.Method}.requestBody",
                "missing",
                "present",
                "Clients can now send a request body.",
                "Document the new request body for consumers."));
            return;
        }

        if (oldOperation.RequestBody is not null && newOperation.RequestBody is null)
        {
            changes.Add(BuildOperationChange(
                oldOperation,
                ChangeSeverity.Breaking,
                "Request body removed",
                $"paths.{path}.{oldOperation.Method}.requestBody",
                "present",
                "missing",
                "Clients that sent request bodies will no longer be accepted.",
                "Update clients to remove request bodies."));
            return;
        }

        var oldBody = oldOperation.RequestBody!;
        var newBody = newOperation.RequestBody!;

        if (!oldBody.Required && newBody.Required)
        {
            changes.Add(BuildOperationChange(
                newOperation,
                ChangeSeverity.Breaking,
                "Request body became required",
                $"paths.{path}.{newOperation.Method}.requestBody.required",
                "false",
                "true",
                "Clients must send a request body for this operation.",
                "Ensure clients send a request body before deployment."));
        }
        else if (oldBody.Required && !newBody.Required)
        {
            changes.Add(BuildOperationChange(
                newOperation,
                ChangeSeverity.Additive,
                "Request body became optional",
                $"paths.{path}.{newOperation.Method}.requestBody.required",
                "true",
                "false",
                "Clients may omit the request body without failing validation.",
                "Optionally update documentation for the optional body."));
        }

        CompareContentTypes(path, newOperation, "requestBody", oldBody.Content, newBody.Content, changes);
    }

    private static void CompareResponses(string path, Operation oldOperation, Operation newOperation, List<ChangeRecord> changes)
    {
        foreach (var oldResponse in oldOperation.Responses)
        {
            if (!newOperation.Responses.TryGetValue(oldResponse.Key, out var newResponse))
            {
                changes.Add(BuildOperationChange(
                    oldOperation,
                    ChangeSeverity.Breaking,
                    "Response status removed",
                    $"paths.{path}.{oldOperation.Method}.responses[{oldResponse.Key}]",
                    "present",
                    "missing",
                    "Clients can no longer receive this status code.",
                    "Update client handling for the removed status code."));

                continue;
            }

            CompareContentTypes(path, newOperation, $"responses[{oldResponse.Key}]", oldResponse.Value.Content, newResponse.Content, changes);
        }

        foreach (var newResponse in newOperation.Responses)
        {
            if (oldOperation.Responses.ContainsKey(newResponse.Key))
            {
                continue;
            }

            changes.Add(BuildOperationChange(
                newOperation,
                ChangeSeverity.Additive,
                "Response status added",
                $"paths.{path}.{newOperation.Method}.responses[{newResponse.Key}]",
                "missing",
                "present",
                "Clients may receive a new response status code.",
                "Update client handling if the new status code is relevant."));
        }
    }

    private static void CompareContentTypes(
        string path,
        Operation operation,
        string pointerRoot,
        Dictionary<string, MediaType> oldContent,
        Dictionary<string, MediaType> newContent,
        List<ChangeRecord> changes)
    {
        foreach (var oldContentType in oldContent)
        {
            if (!newContent.TryGetValue(oldContentType.Key, out var newMediaType))
            {
                changes.Add(BuildOperationChange(
                    operation,
                    ChangeSeverity.Breaking,
                    "Content type removed",
                    $"paths.{path}.{operation.Method}.{pointerRoot}.content[{oldContentType.Key}]",
                    "present",
                    "missing",
                    "Clients can no longer send or receive this content type.",
                    "Update clients to use supported content types."));

                continue;
            }

            if (SchemaTypeChanged(oldContentType.Value.Schema, newMediaType.Schema))
            {
                changes.Add(BuildOperationChange(
                    operation,
                    ChangeSeverity.Breaking,
                    "Schema changed",
                    $"paths.{path}.{operation.Method}.{pointerRoot}.content[{oldContentType.Key}].schema",
                    DescribeSchema(oldContentType.Value.Schema),
                    DescribeSchema(newMediaType.Schema),
                    "The payload schema is incompatible with previous clients.",
                    "Update clients to match the new payload schema."));
            }
        }

        foreach (var newContentType in newContent)
        {
            if (oldContent.ContainsKey(newContentType.Key))
            {
                continue;
            }

            changes.Add(BuildOperationChange(
                operation,
                ChangeSeverity.Additive,
                "Content type added",
                $"paths.{path}.{operation.Method}.{pointerRoot}.content[{newContentType.Key}]",
                "missing",
                "present",
                "A new content type is supported.",
                "Document the new content type for consumers."));
        }
    }

    private static void CompareComponentSchemas(OpenApiSpec oldSpec, OpenApiSpec newSpec, List<ChangeRecord> changes)
    {
        foreach (var oldSchema in oldSpec.Schemas)
        {
            if (!newSpec.Schemas.TryGetValue(oldSchema.Key, out var newSchema))
            {
                continue;
            }

            CompareSchemaDetails(oldSchema.Key, oldSchema.Value, newSchema, changes);
        }
    }

    private static void CompareSchemaDetails(string schemaName, Schema oldSchema, Schema newSchema, List<ChangeRecord> changes)
    {
        var endpoint = $"components.schemas.{schemaName}";
        var tag = "components";

        foreach (var oldProperty in oldSchema.Properties)
        {
            if (!newSchema.Properties.TryGetValue(oldProperty.Key, out var newPropertySchema))
            {
                changes.Add(new ChangeRecord
                {
                    Severity = ChangeSeverity.Breaking,
                    RiskScore = CalculateRisk(ChangeSeverity.Breaking, new[] { tag }),
                    Tag = tag,
                    Endpoint = endpoint,
                    Pointer = $"components.schemas.{schemaName}.properties.{oldProperty.Key}",
                    Title = "Schema property removed",
                    Before = "present",
                    After = "missing",
                    Meaning = "Clients relying on this property will no longer receive it.",
                    SuggestedAction = "Remove or replace usage of the removed property."
                });

                continue;
            }

            if (SchemaTypeChanged(oldProperty.Value, newPropertySchema))
            {
                changes.Add(new ChangeRecord
                {
                    Severity = ChangeSeverity.Breaking,
                    RiskScore = CalculateRisk(ChangeSeverity.Breaking, new[] { tag }),
                    Tag = tag,
                    Endpoint = endpoint,
                    Pointer = $"components.schemas.{schemaName}.properties.{oldProperty.Key}.schema",
                    Title = "Schema property type changed",
                    Before = DescribeSchema(oldProperty.Value),
                    After = DescribeSchema(newPropertySchema),
                    Meaning = "The property type is incompatible with previous payloads.",
                    SuggestedAction = "Update clients to match the new property type."
                });
            }

            CompareEnums(schemaName, oldProperty.Key, oldProperty.Value, newPropertySchema, changes);
        }

        foreach (var newProperty in newSchema.Properties)
        {
            if (oldSchema.Properties.ContainsKey(newProperty.Key))
            {
                continue;
            }

            var isRequired = newSchema.Required.Contains(newProperty.Key);
            var severity = isRequired ? ChangeSeverity.Risky : ChangeSeverity.Additive;
            var title = isRequired ? "Required schema property added" : "Optional schema property added";
            var meaning = isRequired
                ? "Clients must now supply this property in payloads."
                : "Clients may include this new property in payloads.";
            var action = isRequired
                ? "Ensure clients populate the new required property."
                : "Document the new optional property for consumers.";

            changes.Add(new ChangeRecord
            {
                Severity = severity,
                RiskScore = CalculateRisk(severity, new[] { tag }),
                Tag = tag,
                Endpoint = endpoint,
                Pointer = $"components.schemas.{schemaName}.properties.{newProperty.Key}",
                Title = title,
                Before = "missing",
                After = "present",
                Meaning = meaning,
                SuggestedAction = action
            });
        }

        foreach (var requiredProperty in newSchema.Required)
        {
            if (!oldSchema.Required.Contains(requiredProperty) && oldSchema.Properties.ContainsKey(requiredProperty))
            {
                changes.Add(new ChangeRecord
                {
                    Severity = ChangeSeverity.Risky,
                    RiskScore = CalculateRisk(ChangeSeverity.Risky, new[] { tag }),
                    Tag = tag,
                    Endpoint = endpoint,
                    Pointer = $"components.schemas.{schemaName}.required[{requiredProperty}]",
                    Title = "Schema property became required",
                    Before = "optional",
                    After = "required",
                    Meaning = "Payloads must now include this property to validate.",
                    SuggestedAction = "Ensure clients always include the required property."
                });
            }
        }

        foreach (var requiredProperty in oldSchema.Required)
        {
            if (!newSchema.Required.Contains(requiredProperty) && newSchema.Properties.ContainsKey(requiredProperty))
            {
                changes.Add(new ChangeRecord
                {
                    Severity = ChangeSeverity.Cosmetic,
                    RiskScore = CalculateRisk(ChangeSeverity.Cosmetic, new[] { tag }),
                    Tag = tag,
                    Endpoint = endpoint,
                    Pointer = $"components.schemas.{schemaName}.required[{requiredProperty}]",
                    Title = "Schema property became optional",
                    Before = "required",
                    After = "optional",
                    Meaning = "Payloads may omit this property without validation errors.",
                    SuggestedAction = "Optionally update documentation for optional usage."
                });
            }
        }

        CompareEnums(schemaName, null, oldSchema, newSchema, changes);
    }

    private static void CompareEnums(string schemaName, string? propertyName, Schema oldSchema, Schema newSchema, List<ChangeRecord> changes)
    {
        if (oldSchema.Enum.Count == 0 && newSchema.Enum.Count == 0)
        {
            return;
        }

        var removed = oldSchema.Enum.Except(newSchema.Enum, StringComparer.Ordinal).ToList();
        var added = newSchema.Enum.Except(oldSchema.Enum, StringComparer.Ordinal).ToList();
        if (removed.Count == 0 && added.Count == 0)
        {
            return;
        }

        var endpoint = $"components.schemas.{schemaName}";
        var tag = "components";
        var pointerTarget = propertyName is null
            ? $"components.schemas.{schemaName}.enum"
            : $"components.schemas.{schemaName}.properties.{propertyName}.enum";

        if (removed.Count > 0)
        {
            changes.Add(new ChangeRecord
            {
                Severity = ChangeSeverity.Breaking,
                RiskScore = CalculateRisk(ChangeSeverity.Breaking, new[] { tag }),
                Tag = tag,
                Endpoint = endpoint,
                Pointer = pointerTarget,
                Title = "Enum values removed",
                Before = string.Join(", ", oldSchema.Enum),
                After = string.Join(", ", newSchema.Enum),
                Meaning = "Clients sending removed enum values will fail validation.",
                SuggestedAction = "Update clients to use supported enum values."
            });
        }

        if (added.Count > 0)
        {
            changes.Add(new ChangeRecord
            {
                Severity = ChangeSeverity.Additive,
                RiskScore = CalculateRisk(ChangeSeverity.Additive, new[] { tag }),
                Tag = tag,
                Endpoint = endpoint,
                Pointer = pointerTarget,
                Title = "Enum values added",
                Before = string.Join(", ", oldSchema.Enum),
                After = string.Join(", ", newSchema.Enum),
                Meaning = "Clients may encounter new enum values.",
                SuggestedAction = "Update clients to handle the new enum values."
            });
        }
    }

    private static ChangeRecord BuildOperationChange(
        Operation operation,
        ChangeSeverity severity,
        string title,
        string pointer,
        string before,
        string after,
        string meaning,
        string suggestedAction)
    {
        var tag = operation.Tags.FirstOrDefault() ?? "untagged";
        return new ChangeRecord
        {
            Severity = severity,
            RiskScore = CalculateRisk(severity, operation.Tags),
            Tag = tag,
            Endpoint = $"{operation.Method.ToUpperInvariant()} {operation.Path}",
            Pointer = pointer,
            Title = title,
            Before = before,
            After = after,
            Meaning = meaning,
            SuggestedAction = suggestedAction
        };
    }

    private static string ParamKey(Parameter parameter) => $"{parameter.Name}:{parameter.In}";

    private static bool SchemaTypeChanged(Schema? oldSchema, Schema? newSchema)
    {
        var oldDescriptor = DescribeSchema(oldSchema);
        var newDescriptor = DescribeSchema(newSchema);
        return !string.Equals(oldDescriptor, newDescriptor, StringComparison.Ordinal);
    }

    private static string DescribeSchema(Schema? schema)
    {
        if (schema is null)
        {
            return "none";
        }

        if (!string.IsNullOrWhiteSpace(schema.Ref))
        {
            return $"$ref:{schema.Ref}";
        }

        if (!string.IsNullOrWhiteSpace(schema.Format))
        {
            return $"{schema.Type}:{schema.Format}";
        }

        return schema.Type ?? "object";
    }

    private static int CalculateRisk(ChangeSeverity severity, IEnumerable<string> tags)
    {
        var baseScore = severity switch
        {
            ChangeSeverity.Breaking => 100,
            ChangeSeverity.Risky => 60,
            ChangeSeverity.Additive => 20,
            ChangeSeverity.Cosmetic => 5,
            _ => 0
        };

        var hasPublicTag = tags.Any(tag => tag.Contains("public", StringComparison.OrdinalIgnoreCase));
        return hasPublicTag ? baseScore + 10 : baseScore;
    }
}
