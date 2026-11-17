#!/bin/bash
# El clon enlazado del proyecto para el segundo lote de pruebas (como ParrelSync): ../Conquest_prototype_lote2, con
# - Assets y Packages enlazados (junction) a los de Conquest: el mismo código, sin copiarlo;
# - Library copiada una vez (11 GB) y puesta al día en cada vuelta (robocopy solo copia lo cambiado), menos lo compilado (Bee y ScriptAssemblies);
# - ProjectSettings copiado en cada vuelta, con otro productName: así sus PlayerPrefs (el login de las pruebas) no pisan los del principal;
# - el fichero `.clon` que lo marca.
# Para quitarlo: `bash tasks/pruebas/clon_lote2.sh quitar`, que borra SOLO los enlaces (rmdir sin /s) y deja Conquest intacto.
# Nunca con el editor de Víctor abierto en el proyecto principal.
CQ=$(cd "$(dirname "$0")/../.." && pwd)
CLON=$(cd "$CQ/.." && pwd)/Conquest_prototype_lote2
W() { cygpath -w "$1"; }
if [ "$1" = "quitar" ]; then
  for d in Assets Packages; do [ -d "$CLON/$d" ] && cmd //c rmdir "$(W "$CLON/$d")"; done
  echo "enlaces quitados; el resto de $CLON (Library, ProjectSettings, Logs, Temp) se puede borrar a mano"
  exit 0
fi
mkdir -p "$CLON"
for d in Assets Packages; do [ -e "$CLON/$d" ] || cmd //c mklink //J "$(W "$CLON/$d")" "$(W "$CQ/$d")" >/dev/null; done
touch "$CLON/.clon"
# Sin Bee ni ScriptAssemblies: lo compilado del clon es suyo. Lo de Conquest lleva la ruta de Conquest dentro, así que copiado no le vale
# al clon y lo recompilaba entero en cada vuelta (13 minutos).
robocopy "$(W "$CQ/Library")" "$(W "$CLON/Library")" //MIR //XD "$(W "$CQ/Library/Bee")" "$(W "$CQ/Library/ScriptAssemblies")" "$(W "$CLON/Library/Bee")" "$(W "$CLON/Library/ScriptAssemblies")" //NFL //NDL //NJH //NJS //NP //R:1 //W:1 >/dev/null
# ProjectSettings.asset, con su productName, solo si cambió (antes se reescribía en cada vuelta).
robocopy "$(W "$CQ/ProjectSettings")" "$(W "$CLON/ProjectSettings")" //MIR //XF ProjectSettings.asset //NFL //NDL //NJH //NJS //NP >/dev/null
sed "s/^  productName: .*/  productName: prototipo_curado_lote2/" "$CQ/ProjectSettings/ProjectSettings.asset" > "$CLON/ProjectSettings/ProjectSettings.asset.nuevo"
cmp -s "$CLON/ProjectSettings/ProjectSettings.asset.nuevo" "$CLON/ProjectSettings/ProjectSettings.asset" && rm "$CLON/ProjectSettings/ProjectSettings.asset.nuevo" || mv "$CLON/ProjectSettings/ProjectSettings.asset.nuevo" "$CLON/ProjectSettings/ProjectSettings.asset"
echo "$CLON"
