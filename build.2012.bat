@echo off
call "C:\Program Files (x86)\Microsoft Visual Studio 11.0\Common7\Tools\VsDevCmd.bat"
msbuild J.sln /t:Clean /p:Configuration=Release,Platform=x64
msbuild J.sln /t:Build /p:Configuration=Release,Platform=x64