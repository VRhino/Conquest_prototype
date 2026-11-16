#!/bin/bash
# Las pruebas PlayMode del mundo contra un servidor de prueba PROPIO (puerto 7891, nunca el 7878 de Víctor), con el motor de main compilado en
# su clon aparte (tasks/pruebas/preparar_motor.sh) y datos nuevos en cada vuelta. Al acabar, el servidor sigue arriba (para mirar o capturar);
# se apaga con tasks/pruebas/parar.sh. Desde la carpeta de Conquest:
#   bash tasks/pruebas/correr.sh "ConquestTactics.Mundo.UI.Tests" "<carpeta de capturas o vacío>"
#   bash tasks/pruebas/correr.sh "ConquestTactics.Mundo.UI.Tests.PantallasDeMundoTests.TuPlazaPorDentro;ConquestTactics.Mundo.UI.Tests.SigilosTests" ""
# Si el editor de Víctor tiene abierto el proyecto, no correr (el bloqueo de la carpeta lo impide).
FILTRO="$1"; CAPS="$2"
CQ=$(cd "$(dirname "$0")/../.." && pwd)
MOTOR=${MOTOR:-$(cd "$CQ/.." && pwd)/BronzeAgeMotorPruebas}
# Fuera de Temp: Unity la vacía al arrancar y al salir, y el motor perdería su crónica (el diario de `datos/cronica`) a media prueba.
DATOS=$CQ/Logs/pruebas-mundo
bash "$CQ/tasks/pruebas/parar.sh"
rm -rf "$DATOS" && mkdir -p "$DATOS/datos"
(BRONZEAGE_DIRECCION=127.0.0.1:7891 BRONZEAGE_MUNDO=$MOTOR/mundo-pruebas BRONZEAGE_DATOS=$DATOS/datos \
  nohup "$MOTOR/target/release/bronzeage-servidor.exe" > "$DATOS/servidor.log" 2>&1 &)
for i in $(seq 1 15); do grep -q escuchando "$DATOS/servidor.log" 2>/dev/null && break; sleep 1; done
CODIGO=$(grep -o "regístrate con él): [A-Z0-9-]*" "$DATOS/servidor.log" | awk '{print $NF}')
cd "$CQ"
BRONZEAGE_PRUEBA_ADMIN=$CODIGO BRONZEAGE_PRUEBA_URL=http://127.0.0.1:7891 MUNDO_CAPTURAS="$CAPS" "/c/Program Files/Unity/Hub/Editor/6000.5.8f1/Editor/Unity.exe" \
  -batchmode -projectPath . -runTests -testPlatform PlayMode -testFilter "$FILTRO" -testResults Logs/mundo/play.xml -logFile Logs/mundo/play.log
echo "exit $?"
head -c 400 Logs/mundo/play.xml | grep -o 'result="[^"]*" total="[0-9]*" passed="[0-9]*" failed="[0-9]*"'
grep -o 'methodname="[^"]*"[^>]*result="[^"]*"' Logs/mundo/play.xml | sed 's/classname.*result/result/' | grep -v Passed
grep -A4 "<message>" Logs/mundo/play.xml | grep -v "child tests\|</\?failure>\|^--\|test-suite\|propert" | head -8
grep -o "\[Prueba\][^<]*" Logs/mundo/play.log | cut -c1-300
grep "error CS" Logs/mundo/play.log | sort -u | head -5
