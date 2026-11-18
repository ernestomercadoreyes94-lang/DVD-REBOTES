@echo off
rem Cheat del DVD: RESET - reinicia el contador y quita todos los cheats
rem Funciona desde cualquier carpeta: se lo manda al DVD que este abierto.
title Cheat RESET
tasklist /fi "imagename eq DvdRebote.exe" 2>nul | find /i "DvdRebote.exe" >nul
if errorlevel 1 (
    echo.
    echo   Abre el DVD primero ^(DvdRebote.exe^).
    timeout /t 3 >nul
    exit /b 1
)
(echo RESET)>\\.\pipe\DvdRebote_Cheats 2>nul
if errorlevel 1 (
    echo.
    echo   No se pudo mandar el cheat. Prueba otra vez.
    timeout /t 3 >nul
    exit /b 1
)
echo.
echo   CHEAT RESET ACTIVADO: reinicia el contador y quita todos los cheats
timeout /t 1 >nul
