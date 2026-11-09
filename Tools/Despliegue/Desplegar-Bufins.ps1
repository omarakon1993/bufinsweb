<#
.SYNOPSIS
  Respalda, publica y restaura el sitio bufins.com con Web Deploy (msdeploy), el mismo mecanismo que usaba Visual Studio.

.DESCRIPTION
  Cuatro acciones:
    SubirSecretos  Sube secretsSettings.json (credenciales SMTP) al sitio; solo agrega/actualiza ese archivo.
    Respaldar  Descarga una copia COMPLETA del sitio actual a una carpeta local (solo lee; no cambia nada en el servidor).
    Publicar   Sube el paquete .NET 10 al sitio. Sin -Aplicar solo SIMULA y muestra que crearia, cambiaria y borraria.
    Restaurar  Devuelve el sitio al estado de un respaldo (vuelta atras). Sin -Aplicar solo simula.

  La contrasena de Web Deploy se pide por pantalla (no se guarda ni se escribe en ningun archivo).
  No toca App_Data\pulso\uso (analitica anonima del servidor), logs ni secretsSettings.json del servidor al publicar.

.EXAMPLE
  .\Desplegar-Bufins.ps1 -Accion Respaldar
  .\Desplegar-Bufins.ps1 -Accion Publicar            # simulacion
  .\Desplegar-Bufins.ps1 -Accion Publicar -Aplicar   # publica de verdad
  .\Desplegar-Bufins.ps1 -Accion Restaurar -Respaldo C:\Users\omara_\bufins-despliegue\respaldo-20261107-1530 -Aplicar
#>
param(
    [Parameter(Mandatory = $true)][ValidateSet('Respaldar', 'Publicar', 'SubirSecretos', 'Restaurar')][string]$Accion,
    [string]$Paquete = 'C:\Users\omara_\bufins-despliegue\paquete-net10',
    [string]$Respaldo,
    [string]$Secretos,
    [string]$Servicio = 'https://bufins.com:8172/msdeploy.axd?site=bufins.com',
    [string]$Sitio = 'bufins.com',
    [string]$Usuario = 'oglearni',
    [switch]$Aplicar,
    [switch]$ConservarArchivosExtra
)

$ErrorActionPreference = 'Stop'
if (-not $Secretos) {
    $carpetaScript = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
    $Secretos = Join-Path (Split-Path (Split-Path $carpetaScript -Parent) -Parent) 'secretsSettings.json'
}
$carpetaBase = 'C:\Users\omara_\bufins-despliegue'

$msdeploy = @('C:\Program Files\IIS\Microsoft Web Deploy V3\msdeploy.exe', 'C:\Program Files (x86)\IIS\Microsoft Web Deploy V3\msdeploy.exe') |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $msdeploy) { throw 'No se encontro msdeploy.exe (Web Deploy 3.6). Instale Microsoft Web Deploy.' }

# --- contrasena (solo en memoria) ---
$seg = Read-Host -Prompt "Contrasena de Web Deploy del usuario $Usuario" -AsSecureString
$ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($seg)
try { $clave = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) } finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
if ([string]::IsNullOrEmpty($clave)) { throw 'La contrasena no puede estar vacia.' }
if ($clave.Contains('"')) { throw 'La contrasena contiene comillas dobles; cambiela o use otra via.' }

# Conexion al servidor (se usa como origen o destino segun la accion)
$conexion = "computerName=`"$Servicio`",userName=$Usuario,password=`"$clave`",authType=Basic"

# Rutas y archivos del servidor que NUNCA se tocan al publicar
$omitir = @(
    '-skip:objectName=dirPath,absolutePath=".*\\App_Data\\pulso\\uso$"',
    '-skip:objectName=dirPath,absolutePath=".*\\App_Data\\dpkeys$"',    # llaves de proteccion de datos (antiforgery): no borrar
    '-skip:objectName=dirPath,absolutePath=".*\\logs$"',
    '-skip:objectName=filePath,absolutePath=".*\\secretsSettings\.json$"',
    '-skip:objectName=filePath,absolutePath=".*\\\.user\.ini$"'    # archivo de Plesk: no se toca
)

$sello = Get-Date -Format 'yyyyMMdd-HHmm'
$registro = Join-Path $carpetaBase ("registro-$Accion-$sello.txt")
New-Item -ItemType Directory -Force $carpetaBase | Out-Null

function Ejecutar([string[]]$argumentos, [string]$descripcion) {
    Write-Host ""
    Write-Host "== $descripcion" -ForegroundColor Cyan
    # el registro no lleva la contrasena
    ($argumentos -join ' ') -replace 'password="[^"]*"', 'password="***"' | Out-File $registro -Append -Encoding utf8
    & $msdeploy @argumentos 2>&1 | Tee-Object -FilePath $registro -Append
    if ($LASTEXITCODE -ne 0) { throw "msdeploy termino con codigo $LASTEXITCODE. Revise $registro" }
}

switch ($Accion) {

    'Respaldar' {
        $destino = Join-Path $carpetaBase "respaldo-$sello"
        Ejecutar @('-verb:sync', "-source:contentPath=`"$Sitio`",$conexion", "-dest:archiveDir=`"$destino`"", '-allowUntrusted') "Descargando copia completa de $Sitio a $destino"
        $f = Get-ChildItem $destino -Recurse -File -ErrorAction SilentlyContinue
        Write-Host ""
        Write-Host ("Respaldo listo: {0} archivos, {1:N1} MB en {2}" -f $f.Count, (($f | Measure-Object Length -Sum).Sum / 1MB), $destino) -ForegroundColor Green
        Write-Host "Verifique que existan: web.config, bin\ y App_Data\pulso\uso\ (si el sitio los tenia)."
    }

    'Publicar' {
        if (-not (Test-Path (Join-Path $Paquete 'web.config'))) { throw "El paquete no es valido (falta web.config): $Paquete" }
        if (Get-ChildItem $Paquete -Recurse -File | Where-Object { $_.Name -like 'secretsSettings*' -and $_.Name -notlike '*.example' }) {
            throw 'El paquete contiene secretsSettings*: no se publica. Genere el paquete de nuevo.'
        }
        $args2 = @('-verb:sync', "-source:contentPath=`"$Paquete`"", "-dest:contentPath=`"$Sitio`",$conexion", '-allowUntrusted',
                   '-enableRule:AppOffline') + $omitir
        if ($ConservarArchivosExtra) { $args2 += '-enableRule:DoNotDeleteRule' }
        if (-not $Aplicar) {
            $args2 += '-whatif'
            Write-Host "SIMULACION: no se cambia nada en el servidor. Use -Aplicar para publicar." -ForegroundColor Yellow
        } else {
            Write-Host "Se publicara en $Sitio. El sitio mostrara 'app_offline' unos segundos/minutos mientras se copian los archivos." -ForegroundColor Yellow
            if ((Read-Host 'Escriba PUBLICAR para continuar') -ne 'PUBLICAR') { throw 'Cancelado.' }
        }
        Ejecutar $args2 ($(if ($Aplicar) { "Publicando $Paquete en $Sitio" } else { "Simulando publicacion de $Paquete en $Sitio" }))
    }

    'SubirSecretos' {
        # El secretsSettings.config del sitio antiguo se borra al publicar; la version nueva lee secretsSettings.json.
        # Sube solo ese archivo (DoNotDeleteRule: no borra nada del servidor).
        if (-not (Test-Path $Secretos)) { throw "No existe $Secretos (copie secretsSettings.json.example y complete los valores)." }
        $tmp = Join-Path $env:TEMP ("bufins-secretos-" + $sello)
        New-Item -ItemType Directory -Force $tmp | Out-Null
        try {
            Copy-Item $Secretos (Join-Path $tmp 'secretsSettings.json') -Force
            $args2 = @('-verb:sync', "-source:contentPath=`"$tmp`"", "-dest:contentPath=`"$Sitio`",$conexion", '-allowUntrusted', '-enableRule:DoNotDeleteRule')
            if (-not $Aplicar) { $args2 += '-whatif'; Write-Host 'SIMULACION: use -Aplicar para subir el archivo.' -ForegroundColor Yellow }
            Ejecutar $args2 'Subiendo secretsSettings.json al sitio (no borra nada)'
        } finally { Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue }
    }

    'Restaurar' {
        if (-not $Respaldo -or -not (Test-Path $Respaldo)) { throw 'Indique -Respaldo con la carpeta del respaldo (la que creo la accion Respaldar).' }
        $args2 = @('-verb:sync', "-source:archiveDir=`"$Respaldo`"", "-dest:contentPath=`"$Sitio`",$conexion", '-allowUntrusted', '-enableRule:AppOffline')
        if (-not $Aplicar) {
            $args2 += '-whatif'
            Write-Host "SIMULACION: no se cambia nada en el servidor. Use -Aplicar para restaurar." -ForegroundColor Yellow
        } else {
            Write-Host "Se RESTAURARA $Sitio al estado del respaldo $Respaldo (reemplaza lo publicado)." -ForegroundColor Yellow
            if ((Read-Host 'Escriba RESTAURAR para continuar') -ne 'RESTAURAR') { throw 'Cancelado.' }
        }
        Ejecutar $args2 ($(if ($Aplicar) { "Restaurando $Sitio desde $Respaldo" } else { "Simulando restauracion desde $Respaldo" }))
    }
}

Write-Host ""
Write-Host "Registro: $registro"
