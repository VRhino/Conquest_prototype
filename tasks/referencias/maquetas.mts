// Referencias para la prueba de EditMode de las maquetas de asentamientos «Prototipo» (`MaquetasDelMapaTests`): ejecuta las funciones de
// `maquetas.ts` y `huellas.ts` del prototipo (BronzeAgeClient, solo lectura) sobre todos sus casos y escribe cada pieza (material, posición,
// tamaño, giro, inclinación y base) y cada bandera, y las huellas, en `Assets/Tests/EditMode/Mundo/Datos/maquetas_referencia.json`. Sus
// módulos importan sin extensión y no cargan sueltos: se copian a un fichero temporal (fuera de los dos repos) con `MUNDO3D` sacado de
// `config.ts` y exportando lo que no se exporta.
//   node --experimental-transform-types --no-warnings tasks/referencias/maquetas.mts
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';

const CLIENTE = 'C:/Users/VRINO/Desarrollo/BronzeAgeClient';
const leer = (p: string) => readFileSync(`${CLIENTE}/${p}`, 'utf8').replace(/\r\n/g, '\n');
const three = pathToFileURL(`${CLIENTE}/node_modules/three/build/three.module.js`).href;
// MUNDO3D: solo las escalas de las maquetas, de `config.ts`
const config = leer('src/mundo3d/config.ts');
const num = (k: string) => Number(new RegExp(`\\n\\s*${k}: ([\\d.]+),`).exec(config)![1]);
const MUNDO3D = { maqueta: num('maqueta'), campamento: num('campamento'), alijo: num('alijo'), pueblo: num('pueblo'), avanzada: num('avanzada'), altoPlaza: num('altoPlaza') };
const tmp = join(tmpdir(), 'conquest-maquetas-referencia');
mkdirSync(tmp, { recursive: true });
const mundo = `export const MUNDO3D = ${JSON.stringify(MUNDO3D)};\n`;
writeFileSync(join(tmp, 'config.mts'), mundo);
writeFileSync(join(tmp, 'huellas.mts'), leer('src/mundo3d/huellas.ts').replace("import { MUNDO3D } from './config';", "import { MUNDO3D } from './config.mts';"));
const maquetas = leer('src/mundo3d/maquetas.ts')
  .replace("import * as THREE from 'three';", `import * as THREE from '${three}';`)
  .replace(/^import type .*$/gm, '')
  .replace("import { MUNDO3D } from './config';", "import { MUNDO3D } from './config.mts';")
  .replace("from './huellas';", "from './huellas.mts';")
  + '\nexport { Piezas, ciudad, pueblo, avanzada, obra, campamento, bandidos, alijo, batalla, hash, azar, semilla };\n';
writeFileSync(join(tmp, 'maquetas.mts'), maquetas);
const M = await import(pathToFileURL(join(tmp, 'maquetas.mts')).href);
const H = await import(pathToFileURL(join(tmp, 'huellas.mts')).href);

type P = { x: number; y: number; ancho: number; fondo: number; alto: number; giro: number; base: number; inclina?: number };
function volcar(hacer: (p: any) => void) {
  const p = new M.Piezas();
  hacer(p);
  const piezas = [...p.por].flatMap(([mat, l]: [string, P[]]) => l.map((q) => ({ mat, ...q, inclina: q.inclina ?? 0 })));
  const banderas = [...p.banderas].flatMap(([color, l]: [string, P[]]) => l.map((q) => ({ color, ...q, inclina: q.inclina ?? 0 })));
  return { piezas, banderas };
}

const casos: any[] = [];
const C = '#3a6ea5';
// plazas: niveles 1 a 5, sin saber de la muralla (recordada), con ella y sin ella, en una plaza normal (rc 3) y en una grande (rc 6.5)
for (const rc of [3, 6.5])
  for (let nivel = 1; nivel <= 5; nivel++)
    for (const muralla of [undefined, true, false]) {
      const x = 812.37 + nivel * 11.3 + rc, y = 433.91 - nivel * 7.7;
      const sem = M.semilla(x, y);
      casos.push({ tipo: 'ciudad', x, y, rc, nivel, muralla: muralla ?? null, sem, color: C, ...volcar((p) => M.ciudad(p, x, y, rc, nivel, C, sem, muralla)) });
    }
// pueblos: el extractor y la aldea con cada cultivo (y sin saberlo)
for (const [aldea, cultivo] of [[false, undefined], [true, undefined], [true, 'trigo'], [true, 'vid'], [true, 'oliva'], [true, 'cebada'], [true, 'sesamo'], [true, 'ganado']] as const) {
  const x = 1201.55 + (cultivo?.length ?? 0) * 3.1, y = 987.12 + (aldea ? 5 : 0);
  const sem = M.semilla(x, y);
  casos.push({ tipo: 'pueblo', x, y, aldea, cultivo: cultivo ?? null, sem, color: C, ...volcar((p) => M.pueblo(p, x, y, aldea, C, sem, cultivo)) });
}
// avanzadas: las tres etapas, en pie y en ruinas
for (let etapa = 1; etapa <= 3; etapa++)
  for (const ruinas of [false, true]) {
    const x = 640.25 + etapa * 4, y = 1500.75 - (ruinas ? 3 : 0);
    casos.push({ tipo: 'avanzada', x, y, etapa, ruinas, color: C, ...volcar((p) => M.avanzada(p, x, y, etapa, ruinas, C)) });
  }
for (const arrancada of [false, true]) {
  const x = 300.5, y = 200.25 + (arrancada ? 2 : 0);
  casos.push({ tipo: 'obra', x, y, arrancada, color: C, ...volcar((p) => M.obra(p, x, y, arrancada, C)) });
}
{ const x = 2000.4, y = 100.6, sem = M.semilla(x, y); casos.push({ tipo: 'campamento', x, y, sem, ...volcar((p) => M.campamento(p, x, y, sem)) }); }
for (let nivel = 1; nivel <= 3; nivel++) {
  const x = 1500.3 + nivel, y = 1600.7, sem = M.semilla(x, y);
  casos.push({ tipo: 'bandidos', x, y, nivel, sem, ...volcar((p) => M.bandidos(p, x, y, nivel, sem)) });
}
{ const x = 50.5, y = 60.25; casos.push({ tipo: 'alijo', x, y, ...volcar((p) => M.alijo(p, x, y)) }); }
for (const propia of [false, true]) { const x = 70.5, y = 80.25; casos.push({ tipo: 'batalla', x, y, propia, ...volcar((p) => M.batalla(p, x, y, propia)) }); }

// el hash, el azar y la semilla a secas, también con valores grandes y negativos
const hashes = [[0, 0, 0], [5, 7, 11], [-3, 1e6, 2], [123456, -789, 42], [2147483647, 1, -1]].map(([a, b, c]) => [a, b, c, M.hash(a, b, c), M.azar(a, b, c)]);
const semillas = [[812.37, 433.91], [-1.25, 3.5], [2666.9, 1999.9]].map(([x, y]) => [x, y, M.semilla(x, y)]);
const huellas = {
  plaza: [[3, 1], [3, 2], [3, 3], [6.5, 5]].map(([rc, n]) => [rc, n, H.radioDePlaza(rc, n)]),
  pueblo: H.radioDePueblo(false), campos: H.radioDeCampos(), avanzada: [1, 2, 3].map((e) => [e, H.radioDeAvanzada(e)]), obra: H.radioDeObra(),
  campamento: H.radioDeCampamento(), bandidos: H.radioDeBandidos(), alijo: H.radioDeAlijo(), batalla: H.radioDeBatalla(), holgura: H.HOLGURA_ARO,
};
const destino = 'Assets/Tests/EditMode/Mundo/Datos/maquetas_referencia.json';
writeFileSync(destino, JSON.stringify({ mundo3d: MUNDO3D, casos, hashes, semillas, huellas }));
console.log(destino, casos.length, 'casos', casos.reduce((a, c) => a + c.piezas.length, 0), 'piezas', casos.reduce((a, c) => a + c.banderas.length, 0), 'banderas');
