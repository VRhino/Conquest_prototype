#!/bin/bash
# La suite del mundo en dos lotes a la vez: el lote 1 en Conquest contra su servidor de prueba (7891) y el lote 2 en el clon enlazado
# (../Conquest_prototype_lote2, `clon_lote2.sh`) contra otro (7892), cada uno con sus datos y su copia del mundo. Qué prueba va en cada lote lo
# dice `MUNDO_LOTE` (`PantallasDeMundoTests.Lotes`); en el otro lote sale como saltada. Sin capturas (la regresión). Desde la carpeta de Conquest:
#   bash tasks/pruebas/correr_en_paralelo.sh
# Nunca con el editor de Víctor abierto en el proyecto principal. Al acabar, los dos servidores apagados.
# Sin las pruebas del arte del mapa ([Category("Visual")], 25-11): van en la pasada visual (correr_visual.sh), antes del push (gancho pre-push).
CQ=$(cd "$(dirname "$0")/../.." && pwd)
MOTOR=${MOTOR:-$(cd "$CQ/.." && pwd)/BronzeAgeMotorPruebas}
UNITY="/c/Program Files/Unity/Hub/Editor/6000.5.8f1/Editor/Unity.exe"
bash "$CQ/tasks/pruebas/parar.sh"
CLON=$(bash "$CQ/tasks/pruebas/clon_lote2.sh")
# El mundo del lote 2, una copia del de pruebas (el servidor no lo escribe, pero así cada uno tiene el suyo).
mkdir -p "$MOTOR/mundo-pruebas-2" && cp -u "$MOTOR"/mundo-pruebas/*.bin "$MOTOR/mundo-pruebas-2/"
servidor() { # puerto, carpeta del proyecto, mundo
  local datos=$2/Logs/pruebas-mundo
  rm -rf "$datos" && mkdir -p "$datos/datos"
  (BRONZEAGE_DIRECCION=127.0.0.1:$1 BRONZEAGE_MUNDO=$3 BRONZEAGE_DATOS=$datos/datos nohup "$MOTOR/target/release/bronzeage-servidor.exe" > "$datos/servidor.log" 2>&1 &)
  for i in $(seq 1 15); do grep -q escuchando "$datos/servidor.log" 2>/dev/null && break; sleep 1; done
  grep -o "regístrate con él): [A-Z0-9-]*" "$datos/servidor.log" | awk '{print $NF}'
}
C1=$(servidor 7891 "$CQ" "$MOTOR/mundo-pruebas")
C2=$(servidor 7892 "$CLON" "$MOTOR/mundo-pruebas-2")
mkdir -p "$CQ/Logs/mundo" "$CLON/Logs/mundo"
inicio=$(date +%s)
(cd "$CQ" && MUNDO_LOTE=1 BRONZEAGE_PRUEBA_ADMIN=$C1 BRONZEAGE_PRUEBA_URL=http://127.0.0.1:7891 MUNDO_CAPTURAS= "$UNITY" -batchmode -projectPath . -runTests \
  -testPlatform PlayMode -testFilter "ConquestTactics.Mundo.UI.Tests" -testCategory "!Visual" -testResults Logs/mundo/lote1.xml -logFile Logs/mundo/lote1.log) &
(cd "$CLON" && MUNDO_LOTE=2 BRONZEAGE_PRUEBA_ADMIN=$C2 BRONZEAGE_PRUEBA_URL=http://127.0.0.1:7892 MUNDO_CAPTURAS= "$UNITY" -batchmode -projectPath . -runTests \
  -testPlatform PlayMode -testFilter "ConquestTactics.Mundo.UI.Tests.PantallasDeMundoTests" -testResults Logs/mundo/lote2.xml -logFile Logs/mundo/lote2.log) &
wait
echo "las dos vueltas, en $(( $(date +%s) - inicio )) s"
cp "$CLON/Logs/mundo/lote2.xml" "$CLON/Logs/mundo/lote2.log" "$CQ/Logs/mundo/" 2>/dev/null
for n in 1 2; do
  echo "lote $n: $(head -c 600 "$CQ/Logs/mundo/lote$n.xml" | grep -o 'result="[^"]*" total="[0-9]*" passed="[0-9]*" failed="[0-9]*"')"
  grep -o 'methodname="[^"]*"[^>]*result="Failed"' "$CQ/Logs/mundo/lote$n.xml" | sed 's/classname.*result/result/'
done
powershell -NoProfile -Command 'foreach ($port in 7891,7892) { $p=(Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue).OwningProcess; if ($p) { Stop-Process -Id $p -Confirm:$false } }' >/dev/null 2>&1
