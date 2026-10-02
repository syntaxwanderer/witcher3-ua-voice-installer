@echo off
rem Builds UA_Voice_Setup.exe (GUI) and UA_Voice_Setup_cli.exe (console, for tests) with the C# compiler
rem that ships with Windows (.NET Framework 4). No third-party libraries. Run from any folder.
cd /d "%~dp0"
if exist ..\pkg\W3UA.cs copy /y ..\pkg\W3UA.cs W3UA.cs >nul
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set REFS=/r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll
set SRC=AssemblyInfo.cs Setup.cs Program.cs W3UA.cs
%CSC% /nologo /codepage:65001 /optimize+ /platform:anycpu /win32manifest:app.manifest /target:winexe /out:UA_Voice_Setup.exe %REFS% %SRC% > build.log 2>&1
%CSC% /nologo /codepage:65001 /optimize+ /platform:anycpu /win32manifest:app.manifest /target:exe /out:UA_Voice_Setup_cli.exe %REFS% %SRC% >> build.log 2>&1
echo exit %errorlevel% >> build.log
