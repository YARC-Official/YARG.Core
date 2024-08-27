#include "CrashHandler.h"

#if _WIN32

const char* GetExceptionString(DWORD exception)
{
    switch (exception)
    {
    case EXCEPTION_ACCESS_VIOLATION:
        return "EXCEPTION_ACCESS_VIOLATION";
    case EXCEPTION_ARRAY_BOUNDS_EXCEEDED:
        return "EXCEPTION_ARRAY_BOUNDS_EXCEEDED";
    case EXCEPTION_BREAKPOINT:
        return "EXCEPTION_BREAKPOINT";
    case EXCEPTION_DATATYPE_MISALIGNMENT:
        return "EXCEPTION_DATATYPE_MISALIGNMENT";
    case EXCEPTION_FLT_DENORMAL_OPERAND:
        return "EXCEPTION_FLT_DENORMAL_OPERAND";
    case EXCEPTION_FLT_DIVIDE_BY_ZERO:
        return "EXCEPTION_FLT_DIVIDE_BY_ZERO";
    case EXCEPTION_FLT_INEXACT_RESULT:
        return "EXCEPTION_FLT_INEXACT_RESULT";
    case EXCEPTION_FLT_INVALID_OPERATION:
        return "EXCEPTION_FLT_INVALID_OPERATION";
    case EXCEPTION_FLT_OVERFLOW:
        return "EXCEPTION_FLT_OVERFLOW";
    case EXCEPTION_FLT_STACK_CHECK:
        return "EXCEPTION_FLT_STACK_CHECK";
    case EXCEPTION_FLT_UNDERFLOW:
        return "EXCEPTION_FLT_UNDERFLOW";
    case EXCEPTION_ILLEGAL_INSTRUCTION:
        return "EXCEPTION_ILLEGAL_INSTRUCTION";
    case EXCEPTION_IN_PAGE_ERROR:
        return "EXCEPTION_IN_PAGE_ERROR";
    case EXCEPTION_INT_DIVIDE_BY_ZERO:
        return "EXCEPTION_INT_DIVIDE_BY_ZERO";
    case EXCEPTION_INT_OVERFLOW:
        return "EXCEPTION_INT_OVERFLOW";
    case EXCEPTION_INVALID_DISPOSITION:
        return "EXCEPTION_INVALID_DISPOSITION";
    case EXCEPTION_NONCONTINUABLE_EXCEPTION:
        return "EXCEPTION_NONCONTINUABLE_EXCEPTION";
    case EXCEPTION_PRIV_INSTRUCTION:
        return "EXCEPTION_PRIV_INSTRUCTION";
    case EXCEPTION_SINGLE_STEP:
        return "EXCEPTION_SINGLE_STEP";
    case EXCEPTION_STACK_OVERFLOW:
        return "EXCEPTION_STACK_OVERFLOW";
    default:
        return "UNKNOWN EXCEPTION";
    }
}

#else

#include <signal.h>

const char* GetExceptionString(int signal, siginfo_t* sigInfo)
{
    switch (signal)
    {
    case SIGSEGV:
        return "SIGSEGV: Segmentation Fault";
    case SIGINT:
        return "SIGINT: Interrupt";
    case SIGFPE:
        switch (sigInfo->si_code)
        {
        case FPE_INTDIV:
            return "SIGFPE: Integer Divide by Zero";
        case FPE_INTOVF:
            return "SIGFPE: Integer Overflow";
        case FPE_FLTDIV:
            return "SIGFPE: Floating Point Divide by Zero";
        case FPE_FLTOVF:
            return "SIGFPE: Floating Point Overflow";
        case FPE_FLTUND:
            return "SIGFPE: Floating Point Underflow";
        case FPE_FLTRES:
            return "SIGFPE: Floating Point Inexact Result";
        case FPE_FLTINV:
            return "SIGFPE: Floating Point Invalid Operation";
        case FPE_FLTSUB:
            return "SIGFPE: Subscript Out of Range";
        default:
            return "SIGFPE: Arithmetic Exception";
        }
    case SIGILL:
        switch (sigInfo->si_code)
        {
        case ILL_ILLOPC:
            return "SIGILL: Illegal Opcode";
        case ILL_ILLOPN:
            return "SIGILL: Illegal Operand";
        case ILL_ILLADR:
            return "SIGILL: Illegal Address";
        case ILL_ILLTRP:
            return "SIGILL: Illegal Trap";
        case ILL_PRVOPC:
            return "SIGILL: Privileged Opcode";
        case ILL_PRVREG:
            return "SIGILL: Privileged Register";
        case ILL_COPROC:
            return "SIGILL: Coprocessor Error";
        case ILL_BADSTK:
            return "SIGILL: Internal Stack Error";
        default:
            return "SIGILL: Illegal Instruction";
        }
    case SIGTERM:
        return "SIGTERM: Termination Requested";
    case SIGABRT:
        return "SIGABRT: Abnormal Termination";
    default:
        return "Unknown Signal";
    }
}

#endif

// Disable recursive function warning as it is intended
#pragma warning(disable: 4717)
void Overflow()
{
    int arr[1000];
    (void)arr;
    Overflow();
}
#pragma warning(default: 4717)

void YARGCrash()
{
    // Access violation
    int* p = (int*)0x12345678;
    *p = 0;

    // Divide by zero
    int a = 1;
    int b = 0;
    int c = a / b;

    // Stack Overflow
    Overflow();
}