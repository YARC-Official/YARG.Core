# https://github.com/nothings/stb
project(stb LANGUAGES CXX C)

add_library(stb STATIC
    "stb_implementation.c"
)

target_include_directories(stb
    PUBLIC
    ${PROJECT_SOURCE_DIR}
)
