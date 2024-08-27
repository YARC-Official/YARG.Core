#include "Image.h"

byte* YARGImage_Load(const byte* bytes, int length, int* width, int* height, int* components)
{
    return stbi_load_from_memory(bytes, length, width, height, components, 0);
}

void YARGImage_Free(byte* image)
{
    stbi_image_free(image);
}