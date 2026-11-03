@echo off
cd /d "%~dp0"
set "CSC=%windir%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%windir%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
for %%F in (Glean.cs Setup.cs Glean.ico Glean.manifest) do (
  if not exist "%%F" (
    echo Missing file: %%F  ^(put all files in the same folder^)
    goto fail
  )
)
set "COMMON=/nologo /optimize /target:winexe /codepage:65001 /win32manifest:Glean.manifest /win32icon:Glean.ico /resource:Glean.ico,Glean.ico /reference:System.Windows.Forms.dll /reference:System.Drawing.dll"

echo Closing any running Glean ...
taskkill /f /im Glean.exe >nul 2>&1

echo [1/2] Compiling Glean.exe ...
"%CSC%" %COMMON% /out:Glean.exe Glean.cs
if errorlevel 1 goto fail

echo [2/2] Building installer Glean-Setup.exe ...
"%CSC%" %COMMON% /main:Glean.SetupProgram /resource:Glean.exe,payload.exe /out:Glean-Setup.exe Glean.cs Setup.cs
if errorlevel 1 goto fail

echo.
echo Done!  Installer: %~dp0Glean-Setup.exe
echo Upload it to a GitHub Release (keep the file name Glean-Setup.exe) so auto-update can find it.
pause
exit /b 0

:fail
echo.
echo Build FAILED. Please send the error messages above to Claude.
pause
exit /b 1
