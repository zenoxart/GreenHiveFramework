@echo off
echo ================================================
echo Zenox Lokalisierungs-Scanner
echo ================================================
echo.

set "projectPath=%~dp0.."

echo Scanne Projekt: %projectPath%
echo.

dotnet run --project "%projectPath%\Zenox.Wpf.UI.csproj" -- scan-localization

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo FEHLER: Scanner konnte nicht ausgefuehrt werden
    echo Verwenden Sie stattdessen die integrierte Funktion im Debug-Menue
    pause
    exit /b 1
)

echo.
echo Scan abgeschlossen!
echo Report wurde im Docs-Ordner erstellt.
echo.

pause
