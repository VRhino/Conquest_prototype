// Paso 11 (tanda 2): el ejército en el mapa en el prototipo (BronzeAgeClient en Vite contra mi servidor 7891), con Chrome sin pantalla, por CDP,
// a los cuatro tamaños. La escena la deja la prueba TuEjercitoEnElMapa con MUNDO_PROTO=ejercito: el Gobernador dentro de su plaza, el ejército
// abierto del otro a la puerta. La ficha de la batalla no sale aquí: sin batallas en Unity el motor las resuelve al momento.
// Uso: node ejercito-mapa.mjs <url> <servidor> <nickYo> <nickOtro> <ejercitoId> <x> <y> <carpeta>
import { spawn } from 'node:child_process';
import { writeFileSync, mkdirSync } from 'node:fs';
import { join } from 'node:path';
const [url, servidor, yo, otro, ejercito, bx, by, carpeta] = process.argv.slice(2);
mkdirSync(carpeta, { recursive: true });
const chrome = spawn('C:/Program Files/Google/Chrome/Application/chrome.exe', ['--headless=new', '--remote-debugging-port=9344', '--hide-scrollbars', `--user-data-dir=${join(carpeta, 'perfil-ejercito-mapa')}`, 'about:blank'], { stdio: 'ignore' });
const espera = (ms) => new Promise((r) => setTimeout(r, ms));
let ws, id = 0; const pend = new Map();
const cdp = (method, params = {}) => new Promise((res, rej) => { const i = ++id; pend.set(i, { res, rej }); ws.send(JSON.stringify({ id: i, method, params })); });
const evalua = async (js) => (await cdp('Runtime.evaluate', { expression: js, returnByValue: true })).result.value;
const foto = async (n) => { const { data } = await cdp('Page.captureScreenshot', { format: 'png' }); writeFileSync(join(carpeta, n + '.png'), Buffer.from(data, 'base64')); };
const tam = (w, h) => cdp('Emulation.setDeviceMetricsOverride', { width: w, height: h, deviceScaleFactor: 1, mobile: false });
const clic = (sel) => evalua(`(()=>{const e=document.querySelector(${JSON.stringify(sel)});if(!e)return "sin "+${JSON.stringify(sel)};e.click();return "ok"})()`);
const tamanos = [[1280, 720], [1600, 900], [1920, 1080], [1950, 1130]];
const fotos = async (n) => { for (const [w, h] of tamanos) { await tam(w, h); await espera(1500); await foto(`${n}_${w}x${h}`); } await tam(1600, 900); };
async function entrar(nick, vista) {
  const r = await (await fetch(`${servidor}/v1/sesiones`, { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ nick, clave: 'clave-de-prueba-1' }) })).json();
  await evalua(`(()=>{localStorage.clear();const d=${JSON.stringify({ bac_jugador_sesion_id: r.token, bac_jugador_usuario: r.nick, bac_jugador_game_id: 'mundo' })};for(const k in d)localStorage.setItem(k,d[k]);${vista ? `localStorage.setItem('bronze.vistaMapa.v2.mundo',${JSON.stringify(JSON.stringify(vista))});` : ''}})()`);
  await cdp('Page.reload'); await espera(12000);
}
// La vista guardada del mapa con el panel y la selección (`vistaMapaGuardada.ts`), centrada en (cx, cy).
const vista = (cx, cy, panel, seleccion) => ({ escala: 24, cx, cy, panel, seleccion });
try {
  let lista; for (let k = 0; k < 40 && !lista; k++) { try { lista = await (await fetch('http://127.0.0.1:9344/json')).json(); } catch { await espera(250); } }
  ws = new WebSocket(lista.find((x) => x.type === 'page').webSocketDebuggerUrl);
  await new Promise((r) => ws.addEventListener('open', r));
  ws.addEventListener('message', (m) => { const d = JSON.parse(m.data); if (d.id && pend.has(d.id)) { const p = pend.get(d.id); pend.delete(d.id); d.error ? p.rej(new Error(d.error.message)) : p.res(d.result); } });
  await cdp('Page.enable'); await cdp('Runtime.enable');
  await tam(1600, 900);
  await cdp('Page.navigate', { url }); await espera(2000);
  const x = Number(bx), y = Number(by);

  // 1. Dentro de la plaza: «Unirse a un ejército» en el riel (SIN_PLAZA=1 la salta, si ya se unió).
  if (!process.env.SIN_PLAZA) {
    await entrar(yo);
    console.log('riel unirse', await clic('[data-ventana-plaza="ejercito"]')); await espera(1500);
    await fotos('proto_unirse_desde_plaza');
    console.log('unirme', await clic('[data-unirse-desde-plaza]')); await espera(4000);
  }

  // 2. En el mapa, la ventana Ejército (⚑ del riel) dentro del ejército del otro; separarse, formar y cancelar.
  await entrar(yo, vista(x, y, null, null));
  console.log('ejercito', await clic('[data-panel="ejercito"]')); await espera(1500);
  await fotos('proto_ventana_ejercito');
  console.log('separarme', await clic('[data-ej="separarme"]')); await espera(2500);
  console.log('organizar', await clic('[data-ej="organizar"][data-politica="aceptar"]')); await espera(2500);
  await fotos('proto_formacion');
  console.log('cancelar formacion', await clic('[data-ej="cancelar-formacion"]')); await espera(2500);

  // 3. La ficha del ejército del otro, con «Unirme».
  await entrar(yo, vista(x, y, null, { tipo: 'ejercito', id: ejercito }));
  await fotos('proto_ficha_unirme');

} finally { try { ws?.close(); } catch {} chrome.kill(); }
