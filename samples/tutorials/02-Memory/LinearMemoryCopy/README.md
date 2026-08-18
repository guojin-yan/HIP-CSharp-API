# LinearMemoryCopy / 线性内存复制

Allocates two device buffers, performs H2D, D2D, and D2H copies for 256 bytes, compares the result, synchronizes, and frees both allocations. Run only on a machine with a working AMD HIP Runtime:

```bash
dotnet run --project samples/tutorials/02-Memory/LinearMemoryCopy/LinearMemoryCopy.csproj -c Release
```
