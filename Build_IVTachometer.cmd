@echo off
rem Builds IVTachometer.net.dll with the C# compiler that ships with Windows.
rem Usage: Build_IVTachometer.cmd "C:\Path\To\ScriptHookDotNet.dll"   (or drag the dll onto this file)
rem If a copy of ScriptHookDotNet.dll sits next to this file, it is used without asking.
rem Everything is also written to build.txt next to this file.
setlocal
pushd "%~dp0"

set "HOOK=%~1"
if "%HOOK%"=="" if exist "%~dp0ScriptHookDotNet.dll" set "HOOK=%~dp0ScriptHookDotNet.dll"
if "%HOOK%"=="" set /p HOOK=Path to ScriptHookDotNet.dll: 
set "HOOK=%HOOK:"=%"

set "CSC="
if exist "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not defined CSC if exist "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not defined CSC goto nocsc
if not exist "%HOOK%" goto nohook
if not exist "src\IVTachometer.cs" goto nosrc

echo Using %CSC%
echo Source  %CD%\src\IVTachometer.cs
echo Hook    %HOOK%
echo.
"%CSC%" /nologo /target:library /platform:x86 /optimize+ /out:IVTachometer.net.dll /reference:"%HOOK%" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "src\IVTachometer.cs" > build.txt 2>&1
set RESULT=%ERRORLEVEL%
type build.txt
echo.
if not "%RESULT%"=="0" goto failed
echo BUILD OK: %CD%\IVTachometer.net.dll
echo Copy it and the IVTachometer folder into your GTA IV "scripts" folder.
goto end

:nocsc
echo Could not find csc.exe under %WINDIR%\Microsoft.NET\Framework\v4.0.30319
goto failed
:nohook
echo ScriptHookDotNet.dll not found at: %HOOK%
goto failed
:nosrc
echo Could not find src\IVTachometer.cs next to Build.bat (looked in %CD%)
goto failed
:failed
echo BUILD FAILED. The text above is also in build.txt - send that file.
:end
popd
pause
