@echo off
title TaskTune AI Launcher
cls
echo ========================================================
echo       TaskTune AI - Agentic Work Execution Platform
echo ========================================================
echo.
echo [1/4] Checking SQL Server connection...
sqlcmd -S .\SQLEXPRESS -E -C -Q "SELECT name FROM sys.databases WHERE name='TaskTuneAIDatabase'" >nul 2>&1
if %errorlevel% neq 0 (
    echo [WARNING] Could not verify TaskTuneAIDatabase in SQLEXPRESS.
) else (
    echo [OK] TaskTuneAIDatabase active on .\SQLEXPRESS.
)

echo.
echo [2/4] Starting 5 Python AI Agents (Ports 8001, 5050, 8003, 8004, 8005)...
start "TaskTune Agents" cmd /k "cd /d %~dp0tasktune-agents && python start_all_agents.py"

echo [3/4] Starting .NET 8 Web API (Port 5000)...
start "TaskTune API (Port 5000)" cmd /k "cd /d %~dp0tasktune-backend\TaskTuneAPI\publish && TaskTune.API.exe"

echo [4/4] Starting React UI (Port 5173)...
start "TaskTune UI (Port 5173)" cmd /k "cd /d %~dp0tasktune-frontend && cmd /c npm run dev -- --host"

timeout /t 5 /nobreak >nul
start http://localhost:5173
echo.
echo ========================================================
echo  TaskTune AI is running!
echo    UI Frontend : http://localhost:5173
echo    Backend API : http://localhost:5000/api
echo    Agent 1 (Chatbot)  : http://localhost:8001/docs
echo    Agent 2 (Guidance) : http://localhost:5050/docs
echo    Agent 3 (Tracking) : http://localhost:8003/docs
echo    Agent 4 (Report)   : http://localhost:8004/docs
echo    Agent 5 (Email)    : http://localhost:8005/docs
echo.
echo    Login Credentials (Password for all: Password123!):
echo      Admin      : admin@tasktune.ai
echo      Manager    : manager@tasktune.ai
echo      Employee   : employee@tasktune.ai
echo      Supervisor : supervisor@tasktune.ai
echo      CEO        : ceo@tasktune.ai
echo      HR         : hr@tasktune.ai
echo ========================================================
echo.
echo Press any key to stop all TaskTune servers...
pause >nul
taskkill /fi "WINDOWTITLE eq TaskTune Agents*" /f >nul 2>&1
taskkill /fi "WINDOWTITLE eq TaskTune API*" /f >nul 2>&1
taskkill /fi "WINDOWTITLE eq TaskTune UI*" /f >nul 2>&1
echo Done.
