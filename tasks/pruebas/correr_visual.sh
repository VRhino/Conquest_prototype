#!/bin/bash
# La pasada visual: las pruebas PlayMode del arte del mapa ([Category("Visual")]: suelo, agua, vegetación, luz, plazas, murallas, pueblos, lo
# menudo, columnas, caminos, costes de dibujo), que no van en la pasada de cada commit (correr_en_paralelo.sh las excluye). Contra un servidor
# de prueba propio (7891), como correr.sh. Si sale bien (sin más rojos que los de rojos_visual_conocidos.txt), apunta el commit en
# Logs/visual_ok.txt, y el gancho pre-push no la repite para ese commit. Desde la carpeta de Conquest, con Unity cerrado:
#   bash tasks/pruebas/correr_visual.sh
# Sale 0 si va bien; 1 si hay un rojo nuevo; 2 si no se pudo correr (Unity abierto, sin servidor, sin resultados).
CQ=$(cd "$(dirname "$0")/../.." && pwd)
MOTOR=${MOTOR:-$(cd "$CQ/.." && pwd)/BronzeAgeMotorPruebas}
cd "$CQ"
if [ -f Temp/UnityLockfile ] && tasklist 2>/dev/null | grep -qi "^unity.exe"; then
  echo "pasada visual: Unity está abierto con el proyecto; ciérralo y vuelve a lanzarla." >&2
  exit 2
fi
HEAD_=$(git rev-parse HEAD)
DATOS=$CQ/Logs/pruebas-visual
bash "$CQ/tasks/pruebas/parar.sh"
rm -rf "$DATOS" && mkdir -p "$DATOS/datos" "$CQ/Logs/mundo"
(BRONZEAGE_DIRECCION=127.0.0.1:7891 BRONZEAGE_MUNDO=$MOTOR/mundo-pruebas BRONZEAGE_DATOS=$DATOS/datos \
  nohup "$MOTOR/target/release/bronzeage-servidor.exe" > "$DATOS/servidor.log" 2>&1 &)
for i in $(seq 1 15); do grep -q escuchando "$DATOS/servidor.log" 2>/dev/null && break; sleep 1; done
CODIGO=$(grep -o "regístrate con él): [A-Z0-9-]*" "$DATOS/servidor.log" | awk '{print $NF}')
if [ -z "$CODIGO" ]; then echo "pasada visual: el servidor de prueba no arrancó (mira $DATOS/servidor.log)." >&2; bash "$CQ/tasks/pruebas/parar.sh"; exit 2; fi
inicio=$(date +%s)
rm -f Logs/mundo/visual.xml
BRONZEAGE_PRUEBA_ADMIN=$CODIGO BRONZEAGE_PRUEBA_URL=http://127.0.0.1:7891 MUNDO_CAPTURAS= "/c/Program Files/Unity/Hub/Editor/6000.5.8f1/Editor/Unity.exe" \
  -batchmode -projectPath . -runTests -testPlatform PlayMode -testCategory "Visual" -testResults Logs/mundo/visual.xml -logFile Logs/mundo/visual.log
bash "$CQ/tasks/pruebas/parar.sh"
if [ ! -f Logs/mundo/visual.xml ]; then echo "pasada visual: Unity no dejó resultados (mira Logs/mundo/visual.log)." >&2; exit 2; fi
echo "pasada visual, en $(( $(date +%s) - inicio )) s: $(head -c 700 Logs/mundo/visual.xml | grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*"')"
# los rojos, frente a los conocidos (una prueba por línea, con su motivo detrás de « #»; las líneas que empiezan por # son comentarios)
CONOCIDOS=$(grep -v '^#' "$CQ/tasks/pruebas/rojos_visual_conocidos.txt" 2>/dev/null | sed 's/ *#.*//' | grep -v '^ *$')
NUEVOS=""
for r in $(grep -o 'methodname="[^"]*"[^>]*result="Failed"' Logs/mundo/visual.xml | grep -o '^methodname="[^"]*"' | sed 's/methodname="//; s/"$//'); do
  if echo "$CONOCIDOS" | grep -qx "$r"; then echo "  rojo conocido: $r"; else NUEVOS="$NUEVOS $r"; echo "  ROJO NUEVO: $r"; fi
done
if [ -n "$NUEVOS" ]; then exit 1; fi
# (el árbol de Assets, sin cambios sin commit fuera de lo ajeno de siempre: si no, lo probado no es el commit)
SUCIO=$(git status --porcelain -- Assets ':!Assets/TextMesh Pro' ':!Assets/InitTestScene*')
if [ -n "$SUCIO" ]; then
  echo "pasada visual: bien, pero hay cambios sin commit en Assets: no se apunta el commit $HEAD_." >&2
  exit 0
fi
echo "$HEAD_" >> Logs/visual_ok.txt
echo "pasada visual: bien; apuntado el commit $HEAD_ en Logs/visual_ok.txt."
exit 0
