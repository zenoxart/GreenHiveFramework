# GreenHive Language Settings Reset Script
# Version 1.0

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "GreenHive Lokalisierungs-Reset Tool" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

$settingsPath = Join-Path $env:APPDATA "GreenHive\Settings\LanguageSettings.json"

if (Test-Path $settingsPath) {
    Write-Host "Loesche Spracheinstellungen..." -ForegroundColor Yellow
    Remove-Item $settingsPath -Force
    Write-Host "[OK] Spracheinstellungen wurden erfolgreich geloescht." -ForegroundColor Green
    Write-Host ""
    Write-Host "Die Anwendung wird beim naechsten Start" -ForegroundColor White
    Write-Host "automatisch Deutsch als Standardsprache verwenden." -ForegroundColor White
} else {
    Write-Host "[INFO] Keine Spracheinstellungen gefunden." -ForegroundColor Blue
    Write-Host "Die Anwendung verwendet bereits die Standardsprache." -ForegroundColor White
}

Write-Host ""

# Optional: Zeige alle GreenHive-Settings
$settingsDir = Join-Path $env:APPDATA "GreenHive\Settings"
if (Test-Path $settingsDir) {
    Write-Host "Weitere Einstellungsdateien:" -ForegroundColor Cyan
    Get-ChildItem $settingsDir | ForEach-Object {
        $size = [math]::Round($_.Length / 1KB, 2)
        Write-Host "  - $($_.Name) (${size} KB)" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "Druecken Sie eine beliebige Taste zum Beenden..." -ForegroundColor White
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
