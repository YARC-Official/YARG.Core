#define STB_IMAGE_IMPLEMENTATION
#include "stb_image.h"

extern "C" __declspec(dllexport) unsigned char* load_image_from_memory(const unsigned char* bytes, int length, int* width, int* height, int* components)
{
    return stbi_load_from_memory(bytes, length, width, height, components, 0);
}

extern "C" __declspec(dllexport) void free_image(unsigned char* image)
{
    stbi_image_free(image);
}