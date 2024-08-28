#ifdef _WIN32

#include "CrashHandler.h"

#include <Windows.h>
#include <stdio.h>
#include <DbgHelp.h>

// MiniDumpWriteDump function pointer type
//typedef BOOL(WINAPI* MINIDUMPWRITEDUMP)(HANDLE, DWORD, HANDLE, MINIDUMP_TYPE, PMINIDUMP_EXCEPTION_INFORMATION, PMINIDUMP_USER_STREAM_INFORMATION, PMINIDUMP_CALLBACK_INFORMATION);

void CreateMiniDump(EXCEPTION_POINTERS* exceptionPointers)
{
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
    const char* exception = GetExceptionString(exceptionPointers->ExceptionRecord->ExceptionCode);
    //CreateMiniDump(exceptionPointers);

    const char* pipeName = "\\\\.\\pipe\\YARGPipe";
    HANDLE pipeHandle = CreateFile(pipeName, GENERIC_WRITE, 0, NULL, OPEN_EXISTING, 0, NULL);

    char buff[256];
    if (pipeHandle == INVALID_HANDLE_VALUE)
    {
        ZeroMemory(buff, 256);
        sprintf_s(buff, 100, "Failed to open pipe: %d", GetLastError());
        MessageBox(NULL, buff, "Pipe Open Result", MB_OK | MB_ICONERROR);
        return EXCEPTION_EXECUTE_HANDLER;
    }

    DWORD result;
    BOOL writeResult = WriteFile(pipeHandle, exception, (DWORD)strlen(exception) + 1, &result, NULL);
    CloseHandle(pipeHandle);

    if (!writeResult)
    {
        ZeroMemory(buff, 256);
        sprintf_s(buff, 100, "Failed to write to pipe: %d", GetLastError());
        MessageBox(NULL, buff, "Pipe Write Result", MB_OK | MB_ICONERROR);
        return EXCEPTION_EXECUTE_HANDLER;
    }

    MessageBox(NULL, exception, "Exception", MB_OK | MB_ICONERROR);
    return EXCEPTION_EXECUTE_HANDLER;
}

void YARGCrashHandler_Install()
{
    SetUnhandledExceptionFilter(CrashHandler_ExceptionHandler);
}

#endif