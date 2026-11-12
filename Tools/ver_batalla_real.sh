#!/usr/bin/env bash
# La batalla de verdad en el servidor (-batallaReal) y dos clientes en este PC, con Builds/PruebaRed. Cada uno recibe
# un héroe de la batalla: el primero, sin pantalla, lo lleva un bot y mide lo que llega por la red; el segundo, con
# ventana, carga la escena de batalla, pinta soldados y héroes con sus modelos y guarda dos capturas.
# Quinto argumento, opciones del cliente de ventana: "-bot" para que también juegue solo (si no, se juega con el
# teclado), "-camaraCerca" para que un espectador sin héroe se ponga junto al choque.
# Variables de entorno:
#   SERVIDOR  opciones solo del servidor:          SERVIDOR="-tasaRed 30 -alcance 100"
#   TODOS     opciones de servidor y clientes:     TODOS="-tasaSoldados 20" (va en el prefab fantasma: igual en todos)
#   VENTANA=no  el segundo cliente tampoco tiene pantalla (para medir sin abrir ventanas)
#   CLIENTES  opciones de los dos clientes:        CLIENTES="--loadNetworkSimulatorJsonFile Tools/perfiles_de_red/mala.json -apagon 60,8"
#   EXE       otro ejecutable:                     EXE=Builds/PruebaRedSimulador/PruebaRed.exe (el del simulador de red)
# Uso: Tools/ver_batalla_real.sh <nombre> <héroes por bando> [segundos del servidor] [segundo en que entran los clientes] [opciones del cliente de ventana]
set -u
cd "$(dirname "$0")/.."
nombre=$1; heroes=$2; segundos=${3:-150}; entrada=${4:-60}; ventana=${5:-}
dir=Logs/red/$nombre; mkdir -p "$dir"; rm -f "${dir:?}"/*.txt "${dir:?}"/*.log "${dir:?}"/*.png
exe=${EXE:-Builds/PruebaRed/PruebaRed.exe}
resto=$((segundos - entrada - 5))
$exe -batchmode -nographics -puerto 7979 -red servidor -batallaReal "$heroes" ${SERVIDOR:-} ${TODOS:-} -medir "$segundos" \
  -informe "$dir/servidor.txt" -logFile "$dir/servidor.log" >/dev/null 2>&1 &
sleep "$entrada"
$exe -batchmode -nographics -puerto 7979 -red cliente -ip 127.0.0.1 -bot ${TODOS:-} ${CLIENTES:-} -medir "$resto" \
  -informe "$dir/cliente.txt" -logFile "$dir/cliente.log" >/dev/null 2>&1 &
if [ "${VENTANA:-si}" = no ]; then
  $exe -batchmode -nographics -puerto 7979 -red cliente -ip 127.0.0.1 -bot ${TODOS:-} ${CLIENTES:-} -medir "$resto" \
    -informe "$dir/cliente2.txt" -logFile "$dir/cliente2.log" >/dev/null 2>&1 &
else
  $exe -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -puerto 7979 -red cliente -ip 127.0.0.1 -batallaReal 1 $ventana ${TODOS:-} ${CLIENTES:-} \
    -captura "$(pwd -W)/$dir/captura.png" -capturaA $((resto - 10)) -medir "$resto" \
    -informe "$dir/cliente-ventana.txt" -logFile "$dir/cliente-ventana.log" >/dev/null 2>&1 &
fi
wait
cat "$dir"/*.txt
