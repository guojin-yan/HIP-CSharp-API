# Public API freeze

The current development baseline is `JYPPX.ROCm.HipSharp.0.11.0-dev.txt`. It includes advanced interop owners and the external-semaphore graph node kinds. `0.10.0.txt` matches the immutable published 0.10.0 package; subsequent additions belong to the development baseline. Development package validation does not authorize publication.

The `0.10.0` baseline covers the published HIPRTC Program/Linker work and M8.13 managed-interface promotion, including typed common queries, virtual-memory owners, arrays, mipmapped arrays, textures, surfaces, and borrowed legacy texture references. The separately retained `0.9.2` and `0.9.3` snapshots remain historical inputs. The assembly identity is `JYPPX.ROCm.HIP.CSharp.API`, while exported namespaces remain under `JYPPX.ROCm.HipSharp`. Snapshots record exported types and their declared public or protected constructors, methods, properties, events, and fields. Enum backing fields and framework-supplied enum interfaces are excluded; enum values are included.

Run the compatibility gate after building Release:

```powershell
./eng/verify-public-api.ps1 -Configuration Release
```

The gate compares `net10.0` with the committed snapshot and confirms that all 15 target frameworks expose the same surface. Updating the baseline requires an explicit review action:

```powershell
./eng/verify-public-api.ps1 -Configuration Release -Update
```

`categories.json` classifies the exported surface as formal or diagnostic and records sample-only and internal roots. A baseline update must be reviewed together with the current 0.x batch record; it is not a routine generated-file refresh. M8.9 promotion locks, the `0.9.1` release records, and the historical local `1.0.0` candidate remain immutable historical inputs.

NuGet package validation separately checks compatible-framework and compatible-TFM pairs. `src/JYPPX.ROCm.HipSharp/CompatibilitySuppressions.xml` narrows its only suppression category to `CP0008` entries for the `ISpanFormattable` interface added by the .NET 8 runtime to each public enum; that interface is not declared by HipSharp.
