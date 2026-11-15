// Paso 8: la Facción del prototipo (BronzeAgeClient en Vite contra mi servidor 7891) con Chrome sin pantalla, por CDP.
// Uso: node faccion.mjs <url> <servidor> <partida> <nickConFaccion> <nickSinFaccion> <carpeta>
import { spawn } from 'node:child_process';
import { writeFileSync, mkdirSync } from 'node:fs';
import { join } from 'node:path';
const [url, servidor, partida, conF, sinF, carpeta] = process.argv.slice(2);
mkdirSync(carpeta, { recursive: true });
const chrome = spawn('C:/Program Files/Google/Chrome/Application/chrome.exe', ['--headless=new', '--remote-debugging-port=9336', '--hide-scrollbars', `--user-data-dir=${join(carpeta, 'perfil')}`, 'about:blank'], { stdio: 'ignore' });
const espera = (ms) => new Promise((r) => setTimeout(r, ms));
let ws, id = 0; const pend = new Map();
const cdp = (method, params = {}) => new Promise((res, rej) => { const i = ++id; pend.set(i, { res, rej }); ws.send(JSON.stringify({ id: i, method, params })); });
const evalua = async (js) => (await cdp('Runtime.evaluate', { expression: js, returnByValue: true })).result.value;
const foto = async (n) => { const { data } = await cdp('Page.captureScreenshot', { format: 'png' }); writeFileSync(join(carpeta, n + '.png'), Buffer.from(data, 'base64')); };
const tam = (w, h) => cdp('Emulation.setDeviceMetricsOverride', { width: w, height: h, deviceScaleFactor: 1, mobile: false });
const clic = (sel) => evalua(`(()=>{const e=document.querySelector(${JSON.stringify(sel)});if(!e)return "sin "+${JSON.stringify(sel)};e.click();return "ok"})()`);
const T = [[1280, 720], [1600, 900], [1920, 1080], [1950, 1130]];
async function entrar(nick) {
  const r = await (await fetch(`${servidor}/v1/sesiones`, { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ nick, clave: 'clave-de-prueba-1' }) })).json();
  const almacen = { bac_jugador_sesion_id: r.token, bac_jugador_usuario: r.nick, bac_jugador_game_id: partida };
  await tam(1600, 900);
  await cdp('Page.navigate', { url }); await espera(2000);
  await evalua(`(()=>{localStorage.clear();const d=${JSON.stringify(almacen)};for(const k in d)localStorage.setItem(k,d[k]);})()`);
  await cdp('Page.reload'); await espera(15000);
  console.log(nick, (await evalua(`document.body.innerText.slice(0,160)`)).replace(/\n/g, ' | '));
}
try {
  let lista; for (let k = 0; k < 40 && !lista; k++) { try { lista = await (await fetch('http://127.0.0.1:9336/json')).json(); } catch { await espera(250); } }
  ws = new WebSocket(lista.find((x) => x.type === 'page').webSocketDebuggerUrl);
  await new Promise((r) => ws.addEventListener('open', r));
  ws.addEventListener('message', (m) => { const d = JSON.parse(m.data); if (d.id && pend.has(d.id)) { const p = pend.get(d.id); pend.delete(d.id); d.error ? p.rej(new Error(d.error.message)) : p.res(d.result); } });
  await cdp('Page.enable'); await cdp('Runtime.enable');

  // Con Facción y plaza: el mapa (el sigilo de la plaza), el lateral y las pestañas.
  await entrar(conF);
  await foto('p8_mapa_1600x900');
  console.log('faccion', await clic('[data-panel-jugador=faccion]')); await espera(2500);
  for (const [w, h] of T) { await tam(w, h); await espera(1500); await foto(`p8_faccion_miembros_${w}x${h}`); }
  await tam(1600, 900); await espera(1000);
  for (const p of ['plazas', 'tecnologia', 'diplomacia', 'ajustes']) {
    console.log(p, await clic(`[data-pestana-faccion=${p}]`)); await espera(2500);
    if (p === 'tecnologia') { for (const [w, h] of T) { await tam(w, h); await espera(1500); await foto(`p8_tecnologia_${w}x${h}`); } await tam(1600, 900); await espera(1000); }
    else await foto(`p8_${p}_1600x900`);
  }
  // Una carta de tecnología con su ficha fija.
  console.log('carta', await evalua(`(()=>{const c=document.querySelector('[data-pestana-faccion=tecnologia]');c&&c.click();return "ok"})()`)); await espera(1500);
  console.log('ficha', await evalua(`(()=>{const c=document.querySelector('.tec-arbol .ta-carta, .tec-arbol [data-tec]');if(!c)return "sin carta";c.click();return c.textContent.slice(0,40)})()`)); await espera(1200);
  await foto('p8_tecnologia_ficha_1600x900');

  // Sin Facción: «Elige tu facción», crear (el selector de sigilo) y pedir ingreso.
  await entrar(sinF);
  console.log('faccion', await clic('[data-panel-jugador=faccion]')); await espera(2500);
  await foto('p8_sin_faccion_1600x900');
  console.log('crear', await clic('#btn-crear-faccion')); await espera(1500);
  for (const [w, h] of T) { await tam(w, h); await espera(1500); await foto(`p8_crear_faccion_${w}x${h}`); }
  await tam(1600, 900); await espera(800);
  console.log('volver', await evalua(`(()=>{const b=[...document.querySelectorAll('button')].find(x=>x.textContent.trim()==='←');if(!b)return "sin volver";b.click();return "ok"})()`)); await espera(1200);
  console.log('unirse', await clic('#btn-unirse-faccion')); await espera(2000);
  await foto('p8_pedir_ingreso_1600x900');
} finally { try { ws?.close(); } catch {} chrome.kill(); }
