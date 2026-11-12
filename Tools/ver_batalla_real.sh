#!/usr/bin/env bash
# La batalla de verdad en el servidor (-batallaReal) y dos clientes que la miran en este PC, con Builds/PruebaRed:
# uno sin pantalla que mide lo que llega por la red y otro con ventana que guarda una captura.
# Uso: Tools/ver_batalla_real.sh <nombre> <héroes por bando> [segundos del servidor] [segundo en que entran los clientes]
set -u
cd "$(dirname "$0")/.."
nombre=$1; heroes=$2; segundos=${3:-150}; entrada=${4:-60}
dir=Logs/red/$nombre; mkdir -p "$dir"; rm -f "${dir:?}"/*.txt "${dir:?}"/*.log "${dir:?}"/*.png
exe=Builds/PruebaRed/PruebaRed.exe
resto=$((segundos - entrada - 5))
$exe -batchmode -nographics -puerto 7979 -red servidor -batallaReal "$heroes" -medir "$segundos" \
  -informe "$dir/servidor.txt" -logFile "$dir/servidor.log" >/dev/null 2>&1 &
sleep "$entrada"
$exe -batchmode -nographics -puerto 7979 -red cliente -ip 127.0.0.1 -medir "$resto" \
  -informe "$dir/cliente.txt" -logFile "$dir/cliente.log" >/dev/null 2>&1 &
$exe -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -puerto 7979 -red cliente -ip 127.0.0.1 \
  -captura "$(pwd -W)/$dir/captura.png" -capturaA $((resto - 10)) -medir "$resto" \
  -informe "$dir/cliente-ventana.txt" -logFile "$dir/cliente-ventana.log" >/dev/null 2>&1 &
wait
cat "$dir"/*.txt
