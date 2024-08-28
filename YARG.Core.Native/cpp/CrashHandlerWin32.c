#ifdef _WIN32

#include "CrashHandler.h"

#include <Windows.h>
#include <DbgHelp.h>

// MiniDumpWriteDump function pointer type
//typedef BOOL(WINAPI* MINIDUMPWRITEDUMP)(HANDLE, DWORD, HANDLE, MINIDUMP_TYPE, PMINIDUMP_EXCEPTION_INFORMATION, PMINIDUMP_USER_STREAM_INFORMATION, PMINIDUMP_CALLBACK_INFORMATION);

void CreateMiniDump(EXCEPTION_POINTERS* exceptionPointers)
{
    // TODO Try to link with dbghelp.lib instead of loading it dynamically?
    // HMODULE hDbgHelp = LoadLibrary("dbghelp.dll");
    // if (hDbgHelp == NULL)
    // {
    //     return;
    // }

    // Get the MiniDumpWriteDump function pointer
    // MINIDUMPWRITEDUMP MiniDumpWriteDump = (MINIDUMPWRITEDUMP)GetProcAddress(hDbgHelp, "MiniDumpWriteDump");

    // Create the dump file
    HANDLE hFile = CreateFileW(L"crash.dmp", GENERIC_WRITE, FILE_SHARE_WRITE, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
    if (hFile == INVALID_HANDLE_VALUE)
    {
        return;
    }

    MINIDUMP_EXCEPTION_INFORMATION exceptionInfo;
    exceptionInfo.ThreadId = GetCurrentThreadId();
    exceptionInfo.ExceptionPointers = exceptionPointers;
    exceptionInfo.ClientPointers = FALSE;

    MINIDUMP_TYPE dumpType = MiniDumpNormal;

    // Write the dump
    MiniDumpWriteDump(GetCurrentProcess(), GetCurrentProcessId(), hFile, dumpType, exceptionPointers ? &exceptionInfo : NULL, NULL, NULL);

    // Close the file
    CloseHandle(hFile);
}

LONG WINAPI CrashHandler_ExceptionHandler(EXCEPTION_POINTERS* exceptionPointers)
{
    //CreateMiniDump(exceptionPointers);
    MessageBox(NULL, "An exception occurred!", "Exception", MB_OK | MB_ICONERROR);
    return EXCEPTION_EXECUTE_HANDLER;
}

void YARGCrashHandler_Install()
{
    SetUnhandledExceptionFilter(CrashHandler_ExceptionHandler);
}

#endif