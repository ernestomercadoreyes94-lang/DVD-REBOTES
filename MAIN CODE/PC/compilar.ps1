# Compila DvdRebote.exe y dvd_config.exe en la carpeta "salida" (junto a este archivo).
# Usa el compilador de C# que ya trae Windows (.NET Framework 4).
Set-Location $PSScriptRoot
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
New-Item -ItemType Directory -Force salida | Out-Null

$recursos = 'logo_dvd.png','trofeo_bronce.png','trofeo_plata.png','trofeo_oro.png','trofeo_diamante.png','troll.png','escudo_viejo.png','cursor_noescape.cur','icono_cmd.ico' | ForEach-Object { "/resource:$_,$_" }
& $csc /nologo /target:winexe /optimize+ /out:salida\DvdRebote.exe /win32icon:dvd.ico /win32manifest:dvd.manifest /r:System.Web.Extensions.dll @recursos DvdRebote.cs DvdConfig.cs
if ($LASTEXITCODE -ne 0) { Write-Host 'Error al compilar DvdRebote.exe' -ForegroundColor Red; exit 1 }

$recursos2 = 'logo_dvd.png','trofeo_bronce.png','trofeo_plata.png','trofeo_oro.png','trofeo_diamante.png' | ForEach-Object { "/resource:$_,$_" }
& $csc /nologo /target:winexe /optimize+ /out:salida\dvd_config.exe /win32icon:dvd.ico /r:System.Web.Extensions.dll @recursos2 DvdConfigApp.cs DvdConfig.cs InfoConfig.cs Actualizador.cs
if ($LASTEXITCODE -ne 0) { Write-Host 'Error al compilar dvd_config.exe' -ForegroundColor Red; exit 1 }

Write-Host 'Listo: salida\DvdRebote.exe y salida\dvd_config.exe' -ForegroundColor Green
