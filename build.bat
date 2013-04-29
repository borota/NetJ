@echo off
call "C:\Program Files (x86)\Microsoft Visual Studio 10.0\VC\vcvarsall.bat" x86
msbuild J.sln /t:Clean /p:Configuration=Release,Platform=x64
msbuild J.sln /t:Build /p:Configuration=Release,Platform=x64
