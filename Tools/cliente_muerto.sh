#!/usr/bin/env bash
# Servidor de batalla sin pantalla y dos clientes bot en este PC; a los MATAR s se mata de golpe el primero. El servidor
# tiene que dar por ido al muerto (a los 10 s) y seguir oyendo al otro hasta el final. Uso: Tools/cliente_muerto.sh [puerto]
set -u
cd "$(dirname "$0")/.."
puerto=${1:-7895}; exe=${EXE:-Builds/PruebaRed/PruebaRed.exe}; dir=Logs/red/cliente-muerto; MATAR=${MATAR:-25}
mkdir -p "$dir"; rm -f "$dir"/*.log
$exe -batchmode -nographics -puerto "$puerto" -red servidor -batallaReal 1 -medir 80 -logFile "$dir/servidor.log" >/dev/null 2>&1 &
sleep 10
$exe -batchmode -nographics -puerto "$puerto" -red cliente -ip 127.0.0.1 -bot -medir 65 -logFile "$dir/muerto.log" >/dev/null 2>&1 &
muerto=$!
$exe -batchmode -nographics -puerto "$puerto" -red cliente -ip 127.0.0.1 -bot -medir 65 -logFile "$dir/vivo.log" >/dev/null 2>&1 &
sleep "$MATAR"
taskkill //F //PID "$(cat /proc/$muerto/winpid)" >/dev/null
wait
echo "--- servidor"
grep -h "se fue\|no llega nada\|vuelve\|Could not enqueue\|conexiones=" "$dir/servidor.log"
