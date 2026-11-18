@echo off
rem Activa o desactiva el modo solo esquinas del DVD (cada vez que lo abres cambia)
rem Funciona en cualquier carpeta: busca el DVD junto a este .bat, en la carpeta de arriba
rem o donde se abrio DvdRebote.exe / dvd_config.exe por ultima vez.
title Modo solo esquinas
setlocal

rem Revisa que este instalado .NET Framework 4.5 o mas nuevo
set RELNUM=0
set REL=
for /f "tokens=3" %%a in ('reg query "HKLM\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" /v Release 2^>nul ^| find "Release"') do set REL=%%a
if defined REL set /a RELNUM=%REL% 2>nul
if %RELNUM% LSS 378389 (
    echo.
    echo   Necesitas .NET Framework para usar esta app.
    echo   Instala .NET Framework 4.8 desde la pagina de Microsoft:
    echo   https://dotnet.microsoft.com/download/dotnet-framework/net48
    echo.
    pause
    exit /b 1
)

rem Buscar la carpeta del DVD
set "DVD="
if exist "%~dp0dvd_config.exe" set "DVD=%~dp0"
if not defined DVD if exist "%~dp0..\dvd_config.exe" set "DVD=%~dp0..\"
if not defined DVD if exist "%~dp0..\.exe\dvd_config.exe" set "DVD=%~dp0..\.exe\"
if not defined DVD if exist "%~dp0..\exe\dvd_config.exe" set "DVD=%~dp0..\exe\"
if not defined DVD for /f "tokens=2,*" %%a in ('reg query "HKCU\Software\DvdRebote" /v Carpeta 2^>nul ^| find "Carpeta"') do set "DVD=%%b"
if defined DVD if not "%DVD:~-1%"=="\" set "DVD=%DVD%\"
if defined DVD if not exist "%DVD%dvd_config.exe" set "DVD="
if not defined DVD (
    echo.
    echo   No encuentro dvd_config.exe.
    echo   Abre dvd_config.exe una vez y vuelve a abrir este .bat.
    timeout /t 4 >nul
    exit /b 1
)

rem DvdRebote.exe puede estar en otra carpeta: junto a dvd_config.exe o donde se abrio por ultima vez
set "REBOTE=%DVD%DvdRebote.exe"
if not exist "%REBOTE%" for /f "tokens=2,*" %%a in ('reg query "HKCU\Software\DvdRebote" /v Rebote 2^>nul ^| find "Rebote"') do set "REBOTE=%%b"

rem Avisar al DVD de donde esta este .bat (asi cuenta como instalado aunque este en otra carpeta)
reg add "HKCU\Software\DvdRebote" /v Bat /t REG_SZ /d "%~f0" /f >nul 2>&1

start "" /wait "%DVD%dvd_config.exe" --alternar-solo-esquinas
set R=%errorlevel%
if "%R%"=="1" (
    echo.
    echo   MODO SOLO ESQUINAS: ACTIVADO
    echo   El DVD ahora solo toca esquinas.
    if exist "%REBOTE%" start "" "%REBOTE%"
) else if "%R%"=="0" (
    echo.
    echo   MODO SOLO ESQUINAS: DESACTIVADO
    echo   El DVD vuelve a rebotar normal.
) else (
    echo.
    echo   No se pudo cambiar el modo.
)
timeout /t 2 >nul
