#!/bin/bash
# Apaga lo que arrancan las pruebas: los servidores de prueba (7891 y 7892, el del lote 2), el lanzador y los servidores de batalla (7801-7805) y el Vite del prototipo (5199).
# Nunca toca el 7878 (el servidor de Víctor).
powershell -NoProfile -Command 'foreach ($port in 7891,7892,5199,7801,7802,7803,7804,7805) { $p=(Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue).OwningProcess; if ($p) { Stop-Process -Id $p -Confirm:$false } }' >/dev/null 2>&1
taskkill //F //IM bronzeage-lanzador.exe >/dev/null 2>&1
taskkill //F //IM PruebaRed.exe >/dev/null 2>&1
exit 0
