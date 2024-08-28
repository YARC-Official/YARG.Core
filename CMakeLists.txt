cmake_minimum_required(VERSION 3.15)
project(YARG.Core LANGUAGES CXX C)

# Set some global CMake settings
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_OSX_ARCHITECTURES x86_64;arm64)
set(CMAKE_OSX_DEPLOYMENT_TARGET 10.15)

# Determine target platform
if(WIN32)
    set(YARG_PLATFORM windows)
elseif(APPLE)
    set(YARG_PLATFORM macos)
elseif(LINUX)
    set(YARG_PLATFORM linux)
else()
    message(FATAL_ERROR "Unsupported target platform")
    return()
endif()

# Determine target architecture
if(CMAKE_SIZEOF_VOID_P EQUAL 8)
    set(YARG_64BIT TRUE)
    set(YARG_32BIT FALSE)
    set(YARG_ARCH_BITS 64bit)
elseif(CMAKE_SIZEOF_VOID_P EQUAL 4)
    set(YARG_64BIT FALSE)
    set(YARG_32BIT TRUE)
    set(YARG_ARCH_BITS 32bit)
else()
    message(FATAL_ERROR "Unsupported architecture bit size")
    return()
endif()

# Forward to YARG.Core.Native
add_subdirectory(YARG.Core.Native)