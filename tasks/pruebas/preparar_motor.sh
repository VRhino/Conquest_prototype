#!/bin/bash
# El motor de las pruebas: un clon aparte de BronzeAge (nunca el checkout de Víctor, ../BronzeAgeFase0) en ../BronzeAgeMotorPruebas, al día con
# origin/main, y el servidor y el lanzador compilados en su target. Desde la carpeta de Conquest:
#   bash tasks/pruebas/preparar_motor.sh
CQ=$(cd "$(dirname "$0")/../.." && pwd)
MOTOR=${MOTOR:-$(cd "$CQ/.." && pwd)/BronzeAgeMotorPruebas}
if [ ! -d "$MOTOR/.git" ]; then git clone https://github.com/VRhino/BronzeAge.git "$MOTOR"; fi
git -C "$MOTOR" fetch origin main
git -C "$MOTOR" checkout --detach origin/main
git -C "$MOTOR" log -1 --format="motor de pruebas en %h (%ci)"
cargo build --release -p servicio -p lanzador --manifest-path "$MOTOR/Cargo.toml" --target-dir "$MOTOR/target"
