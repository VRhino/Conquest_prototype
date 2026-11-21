// Los nodos de recursos del mundo de pruebas, desde el artefacto (solo lectura): cuántos de cada tipo, su rareza, su cantidad inicial, cuántos
// caen en bosque cerrado, en ladera fuerte o pegados a un río o a la costa, y las vetas. Con --json <fichero>, la lista (para el banco).
//   node tasks/referencias/nodos.cjs <mundo.bin> [--json salida.json]
const fs = require('fs'), zlib = require('zlib');
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
const txt = v => Buffer.from(v).toString('utf8');
function punto(v) { let x = 0, y = 0; for (const [f, c] of campos(v)) { if (f == 1) x = zz(c) / 1000; if (f == 2) y = zz(c) / 1000 } return [x, y] }
function raster(v) {
  const r = { col: 0, fil: 0, celda: 0, enc: 0, datos: null, zstd: false };
  for (const [f, c] of campos(v)) { if (f == 1) r.col = c; if (f == 2) r.fil = c; if (f == 3) r.celda = c / 1000; if (f == 4) r.enc = c; if (f == 5) r.datos = c; if (f == 6) r.zstd = !!c }
  if (r.datos && r.zstd) r.datos = zlib.zstdDecompressSync(r.datos);
  r.bpp = r.datos ? r.datos.length / (r.col * r.fil) : 0;
  r.en = (x, y) => { const i = Math.min(r.col - 1, Math.max(0, Math.floor(x / r.celda))), j = Math.min(r.fil - 1, Math.max(0, Math.floor(y / r.celda)));
    return r.bpp == 2 ? r.datos.readUInt16LE((j * r.col + i) * 2) : r.datos[j * r.col + i] };
  return r;
}
const nodos = [], vetas = [], rios = [], R = {};
for (const [f, v] of campos(b)) {
  if (f == 2) R.altura = raster(v); if (f == 3) R.terreno = raster(v); if (f == 6) R.bosque = raster(v);
  if (f == 11) { const n = { id: '', tipo: '', rareza: '', x: 0, y: 0, cantidad: 0, anillo: '' };
    for (const [g, c] of campos(v)) { if (g == 1) n.id = txt(c); if (g == 3) n.tipo = txt(c); if (g == 4) n.rareza = txt(c); if (g == 5) [n.x, n.y] = punto(c); if (g == 6) n.cantidad = zz(c); if (g == 9) n.anillo = txt(c) }
    nodos.push(n) }
  if (f == 12) { const t = { id: '', nodos: [] }; for (const [g, c] of campos(v)) { if (g == 1) t.id = txt(c); if (g == 3) t.nodos.push(txt(c)) } vetas.push(t) }
  if (f == 9) { const r = []; for (const [g, c] of campos(v)) if (g == 2) r.push(punto(c)); rios.push(r) }
}
console.log('rasters', Object.fromEntries(Object.entries(R).map(([k, r]) => [k, `${r.col}x${r.fil} celda ${r.celda} bpp ${r.bpp}`])));
// la altura en metros: el rango del generador no viene aquí; se usa la pendiente relativa del raster (u16) y se informa del percentil
const por = {};
for (const n of nodos) { const p = por[n.tipo] ??= { n: 0, rareza: {}, min: Infinity, max: -Infinity, anillo: {} }; p.n++; p.rareza[n.rareza] = (p.rareza[n.rareza] ?? 0) + 1; p.anillo[n.anillo] = (p.anillo[n.anillo] ?? 0) + 1; p.min = Math.min(p.min, n.cantidad); p.max = Math.max(p.max, n.cantidad) }
console.log('nodos', nodos.length); console.table(Object.fromEntries(Object.entries(por).map(([k, p]) => [k, { n: p.n, rareza: JSON.stringify(p.rareza), cantidad: `${p.min}–${p.max}`, anillo: JSON.stringify(p.anillo) }])));
// río: distancia a la polilínea de cualquier río (puntos densos)
const celdaR = 4, rejilla = new Map();
for (const r of rios) for (let i = 0; i + 1 < r.length; i++) { const [ax, ay] = r[i], [bx, by] = r[i + 1], L = Math.hypot(bx - ax, by - ay), k = Math.max(1, Math.ceil(L / 0.5));
  for (let s = 0; s <= k; s++) { const x = ax + (bx - ax) * s / k, y = ay + (by - ay) * s / k, c = `${Math.floor(x / celdaR)},${Math.floor(y / celdaR)}`; (rejilla.get(c) ?? rejilla.set(c, []).get(c)).push([x, y]) } }
const aRio = (x, y) => { let d = Infinity; for (let i = -1; i <= 1; i++) for (let j = -1; j <= 1; j++) for (const [px, py] of rejilla.get(`${Math.floor(x / celdaR) + i},${Math.floor(y / celdaR) + j}`) ?? []) d = Math.min(d, Math.hypot(px - x, py - y)); return d };
const T = R.terreno, A = R.altura, B = R.bosque;
const cuenta = { bosqueCerrado: 0, bosqueAlgo: 0, montana: 0, laderaFuerte: 0, rio3: 0, costa: 0 }, pendientes = [];
const pend = (x, y) => { const h = A.celda; return Math.hypot(A.en(x + h, y) - A.en(x - h, y), A.en(x, y + h) - A.en(x, y - h)) / 2 };
for (const n of nodos) pendientes.push(pend(n.x, n.y));
const umbral = [...pendientes].sort((a, b) => a - b)[Math.floor(pendientes.length * 0.9)];
const det = {};
nodos.forEach((n, i) => {
  const d = B.en(n.x, n.y) / 255, t = T.en(n.x, n.y), c = det[n.tipo] ??= { bosqueCerrado: 0, montana: 0, rio3: 0, costa: 0 };
  if (d >= 0.7) { cuenta.bosqueCerrado++; c.bosqueCerrado++ } else if (d >= 0.3) cuenta.bosqueAlgo++;
  if (t >= 5) { cuenta.montana++; c.montana++ }
  if (pendientes[i] >= umbral) cuenta.laderaFuerte++;
  let costa = false; for (let a = 0; a < 8 && !costa; a++) { const x = n.x + 2 * Math.cos(a * Math.PI / 4), y = n.y + 2 * Math.sin(a * Math.PI / 4); if (T.en(x, y) <= 2) costa = true }
  if (costa) { cuenta.costa++; c.costa++ }
  n.rio = aRio(n.x, n.y); if (n.rio < 3) { cuenta.rio3++; c.rio3++ }
  n.densidad = d; n.terreno = t; n.pendiente = pendientes[i];
});
console.log('en el sitio (bosque cerrado = densidad ≥ 0,7; montaña = clase montaña o cima; ladera fuerte = el 10 % más empinado; río a < 3 u; costa o agua a ≤ 2 u)');
console.log(cuenta); console.table(det);
console.log('vetas', vetas.length, 'nodos por veta', vetas.map(v => v.nodos.length).join(' '));
if (salida) fs.writeFileSync(salida, JSON.stringify({ nodos, vetas }));
