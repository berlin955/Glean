@echo off
cd /d "%~dp0"
set "CSC=%windir%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%windir%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
set "EXTRA="
if exist Glean.manifest set "EXTRA=%EXTRA% /win32manifest:Glean.manifest"
if exist Glean.ico set "EXTRA=%EXTRA% /win32icon:Glean.ico /resource:Glean.ico,Glean.ico"
echo Closing any running Glean ...
taskkill /f /im Glean.exe >nul 2>&1
echo Compiling Glean.cs ...
"%CSC%" /nologo /optimize /target:winexe /codepage:65001 %EXTRA% /out:Glean.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll Glean.cs
if errorlevel 1 goto fail
echo.
echo Build OK. Starting Glean.exe ...
start "" "%~dp0Glean.exe"
pause
exit /b 0
:fail
echo.
echo Build FAILED. Glean.exe was NOT updated.
echo Please send the error messages above to Claude.
pause
exit /b 1
