# https://github.com/nothings/stb
project(stb LANGUAGES CXX C)

add_library(stb STATIC
    "stb_implementation.c"
)

set_target_properties(stb PROPERTIES
    # GCC requires this to be set when linking a static library into a shared library
    POSITION_INDEPENDENT_CODE TRUE
)

target_include_directories(stb
    PUBLIC
    ${PROJECT_SOURCE_DIR}
)
