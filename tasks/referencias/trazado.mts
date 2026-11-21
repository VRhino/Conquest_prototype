// Referencias para las pruebas de EditMode del trazado de caminos y fronteras (`TrazadoDelMapaTests`): ejecuta las funciones del prototipo
// (BronzeAgeClient, solo lectura) sobre unos casos fijos y escribe lo que dan en `Assets/Tests/EditMode/Mundo/Datos/trazado_referencia.json`.
// La textura de los caminos: el pintor de `caminos.ts` no se exporta; se copia su fuente a un fichero temporal (fuera de los dos repos), sin
// la importación de three y exportando `sendero` y `camino`.
//   node --experimental-transform-types --no-warnings tasks/referencias/trazado.mts
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';
import { deflateRawSync } from 'node:zlib';

const CLIENTE = 'C:/Users/VRINO/Desarrollo/BronzeAgeClient/src';
const { suavizarCamino, tramosEnCaja, recorridos, sentidoDe, circulo } = await import(pathToFileURL(`${CLIENTE}/mundo3d/lineas.ts`).href);
const { partirFronteras } = await import(pathToFileURL(`${CLIENTE}/mundo/fronteras.ts`).href);
const { poligonoDeZona, zonasDeLoRecordado } = await import(pathToFileURL(`${CLIENTE}/mundo/zonasRecordadas.ts`).href);
const { refuerzo } = await import(pathToFileURL(`${CLIENTE}/mundo3d/territorio.ts`).href);

type P = { x: number; y: number };

// --- el pintor de la textura, copiado sin three ---
const fuente = readFileSync(`${CLIENTE}/mundo3d/caminos.ts`, 'utf8').replace(/^import \* as THREE from 'three';$/m, 'const THREE: any = {};') + '\nexport { sendero, camino };\n';
const tmp = join(tmpdir(), 'conquest-caminos-referencia');
mkdirSync(tmp, { recursive: true });
writeFileSync(join(tmp, 'caminos.mts'), fuente);
const { sendero, camino } = await import(pathToFileURL(join(tmp, 'caminos.mts')).href);
function pintar(f: () => (u: number, v: number) => number[]): string {
  const p = f(), datos = new Uint8Array(128 * 512 * 4);
  for (let y = 0; y < 512; y++)
    for (let x = 0; x < 128; x++) {
      const [r, g, b, a] = p((x + 0.5) / 128, (y + 0.5) / 512), i = 4 * (y * 128 + x);
      datos[i] = Math.max(0, Math.min(255, Math.round(r!)));
      datos[i + 1] = Math.max(0, Math.min(255, Math.round(g!)));
      datos[i + 2] = Math.max(0, Math.min(255, Math.round(b!)));
      datos[i + 3] = Math.max(0, Math.min(255, Math.round(a! * 255)));
    }
  return deflateRawSync(datos).toString('base64');
}

// --- caminos: escaleras de la red del servidor (cada 4 u, ocho direcciones) ---
const DIRS = [[4, 0], [4, 4], [0, 4], [-4, 4], [-4, 0], [-4, -4], [0, -4], [4, -4]];
function escalera(semilla: number, n: number, x0: number, y0: number): P[] {
  let s = semilla, d = semilla % 8;
  const r = () => ((s = (s * 1103515245 + 12345) % 2147483648) / 2147483648);
  const out: P[] = [{ x: x0, y: y0 }];
  for (let k = 0; k < n; k++) {
    const u = r();
    d = (d + (u < 0.2 ? 1 : u < 0.4 ? 7 : 0)) % 8;
    const [dx, dy] = DIRS[d]!, p = out[out.length - 1]!;
    out.push({ x: p.x + dx!, y: p.y + dy! });
  }
  return out;
}
const caminos = [
  { puntos: [{ x: 0, y: 0 }, { x: 4, y: 0 }], fijos: [] as number[] },
  { puntos: [{ x: 0, y: 0 }, { x: 4, y: 0 }, { x: 8, y: 4 }], fijos: [] as number[] },
  { puntos: escalera(7, 12, 100, 200), fijos: [] as number[] },
  { puntos: escalera(13, 25, -40, 30), fijos: [] as number[] },
  { puntos: escalera(29, 30, 512, 768), fijos: [6, 14, 22] },
  { puntos: escalera(31, 18, 1.5, 2.5), fijos: [9] },
];
const salidaCaminos = caminos.map((c) => ({ puntos: c.puntos, fijos: c.fijos, suave: suavizarCamino(c.puntos, c.fijos), recorridos: recorridos(c.puntos) }));
const cajas = [
  { puntos: escalera(13, 25, -40, 30), caja: [-30, 20, 0, 60] },
  { puntos: escalera(29, 30, 512, 768), caja: [520, 770, 560, 800] },
];
const salidaCajas = cajas.map((c) => ({ ...c, tramos: tramosEnCaja(c.puntos, c.caja[0], c.caja[1], c.caja[2], c.caja[3]) }));

// --- zonas: discos recortados, como manda el motor ---
const disco = (x: number, y: number, r: number, f: string, id: string) => ({ centro: { x, y }, radio: r, faccionId: f, id });
const discos = [disco(0, 0, 12, 'f-1a', 'a'), disco(18, 2, 11, 'f-1b', 'b'), disco(-5, 22, 13, 'f-2', 'c'), disco(40, -10, 9, 'f-3', 'd')];
const poligonos = discos.map((d) => poligonoDeZona(d, discos.filter((o) => o !== d)));
const zonas = [
  { faccionId: 'f-1', contorno: poligonos[0]! },
  { faccionId: 'f-1', contorno: poligonos[1]! },
  { faccionId: 'f-2', contorno: poligonos[2]! },
  { faccionId: 'f-3', contorno: poligonos[3]! },
  { faccionId: 'f-4', contorno: circulo({ x: 90, y: 90 }, 7) },
];
const fronteras = partirFronteras(zonas);
const recordadas = zonasDeLoRecordado(
  [{ id: 'av1', posicion: { x: 0, y: 0 }, zona: poligonos[0]!, faccionId: 'f-1' }, { id: 'av2', posicion: { x: 30, y: 30 }, zona: circulo({ x: 30, y: 30 }, 10).slice(0, -1), faccionId: '' }] as any,
  [{ asentamientoId: 'co1', posicion: { x: 16, y: 6 }, radioPotencial: 12, faccionId: 'f-2' }, { asentamientoId: 'co2', posicion: { x: 60, y: 0 }, radioPotencial: 8, faccionId: '' }, { asentamientoId: 'co3', posicion: { x: 0, y: 40 }, faccionId: 'f-5' }] as any,
  [{ id: 'pr1', posicion: { x: 35, y: 8 }, faccionId: 'f-1', zonaReal: circulo({ x: 35, y: 8 }, 9).slice(0, -1) }] as any,
);

const salida = {
  caminos: salidaCaminos,
  cajas: salidaCajas,
  discos,
  poligonos,
  zonas,
  fronteras,
  sentidos: poligonos.map((p) => (p.length ? sentidoDe(p) : 0)),
  recordadas: Object.fromEntries(recordadas),
  refuerzos: ['#000000', '#1a1a2e', '#2980b9', '#c0392b', '#f1c40f', '#ffffff'].map((c) => ({ color: c, refuerzo: refuerzo(c) })),
  texturas: { sendero: pintar(sendero), camino: pintar(camino) },
};
const destino = 'Assets/Tests/EditMode/Mundo/Datos';
mkdirSync(destino, { recursive: true });
writeFileSync(`${destino}/trazado_referencia.json`, JSON.stringify(salida));
console.log('referencias:', salidaCaminos.length, 'caminos,', fronteras.length, 'tramos de frontera,', recordadas.size, 'zonas recordadas');
