@echo off
rem Builds NetPanel.exe with the C# compiler that ships with Windows (.NET Framework 4.x). No SDK needed.
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (echo csc.exe not found - .NET Framework 4.x is required. & exit /b 1)
pushd "%~dp0"
"%CSC%" /nologo /target:winexe /optimize /nowarn:162 /out:NetPanel.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll /r:System.Management.dll NetPanel.cs
set ERR=%ERRORLEVEL%
popd
if %ERR%==0 (echo Built NetPanel.exe) else (echo Build failed)
exit /b %ERR%
