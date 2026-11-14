@echo off
echo ================================================
echo GreenHive Lokalisierungs-Reset Tool
echo ================================================
echo.
echo Dieses Tool setzt die Spracheinstellungen zurueck.
echo.

set "settingsPath=%APPDATA%\GreenHive\Settings\LanguageSettings.json"

if exist "%settingsPath%" (
    echo Loesche Spracheinstellungen...
    del "%settingsPath%"
    echo [OK] Spracheinstellungen wurden geloescht.
    echo.
    echo Die Anwendung wird beim naechsten Start
    echo automatisch Deutsch als Standardsprache verwenden.
) else (
    echo [INFO] Keine Spracheinstellungen gefunden.
    echo Die Anwendung verwendet bereits die Standardsprache.
)

echo.
echo ================================================
pause
