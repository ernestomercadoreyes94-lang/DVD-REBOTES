@echo off
rem Cheat del DVD: PARPADEO - los DVDs aparecen y desaparecen (abrelo otra vez para quitarlo)
rem Funciona desde cualquier carpeta: se lo manda al DVD que este abierto.
title Cheat PARPADEO
tasklist /fi "imagename eq DvdRebote.exe" 2>nul | find /i "DvdRebote.exe" >nul
if errorlevel 1 (
    echo.
    echo   Abre el DVD primero ^(DvdRebote.exe^).
    timeout /t 3 >nul
    exit /b 1
)
(echo PARPADEO)>\\.\pipe\DvdRebote_Cheats 2>nul
if errorlevel 1 (
    echo.
    echo   No se pudo mandar el cheat. Prueba otra vez.
    timeout /t 3 >nul
    exit /b 1
)
echo.
echo   CHEAT PARPADEO ACTIVADO: los DVDs aparecen y desaparecen (abrelo otra vez para quitarlo)
timeout /t 1 >nul
