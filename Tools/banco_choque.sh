#!/usr/bin/env bash
# El banco de la predicción «c» (doc 22 §8.6.1): soldados quietos, a 1 m/s y en plena lucha, con los tres perfiles de red, con y sin «c»,
# 2 vueltas. Una línea por cliente con sus correcciones, y una por servidor con sus recolocaciones. Unos 80 minutos.
cd "$(dirname "$0")/.."
for r in 1 2; do for esc in quietos lentos lucha; do for p in buena mala pesima; do for modo in antes despues; do
  extra=""; [ $modo = antes ] && extra="-sinChoque"
  srv=""; [ $esc = quietos ] && srv="-lentos 0 -lentosDesde 45"; [ $esc = lentos ] && srv="-lentos 1 -lentosDesde 45"
  n=esc-$esc-$p-$modo-$r
  EXE=Builds/PruebaRedSimulador/PruebaRed.exe VENTANA=no SERVIDOR="$srv" TODOS="$extra" CLIENTES="--loadNetworkSimulatorJsonFile Tools/perfiles_de_red/$p.json -botAlChoque -batallaReal 1 -conMapa" bash Tools/ver_batalla_real.sh $n 2 130 25 > /dev/null 2>&1
  for f in Logs/red/$n/cliente*.txt; do echo "$esc $p $modo $(grep -o 's cerca) correcciones.*' $f) $(grep -o ', [0-9]*s cerca' $f)"; done
  echo "$esc $p $modo servidor $(grep -o 'recolocaciones.*' Logs/red/$n/servidor.txt)"
done; done; done; done
echo FIN
