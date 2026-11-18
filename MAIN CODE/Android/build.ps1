# Compila y firma DvdRebote.apk sin Gradle (aapt2 + javac + d8 + zipalign + apksigner)
# Uso: powershell -ExecutionPolicy Bypass -File build.ps1 [-Destino carpeta]
param([string]$Destino = "$env:USERPROFILE\OneDrive\Desktop\DVD\apks")

$here = $PSScriptRoot
$jdk  = Get-ChildItem "$env:LOCALAPPDATA\Programs\Microsoft" -Directory -Filter "jdk-*" | Sort-Object Name -Descending | Select-Object -First 1 -ExpandProperty FullName
$sdk  = "$env:LOCALAPPDATA\Android\Sdk"
$bt   = "$sdk\build-tools\35.0.0"
$jar  = "$sdk\platforms\android-35\android.jar"
$env:JAVA_HOME = $jdk
$env:Path = "$jdk\bin;$env:Path"
$out = "$here\build"

function Check($what) { if ($LASTEXITCODE -ne 0) { Write-Host "FALLO: $what" -ForegroundColor Red; exit 1 } }

if (Test-Path $out) { Remove-Item $out -Recurse -Force -Confirm:$false }
New-Item -ItemType Directory -Force $out, "$out\gen", "$out\classes", "$out\dex" | Out-Null

# Versión: cambia en cada compilación para que Android siempre actualice la app
$versionCode = [int]([DateTimeOffset]::UtcNow.ToUnixTimeSeconds() / 60)
$versionName = "4.5 (" + (Get-Date -Format "dd/MM/yyyy HH:mm") + ")"

# 1. Recursos y manifest
& "$bt\aapt2.exe" compile --dir "$here\res" -o "$out\res.zip"; Check "aapt2 compile"
& "$bt\aapt2.exe" link -o "$out\base.apk" -I $jar --manifest "$here\AndroidManifest.xml" `
    --min-sdk-version 26 --target-sdk-version 35 --version-code $versionCode --version-name $versionName `
    --java "$out\gen" "$out\res.zip"; Check "aapt2 link"

# 2. Java -> .class
$src = Get-ChildItem "$here\src", "$out\gen" -Recurse -Filter *.java | ForEach-Object FullName
& javac --release 11 -encoding UTF-8 -nowarn -classpath $jar -d "$out\classes" $src; Check "javac"

# 3. .class -> classes.dex
# (se empacan en un .jar: con muchas clases la lista ya no cabía en la línea de comandos de Windows)
& jar cf "$out\classes.jar" -C "$out\classes" .; Check "jar"
& "$bt\d8.bat" --release --min-api 26 --lib $jar --output "$out\dex" "$out\classes.jar"; Check "d8"

# 4. Meter classes.dex en la APK
Push-Location "$out\dex"
& "$bt\aapt.exe" add "$out\base.apk" classes.dex | Out-Null; $rc = $LASTEXITCODE
Pop-Location
$LASTEXITCODE = $rc; Check "aapt add"

# 5. Alinear y firmar
& "$bt\zipalign.exe" -f -p 4 "$out\base.apk" "$out\aligned.apk"; Check "zipalign"

$props = "$here\keystore.properties"
if (-not (Test-Path "$here\dvd.keystore")) {
    $chars = [char[]]'abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789'
    $pw = -join (1..24 | ForEach-Object { $chars[(Get-Random -Maximum $chars.Length)] })
    Set-Content -Path $props -Value "storePassword=$pw" -Encoding ASCII
    & keytool -genkeypair -keystore "$here\dvd.keystore" -alias dvd -keyalg RSA -keysize 2048 -validity 10000 `
        -storepass $pw -keypass $pw -dname "CN=DVD Rebote" 2>$null | Out-Null; Check "keytool"
}
$pw = ((Get-Content $props) -replace '^storePassword=', '').Trim()
& "$bt\apksigner.bat" sign --ks "$here\dvd.keystore" --ks-pass "pass:$pw" --key-pass "pass:$pw" `
    --out "$out\DvdRebote.apk" "$out\aligned.apk"; Check "apksigner sign"
& "$bt\apksigner.bat" verify "$out\DvdRebote.apk"; Check "apksigner verify"

New-Item -ItemType Directory -Force $Destino | Out-Null
Copy-Item "$out\DvdRebote.apk" "$Destino\DvdRebote.apk" -Force
Write-Host ("APK lista: {0}\DvdRebote.apk ({1:N0} KB)" -f $Destino, ((Get-Item "$Destino\DvdRebote.apk").Length / 1KB)) -ForegroundColor Green
