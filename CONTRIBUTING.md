# Contributing

## Scope

Keep changes inside the program repository. The sibling `plan`, `diary`, and `Radeon_Cloud` directories are project records and are not Git content.

M0 is intentionally a build and packaging baseline. Do not add unverified HIP P/Invoke declarations, downloaded ROCm binaries, GPU claims, or a runtime package payload as part of a baseline maintenance change.

## Development

Use the .NET 10 SDK selected by `global.json`. Run `./eng/build.ps1 -Configuration Release` and `./eng/test.ps1 -Configuration Release -NoBuild` on Windows. A pull request should leave no files under `bin`, `obj`, or `artifacts` tracked by Git.

Any future native asset must have an official URL, package/version, SHA-256, dependency closure, license evidence, and a clean consumer test before its runtime manifest can be enabled.

## Commit and review

Use focused commits with a verb-first subject. Explain compatibility and ownership changes in the pull request. Do not include credentials, cloud addresses, certificates, local absolute paths, or temporary test logs.
