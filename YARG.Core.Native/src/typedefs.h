#ifndef YARG_TYPEDEFS_H
#define YARG_TYPEDEFS_H

// Standard type definitions
#include <stddef.h>
#include <stdint.h>

// Library export defines
#ifdef __cplusplus
    #define EXTERN_C extern "C"
#else
    #define EXTERN_C
#endif

#if defined(_WIN32) || defined(__CYGWIN__)
    #ifdef YARG_EXPORTS
        #ifdef __GNUC__
            #define YARG_EXPORT EXTERN_C __attribute__((dllexport))
        #else
            #define YARG_EXPORT EXTERN_C __declspec(dllexport)
        #endif
    #else
        #ifdef __GNUC__
            #define YARG_EXPORT EXTERN_C __attribute__((dllimport))
        #else
            #define YARG_EXPORT EXTERN_C __declspec(dllimport)
        #endif
    #endif
#elif __GNUC__ >= 4
    #define YARG_EXPORT EXTERN_C __attribute__((visibility ("default")))
#else
    #define YARG_EXPORT EXTERN_C
#endif

// Other useful type definitions
typedef unsigned char byte;
typedef unsigned short ushort;
typedef unsigned int uint;

#endif // YARG_TYPEDEFS_H
