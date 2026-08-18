using System;
using System.Diagnostics;
using System.Threading.Tasks;

internal static class CpuHeatSolver
{
    internal static CpuSolveResult Solve(float[] initialField, float[] fixedField, int width, int height, int steps)
    {
        ArgumentNullException.ThrowIfNull(initialField);
        ArgumentNullException.ThrowIfNull(fixedField);

        if (initialField.Length != checked(width * height) || fixedField.Length != initialField.Length)
        {
            throw new ArgumentException("The input fields do not match the grid dimensions.", nameof(initialField));
        }

        var current = new float[initialField.Length];
        var next = new float[initialField.Length];
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
        };

        var stopwatch = Stopwatch.StartNew();
        Array.Copy(initialField, current, initialField.Length);
        for (int step = 0; step < steps; step++)
        {
            Parallel.For(0, height, parallelOptions, y =>
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    int index = row + x;
                    float fixedTemperature = fixedField[index];
                    if (fixedTemperature >= 0.0f)
                    {
                        next[index] = fixedTemperature;
                        continue;
                    }

                    float center = current[index];
                    float neighbors = current[index - 1] + current[index + 1] + current[index - width] + current[index + width];
                    next[index] = center + HeatProblem.DiffusionRate * (neighbors - (4.0f * center));
                }
            });

            (current, next) = (next, current);
        }

        stopwatch.Stop();
        return new CpuSolveResult(current, stopwatch.Elapsed.TotalMilliseconds, parallelOptions.MaxDegreeOfParallelism);
    }
}

internal sealed class CpuSolveResult
{
    internal CpuSolveResult(float[] field, double elapsedMilliseconds, int workerCount)
    {
        Field = field;
        ElapsedMilliseconds = elapsedMilliseconds;
        WorkerCount = workerCount;
    }

    internal float[] Field { get; }

    internal double ElapsedMilliseconds { get; }

    internal int WorkerCount { get; }
}
