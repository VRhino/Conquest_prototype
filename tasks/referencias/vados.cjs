// Los vados del mundo de pruebas, desde el grafo de navegación del artefacto (solo lectura): las aristas con `vado`, agrupadas en grupos
// conexos; de cada grupo, el nodo del vado (el de más aristas de vado), su río, el punto de `Rio.vados` más cercano y cuánto ocupa el grupo a lo
// largo del cauce (proyectado sobre la dirección del río junto al vado). Saca el resumen y, con --json <fichero>, los trechos.
//   node tasks/referencias/vados.cjs <artefacto.bin> [--json salida.json]
const fs = require('fs');
const b = fs.readFileSync(process.argv[2]);
const salida = process.argv.includes('--json') ? process.argv[process.argv.indexOf('--json') + 1] : null;

function vi(b, i) { let r = 0, s = 0, c; do { c = b[i++]; r += (c & 0x7f) * 2 ** s; s += 7 } while (c >= 0x80); return [r, i] }
function campos(b) {
  const o = []; let i = 0;
  while (i < b.length) {
    let k; [k, i] = vi(b, i); const f = Math.floor(k / 8), w = k & 7; let v;
    if (w == 0) [v, i] = vi(b, i);
    else if (w == 2) { let n; [n, i] = vi(b, i); v = b.subarray(i, i + n); i += n }
    else if (w == 1) { v = b.subarray(i, i + 8); i += 8 }
    else if (w == 5) { v = b.subarray(i, i + 4); i += 4 }
    else throw new Error('tipo ' + w);
    o.push([f, v]);
  }
  return o;
}
const zz = n => (n % 2 ? -(n + 1) / 2 : n / 2);
function punto(v) { let x = 0, y = 0; for (const [f, c] of campos(v)) { if (f == 1) x = zz(c) / 1000; if (f == 2) y = zz(c) / 1000 } return [x, y] }

const rios = [], nodos = new Map(), aristas = [];
for (const [f, v] of campos(b)) {
  if (f == 9) {
    const r = { id: '', puntos: [], vados: [], navegable: false };
    for (const [g, c] of campos(v)) {
      if (g == 1) r.id = Buffer.from(c).toString();
      if (g == 2) r.puntos.push(punto(c));
      if (g == 4) r.navegable = !!c;
      if (g == 5) r.vados.push(punto(c));
    }
    rios.push(r);
  }
  if (f == 16) for (const [g, c] of campos(v)) {
    if (g == 1) { let id = 0, p = [0, 0]; for (const [h, d] of campos(c)) { if (h == 1) id = d; if (h == 2) p = punto(d) } nodos.set(id, p) }
    if (g == 2) { const a = { a: 0, b: 0, vado: false }; for (const [h, d] of campos(c)) { if (h == 1) a.a = d; if (h == 2) a.b = d; if (h == 4) a.vado = !!d } if (a.vado) aristas.push(a) }
  }
}

// grupos conexos de aristas de vado
const padre = new Map();
const raiz = x => { while (padre.get(x) !== x) { padre.set(x, padre.get(padre.get(x))); x = padre.get(x) } return x };
for (const a of aristas) for (const n of [a.a, a.b]) if (!padre.has(n)) padre.set(n, n);
for (const a of aristas) padre.set(raiz(a.a), raiz(a.b));
const grupos = new Map();
for (const a of aristas) { const r = raiz(a.a); if (!grupos.has(r)) grupos.set(r, { aristas: [], nodos: new Set() }); const g = grupos.get(r); g.aristas.push(a); g.nodos.add(a.a); g.nodos.add(a.b) }

const d2 = (p, q) => (p[0] - q[0]) ** 2 + (p[1] - q[1]) ** 2;
function rioCercano(p) {
  let mejor = null;
  for (const r of rios) for (let i = 0; i + 1 < r.puntos.length; i++) {
    const a = r.puntos[i], c = r.puntos[i + 1], dx = c[0] - a[0], dy = c[1] - a[1], l = dx * dx + dy * dy;
    const t = l > 0 ? Math.max(0, Math.min(1, ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / l)) : 0;
    const q = [a[0] + t * dx, a[1] + t * dy], d = Math.sqrt(d2(p, q));
    if (!mejor || d < mejor.d) {
      // a lo largo del cauce: lo recorrido desde el nacimiento hasta q
      let s = 0; for (let k = 0; k < i; k++) s += Math.sqrt(d2(r.puntos[k], r.puntos[k + 1]));
      s += Math.sqrt(l) * t;
      mejor = { rio: r, d, dir: [dx / Math.sqrt(l || 1), dy / Math.sqrt(l || 1)], s };
    }
  }
  return mejor;
}

const vadosRio = rios.flatMap(r => r.vados.map(v => ({ rio: r, p: v })));
const trechos = [];
for (const g of grupos.values()) {
  const grado = new Map();
  for (const a of g.aristas) for (const n of [a.a, a.b]) grado.set(n, (grado.get(n) || 0) + 1);
  const centro = [...grado.entries()].sort((x, y) => y[1] - x[1])[0][0];
  const pc = nodos.get(centro);
  const rc = rioCercano(pc);
  // a lo largo del cauce: la extensión de los nodos del grupo proyectados sobre la dirección del río
  const proy = [...g.nodos].map(n => { const p = nodos.get(n); return (p[0] - pc[0]) * rc.dir[0] + (p[1] - pc[1]) * rc.dir[1] });
  const largo = Math.max(...proy) - Math.min(...proy);
  let vado = null; for (const v of vadosRio) { const d = Math.sqrt(d2(v.p, pc)); if (!vado || d < vado.d) vado = { d, v } }
  const nacimiento = Math.sqrt(d2(rc.rio.puntos[0], pc));
  trechos.push({ rio: rc.rio.id, navegable: rc.rio.navegable, centro: pc, nodos: g.nodos.size, aristas: g.aristas.length, largo, desde: Math.min(...proy), hasta: Math.max(...proy),
                 dir: rc.dir, aRio: rc.d, s: rc.s, aVadoDelRio: vado ? vado.d : null, aNacimiento: nacimiento });
}
const largos = trechos.map(t => t.largo).sort((a, b) => a - b);
const media = largos.reduce((a, b) => a + b, 0) / largos.length;
console.log(`ríos ${rios.length}; Rio.vados ${vadosRio.length}; aristas de vado ${aristas.length}; grupos ${trechos.length}`);
console.log(`trecho a lo largo del cauce (u): media ${media.toFixed(1)}, mínimo ${largos[0].toFixed(1)}, máximo ${largos[largos.length - 1].toFixed(1)}, mediana ${largos[largos.length >> 1].toFixed(1)}`);
const nodosPorGrupo = trechos.map(t => t.nodos).sort((a, b) => a - b);
console.log(`nodos por grupo: ${nodosPorGrupo[0]}..${nodosPorGrupo[nodosPorGrupo.length - 1]}; aristas por grupo: ${Math.min(...trechos.map(t => t.aristas))}..${Math.max(...trechos.map(t => t.aristas))}`);
const lejos = trechos.filter(t => t.aVadoDelRio > 0.5);
console.log(`grupos cuyo nodo central no está en un punto de Rio.vados (> 0,5 u): ${lejos.length}`);
const sinGrupo = vadosRio.filter(v => !trechos.some(t => Math.sqrt(d2(t.centro, v.p)) < 0.5));
console.log(`puntos de Rio.vados sin grupo en el grafo: ${sinGrupo.length}`);
const cercaNacimiento = trechos.filter(t => t.aNacimiento < 15);
console.log(`grupos a menos de 15 u del nacimiento de su río: ${cercaNacimiento.length}` + (cercaNacimiento.length ? ` (a ${cercaNacimiento.map(t => t.aNacimiento.toFixed(1)).join(', ')} u)` : ''));
const nav = trechos.filter(t => t.navegable);
console.log(`en ríos navegables: ${nav.length}`);
if (salida) fs.writeFileSync(salida, JSON.stringify(trechos.map(t => ({ ...t, centro: t.centro.map(v => +v.toFixed(3)) })), null, 1));

// Cada punto de Rio.vados: el grupo de aristas de vado más cercano (por su nodo más cercano) y si otro punto comparte grupo.
{
  const porGrupo = new Map();
  const sueltos = [];
  for (const v of vadosRio) {
    let mejor = null;
    for (const [r, g] of grupos) for (const n of g.nodos) { const d = Math.sqrt(d2(nodos.get(n), v.p)); if (!mejor || d < mejor.d) mejor = { d, r } }
    if (!mejor || mejor.d > 12) { sueltos.push({ rio: v.rio.id, p: v.p, d: mejor ? mejor.d : null }); continue }
    if (!porGrupo.has(mejor.r)) porGrupo.set(mejor.r, []);
    porGrupo.get(mejor.r).push(v);
  }
  const fundidos = [...porGrupo.values()].filter(l => l.length > 1);
  console.log(`puntos de Rio.vados con grupo a menos de 12 u: ${vadosRio.length - sueltos.length}; grupos con más de un punto: ${fundidos.length} (${fundidos.map(l => l.length).join(', ')} puntos)`);
  for (const l of fundidos) console.log('  fundidos:', l.map(v => `${v.rio.id} (${v.p[0].toFixed(1)}, ${v.p[1].toFixed(1)})`).join(' y '), 'a', Math.sqrt(d2(l[0].p, l[1].p)).toFixed(1), 'u');
  console.log(`puntos sin grupo a menos de 12 u: ${sueltos.length}`);
  for (const s of sueltos) console.log(`  ${s.rio} (${s.p[0].toFixed(3)}, ${s.p[1].toFixed(3)}), grupo más cercano a ${s.d == null ? '—' : s.d.toFixed(1)} u`);
}
