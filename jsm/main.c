#include "jsm.h"
#include <io.h>
#include <signal.h>

// output type
#define MTYOEXIT    5    /* exit */

// smoptions
#define SMCON    3  /* jconsole */

void __stdcall Joutput(void*, int, char*);
char* __stdcall Jinput(void*, char*);
char* jepath();
int jefirst(int, char*);
void addargv(int, char**, C*);


static int sid = -1;
static void sigint(int k){JIncAdBreak(sid);signal(SIGINT,sigint);}
static char input[30000];

int main(int argc, char* argv[]){
    void* callbacks[] = {Joutput, 0, Jinput, 0, (void*)SMCON};
    int type;

    sid = JInit();
    if (sid < 0) {
        puts("Library initialization failure.\n");
        exit(1);
    }
    JSM(sid, callbacks);
    signal(SIGINT,sigint);
    if(argc==2&&!strcmp(argv[1],"-jprofile"))
        type=3;
    else if(argc>2&&!strcmp(argv[1],"-jprofile"))
        type=1;
    else
        type=0;
    addargv(argc,argv,input+strlen(input));
    jefirst(type,input);

    while(1){
        JDo(sid, Jinput(NULL ,"   "));
    }
    JFree(sid);
}

void addargv(int argc, char* argv[], C* d)
{
    C *p,*q; I i;

    p=d+strlen(d);
    for(i=0;i<argc;++i)
    {
        if(sizeof(input)<(100+strlen(d)+2*strlen(argv[i]))) exit(100);
        if(1==argc){*p++=',';*p++='<';}
        if(i)*p++=';';
        *p++='\'';
        q=argv[i];
        while(*q)
        {
            *p++=*q++;
            if('\''==*(p-1))*p++='\'';
        }
        *p++='\'';
    }
    *p=0;
}

static char* jepath()
{
    char* path;
    path = (char*)malloc(MAX_PATH * sizeof(char));
    GetModuleFileName(0, path, MAX_PATH);
    *strrchr(path, '\\') = '\0';
    return path;
}

static int jefirst(int type, char* arg)
{
    int r; char* p,*q;
    char* input= (char*)malloc(2000+strlen(arg));
    *input=0;
    if(0==type)
    {
        strcat(input,"(3 : '0!:0 y')<BINPATH,'");
        strcat(input,"\\");
        strcat(input,"profile.ijs'");
    }
    else if(1==type)
        strcat(input,"(3 : '0!:0 y')2{ARGV");
    else if(2==type)
        strcat(input,"11!:0'pc ijx closeok;xywh 0 0 300 200;cc e editijx rightmove bottommove ws_vscroll ws_hscroll;setfont e \"Courier New\" 12;setfocus e;pas 0 0;pgroup jijx;pshow;'[18!:4<'base'");
    else
        strcat(input,"i.0 0");
    strcat(input,"[ARGV_z_=:");
    strcat(input,arg);
    strcat(input,"[BINPATH_z_=:'");
    p=jepath();
    q=input+strlen(input);
    while(*p)
    {
        if(*p=='\'') *q++='\'';    // 's doubled
        *q++=*p++;
    }
    *q=0;
    strcat(input,"'");
    r=JDo(sid, input);
    free(input);
    return r;
}

void __stdcall Joutput(void* jt, int type, char* s)
{
    if(MTYOEXIT==type)
    {
        exit((int)(I)s);
    }
    fputs(s,stdout);
    fflush(stdout);
}

char* __stdcall Jinput(void* jt, char* prompt){
    fputs(prompt,stdout);
    fflush(stdout);
    if(!fgets(input, sizeof(input), stdin))
    {
        if(!_isatty(_fileno(stdin))) return "2!:55''";
        fputs("\n",stdout);
        fflush(stdout);
        JIncAdBreak(sid);
    }
    return input;
}