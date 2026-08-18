extern "C" __global__ void SegmentDefects(
    const unsigned char* image,
    unsigned char* mask,
    int length,
    int darkThreshold,
    int brightThreshold)
{
    int index = (int)(blockIdx.x * blockDim.x + threadIdx.x);
    if (index >= length)
    {
        return;
    }

    unsigned char pixel = image[index];
    mask[index] = (pixel < darkThreshold || pixel > brightThreshold) ? 255 : 0;
}
