# Recompila el contrato de BronzeAge y copia las DLL a Assets/Plugins/BronzeAge.
# Uso: powershell -File Tools/ContratoBronzeAge/actualizar.ps1 [-Proto C:\ruta\a\BronzeAge\proto]
param([string]$Proto = "")
$ErrorActionPreference = "Stop"
$aqui = $PSScriptRoot
$destino = Join-Path $aqui "..\..\Assets\Plugins\BronzeAge"
$argumentos = @("build", "-c", "Release", $aqui)
if ($Proto) { $argumentos += "-p:BronzeAgeProto=$Proto" }
dotnet @argumentos
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
New-Item -ItemType Directory -Force $destino | Out-Null
$salida = Join-Path $aqui "bin\Release\netstandard2.1"
foreach ($dll in "BronzeAge.Contrato.dll", "Google.Protobuf.dll") {
    $origen = Join-Path $salida $dll
    if (Test-Path $origen) { Copy-Item $origen $destino -Force; Write-Host "copiado $dll" }
}
