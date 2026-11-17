#!/bin/bash
# Apaga lo que arrancan las pruebas, solo lo nuestro (en el PC corren otras sesiones con sus propios lanzadores y servidores de batalla):
# - lo que escucha en nuestros puertos: los servidores de prueba (7891 y 7892, el del lote 2), los servidores de batalla de nuestro lanzador
#   (7801-7805) y el Vite del prototipo (5199);
# - los PID apuntados en Logs/pids-pruebas.txt («pid marca-de-arranque» por línea; el lanzador lo apunta la prueba que lo arranca), solo si
#   el proceso con ese PID es el mismo que arrancó (la marca coincide: un PID se reutiliza).
# Nunca por nombre (taskkill /IM). Nunca toca el 7878 (el servidor de Víctor).
CQ=$(cd "$(dirname "$0")/../.." && pwd)
PIDS=$CQ/Logs/pids-pruebas.txt
PIDS_PRUEBAS="$(cygpath -w "$PIDS")" powershell -NoProfile -Command '
foreach ($port in 7891,7892,5199,7801,7802,7803,7804,7805) {
  $p = (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue).OwningProcess
  if ($p) { Stop-Process -Id $p -Confirm:$false -ErrorAction SilentlyContinue }
}
$f = $env:PIDS_PRUEBAS
if (Test-Path $f) {
  foreach ($linea in Get-Content $f) {
    $pid_, $marca = $linea.Split()
    $proc = Get-Process -Id $pid_ -ErrorAction SilentlyContinue
    if ($proc -and $proc.StartTime.ToUniversalTime().Ticks -eq [long]$marca) { Stop-Process -Id $pid_ -Confirm:$false -ErrorAction SilentlyContinue }
  }
  Remove-Item $f
}' >/dev/null 2>&1
exit 0
