extern "C" __global__ void HeatStep(
    const float* current,
    float* next,
    const float* fixedField,
    int width,
    int height)
{
    int x = (int)(blockIdx.x * blockDim.x + threadIdx.x);
    int y = (int)(blockIdx.y * blockDim.y + threadIdx.y);
    if (x >= width || y >= height)
    {
        return;
    }

    int index = (y * width) + x;
    float fixedTemperature = fixedField[index];
    if (fixedTemperature >= 0.0f)
    {
        next[index] = fixedTemperature;
        return;
    }

    float center = current[index];
    float neighbors = current[index - 1] + current[index + 1] +
        current[index - width] + current[index + width];
    next[index] = center + 0.2f * (neighbors - (4.0f * center));
}
