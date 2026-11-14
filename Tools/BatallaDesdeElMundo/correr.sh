#!/bin/bash
# Uso: MOTOR=... MUNDO=... Tools/BatallaDesdeElMundo/correr.sh "ConquestTactics.Mundo.UI.Tests.PantallasDeMundoTests.DelMapaALaBatalla" <carpeta de capturas>
# Paso 5A: el motor 358af5ff con batallas de Unity (mi servidor, 7891), el lanzador de verdad (lo arranca la prueba) y PruebaRed.exe,
# y el editor SIN -batchmode como cliente (la red no carga BattleScene en batchmode). Al acabar, todo apagado.
# MOTOR: carpeta de compilación del motor (bronzeage-servidor.exe y bronzeage-lanzador.exe, de 358af5ff o posterior); MUNDO: el mundo generado.
S=${TRABAJO:-$(mktemp -d)}
MOTOR=${MOTOR:?pon MOTOR=<carpeta con bronzeage-servidor.exe y bronzeage-lanzador.exe>}
MUNDO=${MUNDO:?pon MUNDO=<carpeta del mundo generado>}
FILTRO="$1"; CAPS="$2"
CQ=$(cd "$(dirname "$0")/../.." && pwd)
powershell -NoProfile -Command '$p=(Get-NetTCPConnection -State Listen -LocalPort 7891 -ErrorAction SilentlyContinue).OwningProcess; if ($p) { Stop-Process -Id $p -Confirm:$false }' >/dev/null 2>&1
sleep 1
cd $S && rm -rf datos-batalla && mkdir datos-batalla
SERV=servidores-$RANDOM$RANDOM
(BRONZEAGE_DIRECCION=127.0.0.1:7891 BRONZEAGE_MUNDO=$MUNDO BRONZEAGE_DATOS=$S/datos-batalla \
  BRONZEAGE_BATALLAS_EN_UNITY=1 CODIGO_REGISTRO_SERVIDORES=$SERV nohup $MOTOR/bronzeage-servidor.exe > $S/servidor.log 2>&1 &)
for i in 1 2 3 4 5 6 7 8 9 10; do grep -q escuchando $S/servidor.log 2>/dev/null && break; sleep 1; done
CODIGO=$(grep -o "regístrate con él): [A-Z0-9-]*" $S/servidor.log | awk '{print $NF}')
cd $CQ
export BRONZEAGE_PRUEBA_ADMIN=$CODIGO BRONZEAGE_PRUEBA_URL=http://127.0.0.1:7891 MUNDO_CAPTURAS="$CAPS"
export BRONZEAGE_PRUEBA_LANZADOR="$(cygpath -w $MOTOR/bronzeage-lanzador.exe)"
export LANZADOR_MOTOR=127.0.0.1:7891 LANZADOR_NICK=lanzador-prueba LANZADOR_CLAVE=clave-lanzador-1 CODIGO_REGISTRO_SERVIDORES=$SERV
export LANZADOR_HOST=127.0.0.1 LANZADOR_PUERTOS=7801-7805 LANZADOR_PROCESO="$(cygpath -w $CQ/Builds/PruebaRed/PruebaRed.exe)"
export LANZADOR_ARGUMENTOS="-batchmode -nographics -logFile $(cygpath -w $CQ/Logs/mundo/batalla_servidor.log) -espera 60"
export CONQUEST_BATALLA_REAL=1 CONQUEST_DURACION=${CONQUEST_DURACION:-60}
rm -f Logs/mundo/batalla.xml
"/c/Program Files/Unity/Hub/Editor/6000.5.8f1/Editor/Unity.exe" -projectPath . -runTests -testPlatform PlayMode -testFilter "$FILTRO" -testResults Logs/mundo/batalla.xml -logFile Logs/mundo/batalla.log
echo "exit $?"
head -c 600 Logs/mundo/batalla.xml | grep -o 'result="[^"]*" total="[0-9]*" passed="[0-9]*" failed="[0-9]*"'
grep -A4 "<message>" Logs/mundo/batalla.xml | grep -v "child tests\|</\?failure>\|^--\|test-suite\|propert" | head -8
grep -o "\[Prueba\][^<]*\|\[Batalla\][^<]*\|\[Red\][^<]*" Logs/mundo/batalla.log | cut -c1-300 | head -40
grep "): error" Logs/mundo/batalla.log | sort -u | head -5
taskkill //F //IM bronzeage-lanzador.exe >/dev/null 2>&1; taskkill //F //IM PruebaRed.exe >/dev/null 2>&1
powershell -NoProfile -Command '$p=(Get-NetTCPConnection -State Listen -LocalPort 7891 -ErrorAction SilentlyContinue).OwningProcess; if ($p) { Stop-Process -Id $p -Confirm:$false }' >/dev/null 2>&1
