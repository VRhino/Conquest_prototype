#!/bin/bash
# Lo nuestro del gancho pre-push (lo llama .git/hooks/pre-push antes de Git LFS; lo instala instalar_ganchos.sh): la pasada visual de las
# pruebas del arte del mapa antes de subir. Recibe en $1 un fichero con lo que git le pasa al gancho por la entrada (una línea por rama:
# «ref local, commit local, ref remota, commit remoto»).
# - Si cada commit que se sube ya pasó la pasada visual (Logs/visual_ok.txt, lo apunta correr_visual.sh), sale 0 al momento.
# - Si no, y Unity está abierto con el proyecto, corta el push y pide cerrarlo.
# - Si no, corre la pasada visual (una hora, más o menos) y corta solo ante un rojo que no esté en rojos_visual_conocidos.txt.
# Salidas de emergencia: SALTAR_VISUAL=1 git push …, o git push --no-verify (este último se salta también Git LFS).
CQ=$(cd "$(dirname "$0")/../.." && pwd)
REFS=$1
[ "${SALTAR_VISUAL:-0}" = "1" ] && { echo "pre-push: pasada visual saltada (SALTAR_VISUAL=1)."; exit 0; }
CERO=0000000000000000000000000000000000000000
FALTAN=""
while read -r ref_local sha_local ref_remota sha_remota; do
  [ -z "$sha_local" ] && continue
  [ "$sha_local" = "$CERO" ] && continue  # (se borra una rama remota: nada que probar)
  grep -qx "$sha_local" "$CQ/Logs/visual_ok.txt" 2>/dev/null || FALTAN="$FALTAN $sha_local"
done < "$REFS"
[ -z "$FALTAN" ] && exit 0
if [ -f "$CQ/Temp/UnityLockfile" ] && tasklist 2>/dev/null | grep -qi "^unity.exe"; then
  echo "pre-push: falta la pasada visual de este commit y Unity está abierto con el proyecto. Ciérralo y vuelve a hacer push" >&2
  echo "          (o pásala antes con: bash tasks/pruebas/correr_visual.sh; para saltarla: SALTAR_VISUAL=1 git push, o git push --no-verify)." >&2
  exit 1
fi
HEAD_=$(git -C "$CQ" rev-parse HEAD)
for s in $FALTAN; do
  if [ "$s" != "$HEAD_" ]; then
    echo "pre-push: el commit que se sube ($s) no es el que hay en la carpeta ($HEAD_): la pasada visual prueba la carpeta." >&2
    echo "          Cámbiate a ese commit y pásala (bash tasks/pruebas/correr_visual.sh), o sáltala con SALTAR_VISUAL=1 git push (o git push --no-verify)." >&2
    exit 1
  fi
done
echo "pre-push: corriendo la pasada visual del commit $HEAD_ (las pruebas del arte del mapa; tarda en torno a una hora)…"
bash "$CQ/tasks/pruebas/correr_visual.sh"
r=$?
if [ $r -ne 0 ]; then
  echo "pre-push: la pasada visual no salió bien (código $r; resultados en Logs/mundo/visual.xml). Push cortado." >&2
  echo "          Para subir igualmente: SALTAR_VISUAL=1 git push (o git push --no-verify)." >&2
  exit 1
fi
grep -qx "$HEAD_" "$CQ/Logs/visual_ok.txt" 2>/dev/null || { echo "pre-push: la pasada salió bien pero con cambios sin commit en Assets: no vale para este commit. Push cortado." >&2; exit 1; }
exit 0
