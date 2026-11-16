#!/bin/bash
# La batalla de punta a punta (DelMapaALaBatalla): el servidor de prueba propio (7891) con batallas de Unity, el lanzador de verdad (lo arranca la
# prueba) y Builds/PruebaRed/PruebaRed.exe, y el editor SIN -batchmode (la red no carga BattleScene en batchmode). Al acabar, todo apagado.
#   bash tasks/pruebas/correr_batalla.sh "ConquestTactics.Mundo.UI.Tests.PantallasDeMundoTests.DelMapaALaBatalla" ""
FILTRO="$1"; CAPS="$2"
CQ=$(cd "$(dirname "$0")/../.." && pwd)
MOTOR=${MOTOR:-$(cd "$CQ/.." && pwd)/BronzeAgeMotorPruebas}
DATOS=$CQ/Logs/pruebas-batalla
bash "$CQ/tasks/pruebas/parar.sh"
rm -rf "$DATOS" && mkdir -p "$DATOS/datos"
SERV=servidores-$RANDOM$RANDOM
(BRONZEAGE_DIRECCION=127.0.0.1:7891 BRONZEAGE_MUNDO=$MOTOR/mundo-pruebas BRONZEAGE_DATOS=$DATOS/datos \
  BRONZEAGE_BATALLAS_EN_UNITY=1 CODIGO_REGISTRO_SERVIDORES=$SERV nohup "$MOTOR/target/release/bronzeage-servidor.exe" > "$DATOS/servidor.log" 2>&1 &)
for i in $(seq 1 15); do grep -q escuchando "$DATOS/servidor.log" 2>/dev/null && break; sleep 1; done
CODIGO=$(grep -o "regístrate con él): [A-Z0-9-]*" "$DATOS/servidor.log" | awk '{print $NF}')
cd "$CQ"
export BRONZEAGE_PRUEBA_ADMIN=$CODIGO BRONZEAGE_PRUEBA_URL=http://127.0.0.1:7891 MUNDO_CAPTURAS="$CAPS"
export BRONZEAGE_PRUEBA_LANZADOR="$(cygpath -w "$MOTOR/target/release/bronzeage-lanzador.exe")"
export LANZADOR_MOTOR=127.0.0.1:7891 LANZADOR_NICK=lanzador-prueba LANZADOR_CLAVE=clave-lanzador-1 CODIGO_REGISTRO_SERVIDORES=$SERV
export LANZADOR_HOST=127.0.0.1 LANZADOR_PUERTOS=7801-7805 LANZADOR_PROCESO="$(cygpath -w "$CQ/Builds/PruebaRed/PruebaRed.exe")"
export LANZADOR_ARGUMENTOS="-batchmode -nographics -logFile $(cygpath -w "$CQ/Logs/mundo/batalla_servidor.log") -espera 60"
export CONQUEST_BATALLA_REAL=1 CONQUEST_DURACION=${CONQUEST_DURACION:-60}
rm -f Logs/mundo/batalla.xml
"/c/Program Files/Unity/Hub/Editor/6000.5.8f1/Editor/Unity.exe" -projectPath . -runTests -testPlatform PlayMode -testFilter "$FILTRO" -testResults Logs/mundo/batalla.xml -logFile Logs/mundo/batalla.log
echo "exit $?"
head -c 600 Logs/mundo/batalla.xml | grep -o 'result="[^"]*" total="[0-9]*" passed="[0-9]*" failed="[0-9]*"'
grep -A4 "<message>" Logs/mundo/batalla.xml | grep -v "child tests\|</\?failure>\|^--\|test-suite\|propert" | head -8
grep "): error" Logs/mundo/batalla.log | sort -u | head -5
bash "$CQ/tasks/pruebas/parar.sh"
