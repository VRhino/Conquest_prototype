#!/usr/bin/env bash
# Prueba de red: un servidor dedicado y N clientes bot en este PC, con Builds/PruebaRed.
# Uso: Tools/medir_red.sh <nombre> <clientes> <soldados por héroe> <héroes extra> [tasa de red] [segundos] [bytes por snapshot]
set -u
cd "$(dirname "$0")/.."
nombre=$1; clientes=$2; soldados=$3; extra=$4; tasa=${5:-60}; segundos=${6:-30}; paquete=${7:-0}
dir=Logs/red/$nombre; mkdir -p "$dir"; rm -f "${dir:?}"/*.txt "${dir:?}"/*.log
exe=Builds/PruebaRed/PruebaRed.exe
comun="-batchmode -nographics -puerto 7979"
$exe $comun -red servidor -soldados "$soldados" -heroesExtra "$extra" -tasaRed "$tasa" -paquete "$paquete" -medir "$segundos" \
  -informe "$dir/servidor.txt" -logFile "$dir/servidor.log" >/dev/null 2>&1 &
servidor=$!
sleep 2
for i in $(seq 1 "$clientes"); do
  $exe $comun -red cliente -ip 127.0.0.1 -bot -medir $((segundos - 3)) -informe "$dir/cliente$i.txt" -logFile "$dir/cliente$i.log" >/dev/null 2>&1 &
done
wait
cat "$dir"/*.txt
