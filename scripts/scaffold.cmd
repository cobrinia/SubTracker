@echo off
rem Reverse engineering існуючої БД в окремий проєкт SubTracker.Scaffolded.
chcp 65001 >nul
cd /d "%~dp0.."
dotnet tool restore
dotnet ef dbcontext scaffold "Server=(localdb)\MSSQLLocalDB;Database=SubTrackerDb;Trusted_Connection=True;TrustServerCertificate=True" Microsoft.EntityFrameworkCore.SqlServer --project src\SubTracker.Scaffolded --startup-project src\SubTracker.Scaffolded --context ScaffoldedDbContext --context-dir . --output-dir Entities --namespace SubTracker.Scaffolded.Entities --context-namespace SubTracker.Scaffolded --no-onconfiguring --force
pause
