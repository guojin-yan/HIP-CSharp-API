using System;

internal static class HeatProblem
{
    internal const float AmbientTemperature = 20.0f;
    internal const float DiffusionRate = 0.2f;

    internal static float[] CreateInitialField(int width, int height)
    {
        return CreateInitialField(CreateFixedField(width, height));
    }

    internal static float[] CreateInitialField(float[] fixedField)
    {
        ArgumentNullException.ThrowIfNull(fixedField);
        var field = new float[fixedField.Length];
        for (int index = 0; index < field.Length; index++)
        {
            field[index] = fixedField[index] >= 0.0f
                ? fixedField[index]
                : AmbientTemperature;
        }

        return field;
    }

    internal static float[] CreateFixedField(int width, int height)
    {
        var field = new float[checked(width * height)];
        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                float fixedTemperature = GetFixedTemperature(x, y, width, height);
                field[row + x] = float.IsNaN(fixedTemperature) ? -1.0f : fixedTemperature;
            }
        }

        return field;
    }

    internal static float GetFixedTemperature(int x, int y, int width, int height)
    {
        if (x == 0 || y == 0 || x == width - 1 || y == height - 1)
        {
            return AmbientTemperature;
        }

        int radius = Math.Max(8, Math.Min(width, height) / 20);
        if (IsInsideCircle(x, y, width / 4, height / 3, radius))
        {
            return 100.0f;
        }

        if (IsInsideCircle(x, y, width * 3 / 4, height * 2 / 3, radius))
        {
            return 80.0f;
        }

        if (IsInsideCircle(x, y, width * 2 / 3, height / 4, radius))
        {
            return 5.0f;
        }

        return float.NaN;
    }

    private static bool IsInsideCircle(int x, int y, int centerX, int centerY, int radius)
    {
        int deltaX = x - centerX;
        int deltaY = y - centerY;
        return (deltaX * deltaX) + (deltaY * deltaY) <= radius * radius;
    }
}

internal sealed class ErrorMetrics
{
    private ErrorMetrics(double maximumAbsoluteError, double rootMeanSquareError, int nonFiniteValues)
    {
        MaximumAbsoluteError = maximumAbsoluteError;
        RootMeanSquareError = rootMeanSquareError;
        NonFiniteValues = nonFiniteValues;
    }

    internal double MaximumAbsoluteError { get; }

    internal double RootMeanSquareError { get; }

    internal int NonFiniteValues { get; }

    internal static ErrorMetrics Compare(float[] expected, float[] actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);

        if (expected.Length != actual.Length)
        {
            throw new ArgumentException("CPU and GPU fields must have the same length.", nameof(actual));
        }

        double maximum = 0;
        double squaredError = 0;
        int nonFinite = 0;
        for (int index = 0; index < expected.Length; index++)
        {
            if (!float.IsFinite(expected[index]) || !float.IsFinite(actual[index]))
            {
                nonFinite++;
                continue;
            }

            double error = Math.Abs((double)expected[index] - actual[index]);
            maximum = Math.Max(maximum, error);
            squaredError += error * error;
        }

        double rootMeanSquare = nonFinite == expected.Length
            ? double.PositiveInfinity
            : Math.Sqrt(squaredError / (expected.Length - nonFinite));
        return new ErrorMetrics(maximum, rootMeanSquare, nonFinite);
    }
}
