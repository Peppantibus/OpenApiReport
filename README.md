# OpenApiReport

OpenApiReport is a .NET CLI tool that generates **semantic change reports**
between two OpenAPI specifications.

Unlike text-based diffs, OpenApiReport focuses on **what a change means**:
breaking changes, risky changes, additive changes, and cosmetic changes.

## Goals
- Report **only changes**, not full specifications
- Classify changes by severity (Breaking / Risky / Additive / Cosmetic)
- Explain the **meaning** of each change
- Suggest **actions or migration notes**
- Produce CI-friendly exit codes and machine-readable output

## Usage (planned)
```bash
openapi-report diff old.json new.json --format md
