# Binding generator

This current-LTS tool validates the checked-in normalized HIP manifest model without adding a runtime dependency to the core package. The repository PowerShell entry point remains the source generator because it emits the target-specific C# declarations:

```text
pwsh ./eng/generate-interop.ps1 generate
pwsh ./eng/generate-interop.ps1 generate --check
pwsh ./eng/generate-interop.ps1 probe-manifest
dotnet run --project tools/JYPPX.HipSharp.BindingGenerator -- probe-manifest
```

Official headers are accepted only through an explicitly supplied `HeaderRoot`; the generator never searches arbitrary working directories or downloads headers implicitly.
