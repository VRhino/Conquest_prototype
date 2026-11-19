#!/bin/bash
# Solo las pruebas de las pantallas tocadas (Víctor, 2026-11-19, duda 44a): las del mundo por su [Category], contra el servidor de prueba propio
# (7891) como `correr.sh`. Para un cambio de interfaz; la suite entera (correr_en_paralelo.sh y correr_batalla.sh) queda para antes del commit.
#   bash tasks/pruebas/correr_ui.sh Campamento Plaza
# Categorías: Entrada (cuenta, creador, selector), Campamento, Plaza, Mapa, Base, Faccion y SinServidor (iconos, piezas, sigilos, vestido y
# aviso de red: no necesitan motor). Batalla no va aquí: necesita el lanzador y el editor sin -batchmode (correr_batalla.sh).
[ $# -eq 0 ] && { sed -n '2,7p' "$0"; exit 1; }
for c in "$@"; do [ "$c" = Batalla ] && { echo "Batalla va con tasks/pruebas/correr_batalla.sh"; exit 1; }; done
CATEGORIAS=$(IFS=';'; echo "$*")
inicio=$(date +%s)
bash "$(dirname "$0")/correr.sh" "ConquestTactics.Mundo.UI.Tests" "" "$CATEGORIAS"
echo "correr_ui ($CATEGORIAS) en $(( $(date +%s) - inicio )) s"
