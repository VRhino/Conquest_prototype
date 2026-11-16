// Paso 11 (tanda 1): salir como ejército en el prototipo (BronzeAgeClient en Vite contra mi servidor 7891), con Chrome sin pantalla,
// por CDP, a los cuatro tamaños. Uso: node ejercito.mjs <url> <servidor> <nickDelGobernadorResidente> <carpeta>
import { spawn } from 'node:child_process';
import { writeFileSync, mkdirSync } from 'node:fs';
import { join } from 'node:path';
const [url, servidor, nick, carpeta] = process.argv.slice(2);
mkdirSync(carpeta, { recursive: true });
const chrome = spawn('C:/Program Files/Google/Chrome/Application/chrome.exe', ['--headless=new', '--remote-debugging-port=9343', '--hide-scrollbars', `--user-data-dir=${join(carpeta, 'perfil-ejercito')}`, 'about:blank'], { stdio: 'ignore' });
const espera = (ms) => new Promise((r) => setTimeout(r, ms));
let ws, id = 0; const pend = new Map();
const cdp = (method, params = {}) => new Promise((res, rej) => { const i = ++id; pend.set(i, { res, rej }); ws.send(JSON.stringify({ id: i, method, params })); });
const evalua = async (js) => (await cdp('Runtime.evaluate', { expression: js, returnByValue: true })).result.value;
const foto = async (n) => { const { data } = await cdp('Page.captureScreenshot', { format: 'png' }); writeFileSync(join(carpeta, n + '.png'), Buffer.from(data, 'base64')); };
const tam = (w, h) => cdp('Emulation.setDeviceMetricsOverride', { width: w, height: h, deviceScaleFactor: 1, mobile: false });
const clic = (sel) => evalua(`(()=>{const e=document.querySelector(${JSON.stringify(sel)});if(!e)return "sin "+${JSON.stringify(sel)};e.click();return "ok"})()`);
try {
  let lista; for (let k = 0; k < 40 && !lista; k++) { try { lista = await (await fetch('http://127.0.0.1:9343/json')).json(); } catch { await espera(250); } }
  ws = new WebSocket(lista.find((x) => x.type === 'page').webSocketDebuggerUrl);
  await new Promise((r) => ws.addEventListener('open', r));
  ws.addEventListener('message', (m) => { const d = JSON.parse(m.data); if (d.id && pend.has(d.id)) { const p = pend.get(d.id); pend.delete(d.id); d.error ? p.rej(new Error(d.error.message)) : p.res(d.result); } });
  await cdp('Page.enable'); await cdp('Runtime.enable');
  const r = await (await fetch(`${servidor}/v1/sesiones`, { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ nick, clave: 'clave-de-prueba-1' }) })).json();
  await tam(1600, 900);
  await cdp('Page.navigate', { url }); await espera(2000);
  await evalua(`(()=>{localStorage.clear();const d=${JSON.stringify({ bac_jugador_sesion_id: r.token, bac_jugador_usuario: r.nick, bac_jugador_game_id: 'mundo' })};for(const k in d)localStorage.setItem(k,d[k]);})()`);
  await cdp('Page.reload'); await espera(15000);
  // El héroe de la prueba está dentro de su plaza: «Salir al mundo», «Ejército» y convocar; luego la preparación.
  const boton = (t) => evalua(`(()=>{const b=[...document.querySelectorAll('button')].find(b=>(b.textContent||'').trim().toLowerCase()===${JSON.stringify(t)}.toLowerCase());if(!b)return 'sin '+${JSON.stringify(t)};b.click();return 'ok'})()`);
  await tam(1600, 900); await espera(1500);
  console.log('botones', await evalua(`[...document.querySelectorAll('button,[data-salir],[data-accion]')].map(b=>(b.textContent||b.title||'').trim().slice(0,25)).filter(Boolean).join(' | ')`));
  console.log('salir', await evalua(`(()=>{const b=[...document.querySelectorAll('button')].find(b=>/salir al mundo/i.test(b.textContent||'')||/salir al mundo/i.test(b.title||''));if(!b)return 'sin';b.click();return 'ok'})()`)); await espera(2500);
  console.log('ejercito', await evalua(`(()=>{const i=document.querySelector('input[name=salida-modo][value=ejercito]');if(!i)return 'sin radio';i.click();i.dispatchEvent(new Event('change',{bubbles:true}));return 'ok'})()`)); await espera(1500);
  for (const [w, h] of [[1280, 720], [1600, 900], [1920, 1080], [1950, 1130]]) { await tam(w, h); await espera(1500); await foto(`proto_salir_ejercito_${w}x${h}`); }
  console.log('convocar', await boton('Convocar ejército')); await espera(3000);
  for (const [w, h] of [[1280, 720], [1600, 900], [1920, 1080], [1950, 1130]]) { await tam(w, h); await espera(1500); await foto(`proto_preparacion_${w}x${h}`); }
  console.log('cancelar', await boton('Cancelar la salida')); await espera(1500);
} finally { try { ws?.close(); } catch {} chrome.kill(); }
