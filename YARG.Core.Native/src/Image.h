#ifndef YARG_IMAGE_H
#define YARG_IMAGE_H

#include "typedefs.h"
#include "stb_image.h"

YARG_EXPORT byte *YARGImage_Load(const byte *bytes, int length, int *width, int *height, int *component);
YARG_EXPORT void YARGImage_Free(byte *image);

#endif // YARG_IMAGE_H
