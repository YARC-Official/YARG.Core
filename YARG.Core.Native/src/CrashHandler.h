#ifndef YARG_CRASH_HANDLER_H
#define YARG_CRASH_HANDLER_H

#include "typedefs.h"

YARG_EXPORT void YARGCrashHandler_Install();

YARG_EXPORT void YARGCrash();

#if _WIN32
const char* GetExceptionString(DWORD exceptionCode);
#else
#include <signal.h>
const char* GetExceptionString(int signal, siginfo_t* sigInfo);
#endif

void Overflow();

#endif