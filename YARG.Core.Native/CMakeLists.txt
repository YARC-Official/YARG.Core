project(YARG.Core.Native LANGUAGES CXX C)

# CMake compiler settings
set(CMAKE_CXX_STANDARD 17)

# Collect project sources
file(GLOB_RECURSE SOURCE_FILES
    CONFIGURE_DEPENDS
    cpp/*.cpp
    cpp/*.c
)
file(GLOB_RECURSE HEADER_FILES
    CONFIGURE_DEPENDS
    cpp/*.hpp
    cpp/*.h
)

# Create shared library and set the source files to be built
add_library(YARG.Core.Native SHARED
    ${SOURCE_FILES}
    ${HEADER_FILES}
)

# Link against libraries
add_subdirectory(lib)
target_link_libraries(YARG.Core.Native
    PRIVATE
    stb
)

if(WIN32)
    target_link_libraries(YARG.Core.Native
        PRIVATE
        dbghelp
    )
endif()

# Various target properties
set_target_properties(YARG.Core.Native PROPERTIES
    VERSION "0.1.0" # Build version
    SOVERSION "0" # ABI version

    # Set separate output name from the C# assembly
    OUTPUT_NAME "yarg_core_native"

    # Change output to the current directory for easier visibility of output files
    RUNTIME_OUTPUT_DIRECTORY ${PROJECT_SOURCE_DIR}/bin/native
    LIBRARY_OUTPUT_DIRECTORY ${PROJECT_SOURCE_DIR}/bin/native
    ARCHIVE_OUTPUT_DIRECTORY ${PROJECT_SOURCE_DIR}/bin/native
)

# Set defines
target_compile_definitions(YARG.Core.Native
    PRIVATE
    "YARG_EXPORTS"
)

# Set a uniform debug define
if(CMAKE_BUILD_TYPE STREQUAL "Debug")
    target_compile_definitions(YARG.Core.Native PUBLIC "YARG_DEBUG")
endif()
