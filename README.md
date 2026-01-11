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

## Usage
Generate a semantic diff between two OpenAPI specs:

```bash
openapi-report diff old.json new.json --format md
```

Capture specs directly:

```bash
openapi-report capture --mode url --url https://localhost:5001/swagger/v1/swagger.json --out openapi.json
openapi-report capture --mode swashbuckle --project ./MyApi/MyApi.csproj --configuration Release --swaggerDoc v1 --out openapi.json
openapi-report capture --mode nswag --config ./nswag.json --output openapi.json
```

Run a full snapshot + diff flow (ideal for CI):

```bash
openapi-report snapshot-diff \
  --mode swashbuckle \
  --project ./MyApi/MyApi.csproj \
  --base-ref origin/main \
  --head-ref HEAD \
  --out-dir ./reports/openapi \
  --formats md,json \
  --fail-on-breaking
```

Reports are written to `<out-dir>/<project-name>/openapi.diff.*` (project name defaults to the repo folder name).

See [docs/github-actions.md](docs/github-actions.md) for GitHub Actions examples.
