@echo off
rem Cheat del DVD: SOLO - activa o desactiva el modo solo esquinas
rem Funciona desde cualquier carpeta: se lo manda al DVD que este abierto.
title Cheat SOLO
tasklist /fi "imagename eq DvdRebote.exe" 2>nul | find /i "DvdRebote.exe" >nul
if errorlevel 1 (
    echo.
    echo   Abre el DVD primero ^(DvdRebote.exe^).
    timeout /t 3 >nul
    exit /b 1
)
(echo SOLO)>\\.\pipe\DvdRebote_Cheats 2>nul
if errorlevel 1 (
    echo.
    echo   No se pudo mandar el cheat. Prueba otra vez.
    timeout /t 3 >nul
    exit /b 1
)
echo.
echo   CHEAT SOLO ACTIVADO: activa o desactiva el modo solo esquinas
timeout /t 1 >nul
