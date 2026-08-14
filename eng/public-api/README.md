# Public API freeze

`JYPPX.ROCm.HipSharp.0.9.1.txt` is the committed public API baseline for the 0.9.1 forward-fix line. The assembly identity is `JYPPX.ROCm.HIP.CSharp.API`, while exported namespaces remain under `JYPPX.ROCm.HipSharp`. The snapshot records exported types and their declared public or protected constructors, methods, properties, events, and fields. Enum backing fields and framework-supplied enum interfaces are excluded; enum values are included.

Run the compatibility gate after building Release:

```powershell
./eng/verify-public-api.ps1 -Configuration Release
```

The gate compares `net10.0` with the committed snapshot and confirms that all 15 target frameworks expose the same surface. Updating the baseline requires an explicit review action:

```powershell
./eng/verify-public-api.ps1 -Configuration Release -Update
```

`categories.json` classifies the exported surface as formal or diagnostic and records sample-only and internal roots. A baseline update must be reviewed together with `docs/releases/0.9.1-forward-fix.md`; it is not a routine generated-file refresh.

NuGet package validation separately checks compatible-framework and compatible-TFM pairs. `src/JYPPX.ROCm.HipSharp/CompatibilitySuppressions.xml` narrows its only suppression category to `CP0008` entries for the `ISpanFormattable` interface added by the .NET 8 runtime to each public enum; that interface is not declared by HipSharp.
