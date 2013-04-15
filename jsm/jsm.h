#pragma once

#include <windows.h>
#include <stdio.h>

typedef long               I;
typedef char               C;
typedef struct {I k,flag,m,t,c,n,r,s[1];}* A;
typedef void  *JtsCallback (void** jts, int jtSz, int jtIdx);

#ifdef __cplusplus
extern "C" {
#endif 
    int  __stdcall JInit();
    int  __stdcall JSM(int, void**);
    int  __stdcall JDo(int, C*);
    C*   __stdcall JGetLocale(int);
    A    __stdcall JGetA(int, I, C*);
    int  __stdcall JGetM(int, C*, I*, I*, I*, I*);
    I    __stdcall JSetA(int, I, C*, I, C*);
    int  __stdcall JSetM(int, C*, I*, I*, I*, I*);
    A    __stdcall Jga(int, I, I, I, I*);
    int  __stdcall JErrorTextM(int, I, I*);
    int  __stdcall JFree(int);
    int  __stdcall JDoR(int, C*, VARIANT*);
    int  __stdcall JGet(int, C*, VARIANT*);
    int  __stdcall JGetB(int, C*, VARIANT*);
    int  __stdcall JSet(int, C*, VARIANT*);
    int  __stdcall JSetB(int, C*, VARIANT*);
    int  __stdcall JErrorText(int, I, VARIANT*);
    int  __stdcall JErrorTextB(int, I, VARIANT*);
    int  __stdcall JTranspose(int, I);
    int  __stdcall JBreak(int);
    int  __stdcall JClear(int);
    int  __stdcall JIsBusy(int);
    void*__stdcall JGetJt(int);
    void __stdcall JProcessJts(JtsCallback);
    C    __stdcall JIncAdBreak(int);
#ifdef __cplusplus
}
#endif