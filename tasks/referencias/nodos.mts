// Referencias para la prueba de EditMode de los nodos de recursos «Prototipo» (`NodosDelMapaTests`): ejecuta el bloque de los nodos de
// `paisaje.ts` del prototipo (BronzeAgeClient, solo lectura) sobre unos casos fijos y escribe cada roca (posición, giro, escala y color) en
// `Assets/Tests/EditMode/Mundo/Datos/nodos_referencia.json`. `azar`, la roca y el color no se exportan: se copian de su fuente a un fichero
// temporal (fuera de los dos repos) junto con el bloque que llena el InstancedMesh, tal cual, con su three.
//   node --experimental-transform-types --no-warnings tasks/referencias/nodos.mts
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';

const CLIENTE = 'C:/Users/VRINO/Desarrollo/BronzeAgeClient';
const url = (p: string) => pathToFileURL(`${CLIENTE}/${p}`).href;
const s = readFileSync(`${CLIENTE}/src/mundo3d/paisaje.ts`, 'utf8').replace(/\r\n/g, '\n');
const trozo = (desde: string, hasta: string) => { const i = s.indexOf(desde), j = s.indexOf(hasta, i); if (i < 0 || j < 0) throw new Error(desde); return s.slice(i, j + hasta.length); };
const azar = trozo('const azar = ', '};');
const roca = trozo('const ROCA = ', '\n};');
const bloque = trozo('    if (nodos.length) {', '      g.add(rocas);\n    }').replaceAll('this.alturaU', 'alturaU').replaceAll('this.roca', 'new THREE.MeshBasicMaterial()');
const COLOR = trozo('const COLOR = ', ';');
// la paleta y la escala, de su fuente (sus módulos importan sin extensión y no cargan sueltos)
const paletas = readFileSync(`${CLIENTE}/src/paletas.ts`, 'utf8').replace(/\r\n/g, '\n');
const RECURSO = paletas.slice(paletas.indexOf('export const RECURSO_COLOR'), paletas.indexOf('\n};', paletas.indexOf('export const RECURSO_COLOR')) + 3).replace('export ', '');
const NODO = /\n\s*nodo: ([\d.]+),/.exec(readFileSync(`${CLIENTE}/src/mundo3d/config.ts`, 'utf8').replace(/\r\n/g, '\n'))![1];
const fuente = `import * as THREE from '${url('node_modules/three/build/three.module.js')}';
${RECURSO}
const MUNDO3D = { nodo: ${NODO} };
${COLOR}
${azar}
${roca}
export function rocas(nodos: any[], alturaU: (x: number, y: number) => number) {
  const g = { add(_: any) {} }; let r: any = null;
${bloque.replace('g.add(rocas);', 'g.add(rocas); r = rocas;')}
  return r;
}
export { ROCA, azar };
`;
const tmp = join(tmpdir(), 'conquest-nodos-referencia');
mkdirSync(tmp, { recursive: true });
writeFileSync(join(tmp, 'paisaje-nodos.mts'), fuente);
const { rocas, ROCA, azar: azarDelPrototipo } = await import(pathToFileURL(join(tmp, 'paisaje-nodos.mts')).href);
const THREE = await import(url('node_modules/three/build/three.module.js'));

// casos: cada tipo del mundo (y uno sin color) en posiciones con decimales y negativas; el suelo, una ladera
const alturaU = (x: number, y: number) => 0.1 * x - 0.05 * y + 2;
const tipos = ['piedra', 'cobre', 'estano', 'oro', 'hierro', 'livestock', 'desconocido'];
const casos: any[] = [];
let k = 0;
for (const tipo of tipos)
  for (const [x, y] of [[123.4567, 987.654], [-12.3, 45.678], [0.03, -0.07], [1999.99, 1500.01]])
    casos.push({ id: `n${k++}`, tipo, posicion: { x: x + k * 0.137, y: y - k * 0.091 } });
const salida = casos.map((n) => {
  const m = rocas([n], alturaU), M = new THREE.Matrix4(), p = new THREE.Vector3(), q = new THREE.Quaternion(), e = new THREE.Vector3(), c = new THREE.Color();
  const lista = [];
  for (let i = 0; i < m.count; i++) {
    m.getMatrixAt(i, M); M.decompose(p, q, e); m.getColorAt(i, c);
    lista.push({ p: [p.x, p.y, p.z], q: [q.x, q.y, q.z, q.w], s: e.x, lineal: [c.r, c.g, c.b] });
  }
  return { tipo: n.tipo, x: n.posicion.x, y: n.posicion.y, rocas: lista };
});
// la roca: un dodecaedro de radio 1 aplastado a 0,6, sin indexar (las normales planas las hace flatShading)
const pos = ROCA.getAttribute('position');
const vertices = Array.from({ length: pos.count }, (_, i) => [pos.getX(i), pos.getY(i), pos.getZ(i)]);
const destino = 'Assets/Tests/EditMode/Mundo/Datos/nodos_referencia.json';
// el azar a secas, también donde `Math.round(a · 16) | 0` se sale de 32 bits y con medios exactos (el redondeo de JavaScript)
const azares = [[0, 0], [123.4567, 1], [-12.3, 2], [1e9, 3], [-3.5e8, 4], [0.03125, 5], [-0.03125, 6], [2667.5, 7], [134217728, 1], [-1e12, 2]]
  .map(([a, b]) => [a, b, azarDelPrototipo(a, b)]);
writeFileSync(destino, JSON.stringify({ suelo: '0.1 x - 0.05 y + 2', casos: salida, roca: vertices, azar: azares }));
console.log(destino, salida.length, 'nodos', salida.reduce((a, n) => a + n.rocas.length, 0), 'rocas', vertices.length, 'vértices de la roca');
