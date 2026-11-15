@echo off
REM Build ScholarTray.exe (C# 5 / .NET Framework 4.x, no third-party deps)
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo ERROR: csc.exe not found
  exit /b 1
)

set "REFS=/r:System.dll /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Management.dll"
set "RES=/resource:src\Resources\AppIcon.ico,ScholarTray.Resources.AppIcon.ico /resource:src\Resources\AppIconOff.ico,ScholarTray.Resources.AppIconOff.ico"
set "SRC=/recurse:src\*.cs"

echo [1/2] Building ScholarTray.exe ...
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 ^
  /main:ScholarTray.Program /out:ScholarTray.exe /win32manifest:ScholarTray.manifest ^
  %REFS% %RES% %SRC%
if errorlevel 1 (
  echo ERROR: main build failed
  exit /b 1
)

echo [2/2] Building ScholarTray.SelfTest.exe (read-only, no elevation) ...
"%CSC%" /nologo /target:exe /platform:anycpu /codepage:65001 ^
  /main:ScholarTray.SelfTest /out:ScholarTray.SelfTest.exe %REFS% %RES% %SRC%
if errorlevel 1 (
  echo ERROR: selftest build failed
  exit /b 1
)

echo BUILD OK
exit /b 0
