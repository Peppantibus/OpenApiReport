# GitHub Actions: OpenAPI Snapshot Diff

## Pull request workflow (snapshot + diff)

```yaml
name: OpenAPI Snapshot Diff

on:
  pull_request:

jobs:
  openapi:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x

      - name: Run snapshot diff (Swashbuckle)
        run: |
          openapi-report snapshot-diff \
            --mode swashbuckle \
            --project ./src/MyApi/MyApi.csproj \
            --swaggerDoc v1 \
            --base-ref ${{ github.event.pull_request.base.sha }} \
            --head-ref ${{ github.event.pull_request.head.sha }} \
            --out-dir ./reports/openapi \
            --formats md,json \
            --fail-on-breaking

      - name: Run snapshot diff (config file)
        run: |
          openapi-report snapshot-diff \
            --config-file ./openapi-report.json \
            --base-ref ${{ github.event.pull_request.base.sha }} \
            --head-ref ${{ github.event.pull_request.head.sha }}

      - name: Report location
        run: |
          echo "Report stored at ./reports/openapi/${{ github.event.repository.name }}/openapi.diff.md"
```

## Upload report artifacts

```yaml
      - name: Upload OpenAPI reports
        uses: actions/upload-artifact@v4
        with:
          name: openapi-reports
          path: ./reports/openapi
```

## Comment on the PR with the markdown report

```yaml
      - name: Comment OpenAPI report
        uses: actions/github-script@v7
        with:
          script: |
            const fs = require("fs");
            const report = fs.readFileSync("./reports/openapi/${{ github.event.repository.name }}/openapi.diff.md", "utf8");
            await github.rest.issues.createComment({
              owner: context.repo.owner,
              repo: context.repo.repo,
              issue_number: context.issue.number,
              body: report
            });
```

## Push to main (base/head from event payload)

```yaml
name: OpenAPI Snapshot Diff (main)

on:
  push:
    branches:
      - main

jobs:
  openapi:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x

      - name: Run snapshot diff (URL capture)
        run: |
          openapi-report snapshot-diff \
            --mode url \
            --url https://localhost:5001/swagger/v1/swagger.json \
            --base-ref ${{ github.event.before }} \
            --head-ref ${{ github.sha }} \
            --out-dir ./reports/openapi \
            --formats md,json
```
