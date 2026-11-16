// Paso 12 (tanda 3): las misiones en el prototipo (BronzeAgeClient en Vite contra mi servidor 7891), con Chrome sin pantalla, por CDP, a los
// cuatro tamaños. La escena la deja la prueba TusMisiones con MUNDO_PROTO=misiones: el Rey y Gobernador dentro de su plaza recién fundada.
// Captura el diario, el tablón de la plaza, Facción › Misiones y Facción › Tablón.
// Uso: node misiones.mjs <url> <servidor> <nick> <carpeta>
import { spawn } from 'node:child_process';
import { writeFileSync, mkdirSync } from 'node:fs';
import { join } from 'node:path';
const [url, servidor, nick, carpeta] = process.argv.slice(2);
mkdirSync(carpeta, { recursive: true });
const chrome = spawn('C:/Program Files/Google/Chrome/Application/chrome.exe', ['--headless=new', '--remote-debugging-port=9348', '--hide-scrollbars', `--user-data-dir=${join(carpeta, 'perfil-misiones')}`, 'about:blank'], { stdio: 'ignore' });
const espera = (ms) => new Promise((r) => setTimeout(r, ms));
let ws, id = 0; const pend = new Map();
const cdp = (method, params = {}) => new Promise((res, rej) => { const i = ++id; pend.set(i, { res, rej }); ws.send(JSON.stringify({ id: i, method, params })); });
const evalua = async (js) => (await cdp('Runtime.evaluate', { expression: js, returnByValue: true })).result.value;
const foto = async (n) => { const { data } = await cdp('Page.captureScreenshot', { format: 'png' }); writeFileSync(join(carpeta, n + '.png'), Buffer.from(data, 'base64')); };
const tam = (w, h) => cdp('Emulation.setDeviceMetricsOverride', { width: w, height: h, deviceScaleFactor: 1, mobile: false });
const clic = (sel) => evalua(`(()=>{const e=document.querySelector(${JSON.stringify(sel)});if(!e)return "sin "+${JSON.stringify(sel)};e.click();return "ok"})()`);
const tamanos = [[1280, 720], [1600, 900], [1920, 1080], [1950, 1130]];
try {
  let lista; for (let k = 0; k < 40 && !lista; k++) { try { lista = await (await fetch('http://127.0.0.1:9348/json')).json(); } catch { await espera(250); } }
  ws = new WebSocket(lista.find((x) => x.type === 'page').webSocketDebuggerUrl);
  await new Promise((r) => ws.addEventListener('open', r));
  ws.addEventListener('message', (m) => { const d = JSON.parse(m.data); if (d.id && pend.has(d.id)) { const p = pend.get(d.id); pend.delete(d.id); d.error ? p.rej(new Error(d.error.message)) : p.res(d.result); } });
  await cdp('Page.enable'); await cdp('Runtime.enable');
  await tam(1600, 900);
  await cdp('Page.navigate', { url }); await espera(2000);
  const r = await (await fetch(`${servidor}/v1/sesiones`, { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ nick, clave: 'clave-de-prueba-1' }) })).json();
  await evalua(`(()=>{localStorage.clear();const d=${JSON.stringify({ bac_jugador_sesion_id: r.token, bac_jugador_usuario: r.nick, bac_jugador_game_id: 'mundo' })};for(const k in d)localStorage.setItem(k,d[k]);})()`);
  console.log('login', r.nick ?? JSON.stringify(r).slice(0, 120)); await cdp('Page.reload'); await espera(20000);
  console.log('misiones', await clic('[data-panel-jugador=misiones]')); await espera(2500);
  for (const [w, h] of tamanos) { await tam(w, h); await espera(1500); await foto(`proto_diario_${w}x${h}`); }
  await tam(1600, 900);
  console.log('tablón', await clic('[data-diario-vista="tablon"]')); await espera(2000);
  for (const [w, h] of tamanos) { await tam(w, h); await espera(1500); await foto(`proto_tablon_plaza_${w}x${h}`); }
  await tam(1600, 900);
  console.log('cerrar', await clic('[data-mision-cerrar]')); await espera(1500);
  console.log('faccion', await clic('[data-panel-jugador=faccion]')); await espera(2500);
  console.log('misiones de Facción', await clic('[data-pestana-faccion=misiones]')); await espera(2500);
  for (const [w, h] of tamanos) { await tam(w, h); await espera(1500); await foto(`proto_faccion_misiones_${w}x${h}`); }
  await tam(1600, 900);
  console.log('tablón de Facción', await clic('[data-pestana-faccion=tablon]')); await espera(2500);
  for (const [w, h] of tamanos) { await tam(w, h); await espera(1500); await foto(`proto_faccion_tablon_${w}x${h}`); }
} finally { try { ws?.close(); } catch {} chrome.kill(); }
