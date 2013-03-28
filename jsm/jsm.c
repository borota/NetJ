#include "jsm.h"

#define MEMCHUNCK 1024
#ifdef _WIN64
#define JDLLNAME "j.dll"
#else
#define JDLLNAME "j32.dll"
#endif

BOOL GetJProcAddresses(HMODULE);
BOOL GetJProcAddress(void*, char*);

static void** jts = NULL;
static int    jtSz = 0;
static int    jtIdx = -1;
static HANDLE jtMutex = NULL;
static HMODULE hJdll = NULL;

BOOL APIENTRY DllMain(HMODULE hModule, DWORD  ul_reason_for_call, LPVOID lpReserved)
{
	switch (ul_reason_for_call)
	{
	case DLL_PROCESS_ATTACH:
		jtMutex = CreateMutex(NULL, FALSE, NULL);
		if (NULL == jtMutex) {
			printf("CreateMutex error: %d\n", GetLastError());
			return FALSE;
		}
		if (FALSE == GetJProcAddresses(hModule)) {
			return FALSE;
		}
		break;
	case DLL_THREAD_ATTACH:
		break;
	case DLL_THREAD_DETACH:
		break;
	case DLL_PROCESS_DETACH:
		if (NULL != jts) {
			free(jts);
			jts = NULL;
		}
		if (NULL != jtMutex) {
			CloseHandle(jtMutex);
			jtMutex = NULL;
		}
		if (NULL != hJdll) {
			FreeLibrary(hJdll);
			hJdll = NULL;
		}
		break;
	}
	return TRUE;
}

typedef void* (__stdcall *JInitType)       ();
typedef void  (__stdcall *JSMType)         (void*, void**);
typedef int   (__stdcall *JDoType)         (void*, C*);
typedef C*    (__stdcall *JGetLocaleType)  (void*);
typedef A     (__stdcall *JGetAType)       (void*, I, C*);
typedef int   (__stdcall *JGetMType)       (void*, C*, I*, I*, I*, I*);
typedef int   (__stdcall *JSetAType)       (void*, I, C*, I, C*);
typedef int   (__stdcall *JSetMType)       (void*, C*, I*, I*, I*, I*);
typedef A     (__stdcall *JgaType)         (void*, I, I, I, I*);
typedef int   (__stdcall *JErrorTextMType) (void*, I, I*);
typedef int   (__stdcall *JFreeType)       (void*);
typedef int   (__stdcall *JDoRType)        (void*, C*, VARIANT*);
typedef int   (__stdcall *JGetType)        (void*, C*, VARIANT*);
typedef int   (__stdcall *JGetBType)       (void*, C*, VARIANT*);
typedef int   (__stdcall *JSetType)        (void*, C*, VARIANT*);
typedef int   (__stdcall *JSetBType)       (void*, C*, VARIANT*);
typedef int   (__stdcall *JErrorTextType)  (void*, I, VARIANT*);
typedef int   (__stdcall *JErrorTextBType) (void*, I, VARIANT*);
typedef int   (__stdcall *JTransposeType)  (void*, I);
typedef int   (__stdcall *JBreakType)      (void*);
typedef int   (__stdcall *JClearType)      (void*);
typedef int   (__stdcall *JIsBusyType)     (void*);

static JInitType       jinit;
static JSMType         jsm;
static JDoType         jdo;
static JGetLocaleType  jgetlocale;
static JGetAType       jgeta;
static JGetMType       jgetm;
static JSetAType       jseta;
static JSetMType       jsetm;
static JgaType         jga;
static JErrorTextMType jerrortextm;
static JFreeType       jfree;
static JDoRType        jdor;
static JGetType        jget;
static JGetBType       jgetb;
static JSetType        jset;
static JSetBType       jsetb;
static JErrorTextType  jerrortext;
static JErrorTextBType jerrortextb;
static JTransposeType  jtranspose;
static JBreakType      jbreak;
static JClearType      jclear;
static JIsBusyType     jisbusy;

int __stdcall JInit()
{
	DWORD dwWaitResult;
	void** njts = NULL;

	dwWaitResult = WaitForSingleObject(jtMutex, INFINITE);
    switch (dwWaitResult)
    {
    case WAIT_OBJECT_0:
        __try {
			if (++jtIdx >= jtSz) {
				njts = (void**)calloc(jtSz + MEMCHUNCK, sizeof(void*));
				if (NULL != njts) {
					if (NULL != jts) {
						memcpy(njts, jts, jtSz * sizeof(void*));
						free(jts);
					}
					jts = njts;
					jtSz += MEMCHUNCK;
					jts[jtIdx] = jinit();
				}
				else {
					printf("calloc error: %d\n", errno);
					return -1;
				}
			}
        }
        __finally {
            if (!ReleaseMutex(jtMutex))
            {
				printf("Failed to release mutex. Error %d\n", GetLastError());
            }
        }
        break;
    case WAIT_ABANDONED:
        printf("Warning: abandoned mutex. Error %d\n", GetLastError());
		return -3;
    }
	return jtIdx;
}

void* GetJt(int idx) 
{
	void* jt = NULL;
	if (!(idx >= 0 && idx <= jtIdx)) {
		printf("Invalid session id %d passed.\n", idx);
		return NULL;
	}
	return jts[idx];
}

int __stdcall JSM(int idx, void* callbacks[])
{
	void* jt = GetJt(idx);
	if (NULL == jt) {
		return -1;
	}
	jsm(jt, callbacks);
	return idx;
}

int  __stdcall JDo(int idx, C* sentence)
{
	void* jt = GetJt(idx);
	if (NULL == jt) {
		return -1;
	}
	return jdo(jt, sentence);
}

C* __stdcall JGetLocale(int idx)
{
	void* jt = GetJt(idx);
	if (NULL == jt) {
		return NULL;
	}
	return jgetlocale(jt);
}

A __stdcall JGetA(int idx, I n, C* name)
{
	void* jt = GetJt(idx);
	if (NULL == jt) {
		return NULL;
	}
	return jgeta(jt, n, name);	
}

int __stdcall JGetM(int idx, C* name, I* jtype, I* jrank, I* jshape, I* jdata) 
{
	void* jt = GetJt(idx);
	if (NULL == jt) {
		return -1;
	}
	return jgetm(jt, name, jtype, jrank, jshape, jdata);
}

I __stdcall JSetA(int idx, I n, C* name, I dlen, C* d)
{
	void* jt = GetJt(idx);
	if (NULL == jt) {
		return -1;
	}
	return jseta(jt, n, name, dlen, d);
}

int __stdcall JSetM(int idx, C* name, I* jtype, I* jrank, I* jshape, I* jdata)
{
	void* jt = GetJt(idx);
	if (NULL == jt) {
		return -1;
	}
	return jsetm(jt, name, jtype, jrank, jshape, jdata);
}

A __stdcall Jga(int idx, I t, I n, I r, I*s)
{
	void* jt = GetJt(idx);
	if (NULL == jt) {
		return NULL;
	}
	return jga(jt, t, n, r, s);
}

int __stdcall JErrorTextM(int idx, I ec, I* p)
{
	void* jt = GetJt(idx);
	if (NULL == jt) {
		return -1;
	}
	return jerrortextm(jt, ec, p);
}

int __stdcall JFree(int idx)
{
	void* jt = GetJt(idx);
	if (NULL == jt) {
		return -1;
	}
	return jfree(jt);
}

int  __stdcall JDoR(int idx, C* sentence, VARIANT* v)
{
	void* jt = GetJt(idx);
	if (NULL == jt) {
		return -1;
	}
	return jdor(jt, sentence, v);
}

int  __stdcall JGet(int idx, C* name, VARIANT* v)
{
	void* jt = GetJt(idx);
	if (NULL == jt) {
		return -1;
	}
	return jget(jt, name, v);
}

int __stdcall JGetB(int idx, C* name, VARIANT* v)
{
	void* jt = GetJt(idx);
	if (NULL == jt) {
		return -1;
	}
	return jgetb(jt, name, v);
}

int __stdcall JSet(int idx, C* name, VARIANT* v)
{
	void* jt = GetJt(idx);
	if (NULL == jt) {
		return -1;
	}
	return jset(jt, name, v);
}

int __stdcall JSetB(int idx, C* name, VARIANT* v)
{
	void* jt = GetJt(idx);
	if (NULL == jt) {
		return -1;
	}
	return jsetb(jt, name, v);
}

int __stdcall JErrorText(int idx, I ec, VARIANT* v)
{
	void* jt = GetJt(idx);
	if (NULL == jt) {
		return -1;
	}
	return jerrortext(jt, ec, v);
}

int __stdcall JErrorTextB(int idx, I ec, VARIANT* v)
{
	void* jt = GetJt(idx);
	if (NULL == jt) {
		return -1;
	}
	return jerrortextb(jt, ec, v);
}

int __stdcall JTranspose(int idx, I b)
{
	void* jt = GetJt(idx);
	if (NULL == jt) {
		return -1;
	}
	return jtranspose(jt, b);
}

int __stdcall JBreak(int idx)
{
	void* jt = GetJt(idx);
	if (NULL == jt) {
		return -1;
	}
	return jbreak(jt);
}

int __stdcall JClear(int idx)
{
	void* jt = GetJt(idx);
	if (NULL == jt) {
		return -1;
	}
	return jclear(jt);
}

int __stdcall JIsBusy(int idx)
{
	void* jt = GetJt(idx);
	if (NULL == jt) {
		return -1;
	}
	return jisbusy(jt);
}

void* __stdcall JGetJt(int idx)
{
	return GetJt(idx);
}

C __stdcall JIncAdBreak(int idx) // Not sure what exactly this does
{
	char **adadbreak;
	void* jt = GetJt(idx);
	if (NULL == jt) {
		return '\0';
	}
	adadbreak=(char**)jt; // first address in jt is address of breakdata;
	**adadbreak += 1;
	return **adadbreak;
}

static BOOL GetJProcAddresses(HMODULE hModule) {
	CHAR* fsp;
	CHAR fullPath[MAX_PATH];

	GetModuleFileName(hModule, fullPath, MAX_PATH);
	fsp = strrchr(fullPath, '\\') + 1;
	strcpy_s(fsp, strlen(JDLLNAME) + 1, JDLLNAME);
	hJdll = LoadLibrary(fullPath);
	if (NULL == hJdll) {
		printf("Load library %s failed. Error %d.\n", fullPath, GetLastError());
		return FALSE;
	}
	if (FALSE == GetJProcAddress(&jinit, "JInit") ||
		FALSE == GetJProcAddress(&jsm, "JSM") ||
		FALSE == GetJProcAddress(&jdo, "JDo") ||
		FALSE == GetJProcAddress(&jgetlocale, "JGetLocale") ||
		FALSE == GetJProcAddress(&jgeta, "JGetA") ||
		FALSE == GetJProcAddress(&jgetm, "JGetM") ||
		FALSE == GetJProcAddress(&jseta, "JSetA") ||
		FALSE == GetJProcAddress(&jsetm, "JSetM") ||
		FALSE == GetJProcAddress(&jga, "Jga") ||
		FALSE == GetJProcAddress(&jerrortextm, "JErrorTextM") ||
		FALSE == GetJProcAddress(&jfree, "JFree") ||
		FALSE == GetJProcAddress(&jdor, "JDoR") ||
		FALSE == GetJProcAddress(&jget, "JGet") ||
		FALSE == GetJProcAddress(&jgetb, "JGetB") ||
		FALSE == GetJProcAddress(&jset, "JSet") ||
		FALSE == GetJProcAddress(&jsetb, "JSetB") ||
		FALSE == GetJProcAddress(&jerrortext, "JErrorText") ||
		FALSE == GetJProcAddress(&jerrortextb, "JErrorTextB") ||
		FALSE == GetJProcAddress(&jtranspose, "JTranspose") ||
		FALSE == GetJProcAddress(&jbreak, "JBreak") ||
		FALSE == GetJProcAddress(&jclear, "JClear") ||
		FALSE == GetJProcAddress(&jisbusy, "JIsBusy")) {
			return FALSE;
	}
	return TRUE;
}

static BOOL GetJProcAddress(void** func, char* name) {
	if (!(*func = GetProcAddress(hJdll, name))) {
		printf("Failed to get %s address. Error %d.\n", name, GetLastError());
		return FALSE;
	}
	return TRUE;
}