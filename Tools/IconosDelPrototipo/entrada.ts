// Saca los iconos provisionales de BronzeAgeClient a SVG para Conquest (lo empaqueta esbuild; lo corre `generar.mjs`).
// Los dibujos no se copian a mano: salen de los mismos módulos del prototipo.
import { iconoEdificio } from '../../../BronzeAgeClient/src/ui/iconosEdificio';
import { iconoTropa, TROPAS_CON_ICONO } from '../../../BronzeAgeClient/src/ui/iconosTropa';
import { ICONOS_MAPA } from '../../../BronzeAgeClient/src/mundo/iconosMapa';
import { RECURSO_ICONO, RECURSO_COLOR } from '../../../BronzeAgeClient/src/paletas';
import { iconoDeParte } from '../../../BronzeAgeClient/src/ui/hudInterior';

declare const process: { stdout: { write(s: string): void }; env: Record<string, string> };

// Las claves de los edificios: las de su tabla de dibujos (el módulo no las exporta; se leen del propio fuente en generar.mjs).
const EDIFICIOS: string[] = JSON.parse(process.env.EDIFICIOS ?? "[]");
const PARTES: string[] = JSON.parse(process.env.PARTES ?? "[]");

const salida = {
  // `_otro`: lo que el prototipo dibuja para lo que no tiene dibujo propio.
  edificio: Object.fromEntries([...EDIFICIOS, '_otro'].map((t) => [t, iconoEdificio(t, 32)])),
  tropa: Object.fromEntries([...TROPAS_CON_ICONO, '_otro'].map((t) => [t, iconoTropa(t, 32)])),
  parte: Object.fromEntries(PARTES.map((t) => [t, iconoDeParte(t, 32)])),
  mapa: Object.fromEntries(Object.entries(ICONOS_MAPA).map(([id, i]) => [id, { d: i.d, autor: i.autor, icono: i.icono }])),
  recurso: RECURSO_ICONO,
  colorRecurso: RECURSO_COLOR,
};
process.stdout.write(JSON.stringify(salida));
