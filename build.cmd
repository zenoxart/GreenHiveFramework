@echo off
rem =============================================================================
rem Green Hive - build everything (host, framework, modules, backend) on Windows.
rem   build.cmd            Debug build
rem   build.cmd Release    Release build
rem The log is written to artifacts\build.log; development modules are staged to artifacts\modules\.
rem =============================================================================
setlocal
set CONFIG=%1
if "%CONFIG%"=="" set CONFIG=Debug
cd /d "%~dp0"
if not exist artifacts mkdir artifacts
echo Building Green Hive (%CONFIG%) ...
dotnet build GreenHiveFramework.slnx -c %CONFIG% -nologo -clp:Summary -flp:logfile=artifacts\build.log;verbosity=minimal
set RESULT=%ERRORLEVEL%
if %RESULT%==0 (
  echo.
  echo Build succeeded. Start the client:  dotnet run --project src\GreenHive.Wpf.UI -c %CONFIG%
) else (
  echo.
  echo Build failed - see artifacts\build.log
)
exit /b %RESULT%
