@echo off
rem Видаляє БД SubTrackerDb і створює її заново з усіх міграцій (з тестовими даними).
chcp 65001 >nul
cd /d "%~dp0.."
set EF=--project src\SubTracker.DB --startup-project src\SubTracker.Console
dotnet tool restore
dotnet ef database drop --force %EF%
dotnet ef database update %EF%
pause
