@echo off
rem Автоматично проходить усі 13 пунктів меню (Enter = значення за замовчуванням)
rem і зберігає вивід кожного в logs\demo-NN.txt.
chcp 65001 >nul
cd /d "%~dp0.."
if not exist logs mkdir logs
dotnet build SubTracker.slnx -nologo -v q
set EXE=src\SubTracker.Console\bin\Debug\net10.0\SubTracker.Console.exe
for %%i in (01 04 05 06 02 03 07 08 09 10 11 12 13) do (
  "%EXE%" < scripts\demo-input\%%i.txt > logs\demo-%%i.txt 2>&1
  echo Пункт %%i — logs\demo-%%i.txt
)
pause
