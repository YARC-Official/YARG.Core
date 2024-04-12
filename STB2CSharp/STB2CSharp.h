#pragma once
#define STB_IMAGE_IMPLEMENTATION
#include "stb_image.h"

#if defined STB_EXPORTS
    #if defined _WIN32
        #define LIB_API(RetType) extern "C" __declspec(dllexport) RetType
    #else
        #define LIB_API(RetType) extern "C" RetType __attribute__((visibility("default")))
    #endif
#else
    #if defined _WIN32
        #define LIB_API(RetType) extern "C" __declspec(dllimport) RetType
    #else
        #define LIB_API(RetType) extern "C" RetType
    #endif
#endif

LIB_API(unsigned char*) load_image_from_memory(const unsigned char* bytes, int length, int* width, int* height, int* components);

LIB_API(void) free_image(unsigned char* image);
