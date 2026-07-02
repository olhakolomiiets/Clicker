@echo off
setlocal

set "PROJECT_ROOT=%~dp0"
pushd "%PROJECT_ROOT%" >nul

set "PYTHON_LAUNCHER="
set "CHECKED_LAUNCHERS=python, py -3.13, py -3, py"

python --version >nul 2>&1
if %errorlevel%==0 (
    set "PYTHON_LAUNCHER=python"
    goto :run_orchestrator
)

py -3.13 --version >nul 2>&1
if %errorlevel%==0 (
    set "PYTHON_LAUNCHER=py -3.13"
    goto :run_orchestrator
)

py -3 --version >nul 2>&1
if %errorlevel%==0 (
    set "PYTHON_LAUNCHER=py -3"
    goto :run_orchestrator
)

py --version >nul 2>&1
if %errorlevel%==0 (
    set "PYTHON_LAUNCHER=py"
    goto :run_orchestrator
)

echo ERROR: No working Python launcher was found.
echo Checked launchers: %CHECKED_LAUNCHERS%
echo Install Python 3 or make one of these commands available, then run this script again.
echo.
pause
popd >nul
exit /b 1

:run_orchestrator
set "ORCHESTRATOR_ARGS=%*"
if "%ORCHESTRATOR_ARGS%"=="" set "ORCHESTRATOR_ARGS=--dry-run"
echo Using Python launcher: %PYTHON_LAUNCHER%
%PYTHON_LAUNCHER% "CodexAutomation\scripts\orchestrator.py" %ORCHESTRATOR_ARGS%
set "ORCHESTRATOR_EXIT_CODE=%errorlevel%"
if not "%ORCHESTRATOR_EXIT_CODE%"=="0" (
    echo.
    echo Codex automation exited with code %ORCHESTRATOR_EXIT_CODE%.
    pause
)
popd >nul
exit /b %ORCHESTRATOR_EXIT_CODE%
