// Paso 10 (tanda 1): la ventana Mercado de la plaza del prototipo (BronzeAgeClient en Vite contra mi servidor 7891), con Chrome sin pantalla,
// por CDP, a los cuatro tamaños. Uso: node mercado.mjs <url> <servidor> <nickDelGobernadorResidente> <carpeta>
import { spawn } from 'node:child_process';
import { writeFileSync, mkdirSync } from 'node:fs';
import { join } from 'node:path';
const [url, servidor, nick, carpeta] = process.argv.slice(2);
mkdirSync(carpeta, { recursive: true });
const chrome = spawn('C:/Program Files/Google/Chrome/Application/chrome.exe', ['--headless=new', '--remote-debugging-port=9341', '--hide-scrollbars', `--user-data-dir=${join(carpeta, 'perfil-mercado')}`, 'about:blank'], { stdio: 'ignore' });
const espera = (ms) => new Promise((r) => setTimeout(r, ms));
let ws, id = 0; const pend = new Map();
const cdp = (method, params = {}) => new Promise((res, rej) => { const i = ++id; pend.set(i, { res, rej }); ws.send(JSON.stringify({ id: i, method, params })); });
const evalua = async (js) => (await cdp('Runtime.evaluate', { expression: js, returnByValue: true })).result.value;
const foto = async (n) => { const { data } = await cdp('Page.captureScreenshot', { format: 'png' }); writeFileSync(join(carpeta, n + '.png'), Buffer.from(data, 'base64')); };
const tam = (w, h) => cdp('Emulation.setDeviceMetricsOverride', { width: w, height: h, deviceScaleFactor: 1, mobile: false });
const clic = (sel) => evalua(`(()=>{const e=document.querySelector(${JSON.stringify(sel)});if(!e)return "sin "+${JSON.stringify(sel)};e.click();return "ok"})()`);
try {
  let lista; for (let k = 0; k < 40 && !lista; k++) { try { lista = await (await fetch('http://127.0.0.1:9341/json')).json(); } catch { await espera(250); } }
  ws = new WebSocket(lista.find((x) => x.type === 'page').webSocketDebuggerUrl);
  await new Promise((r) => ws.addEventListener('open', r));
  ws.addEventListener('message', (m) => { const d = JSON.parse(m.data); if (d.id && pend.has(d.id)) { const p = pend.get(d.id); pend.delete(d.id); d.error ? p.rej(new Error(d.error.message)) : p.res(d.result); } });
  await cdp('Page.enable'); await cdp('Runtime.enable');
  const r = await (await fetch(`${servidor}/v1/sesiones`, { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ nick, clave: 'clave-de-prueba-1' }) })).json();
  await tam(1600, 900);
  await cdp('Page.navigate', { url }); await espera(2000);
  await evalua(`(()=>{localStorage.clear();const d=${JSON.stringify({ bac_jugador_sesion_id: r.token, bac_jugador_usuario: r.nick, bac_jugador_game_id: 'mundo' })};for(const k in d)localStorage.setItem(k,d[k]);})()`);
  await cdp('Page.reload'); await espera(15000);
  // El héroe de la prueba acaba fuera, a la puerta de su plaza: entra (la acción «Entrar» de la ficha de la plaza) y abre el Mercado del riel.
  // Un clic en la plaza (en el centro de la vista, donde la cámara sigue a la columna, que está a su puerta) abre su ficha.
  await tam(1280, 720); await espera(2000);
  for (const type of ['mousePressed', 'mouseReleased']) await cdp('Input.dispatchMouseEvent', { type, x: 640, y: 360, button: 'left', clickCount: 1 });
  await espera(2000);
  const accion = (t) => evalua(`(()=>{const b=[...document.querySelectorAll('button')].find(b=>(b.textContent||'').trim().endsWith(${JSON.stringify(t)}));if(!b)return 'sin '+${JSON.stringify(t)};b.click();return 'ok'})()`);
  // Si debajo hay varias cosas (la plaza y sus aedas), sale la lista: la plaza es la de la corona.
  console.log('la plaza', await evalua(`(()=>{const b=[...document.querySelectorAll('button')].find(b=>(b.textContent||'').trim().startsWith('♛'));if(!b)return 'sin lista';b.click();return 'ok'})()`)); await espera(2000);
  console.log('mercado de la puerta', await accion('Mercado')); await espera(2000);
  for (const [w, h] of [[1280, 720], [1600, 900], [1920, 1080], [1950, 1130]]) { await tam(w, h); await espera(1500); await foto(`proto_mercado_puerta_${w}x${h}`); }
  // La pestaña Caravanas (tanda 2).
  console.log('caravanas', await evalua(`(()=>{const b=[...document.querySelectorAll('button')].find(b=>(b.textContent||'').trim()==='Caravanas');if(!b)return 'sin Caravanas';b.click();return 'ok'})()`)); await espera(2000);
  for (const [w, h] of [[1280, 720], [1600, 900], [1920, 1080], [1950, 1130]]) { await tam(w, h); await espera(1500); await foto(`proto_caravanas_${w}x${h}`); }
  await tam(1600, 900);
  console.log('botones', await evalua(`[...document.querySelectorAll('button')].map(b=>(b.title||b.textContent).trim().slice(0,30)).filter(Boolean).join(' | ')`));
  console.log('entrar', await evalua(`(()=>{const b=[...document.querySelectorAll('button')].find(b=>/^Entrar/.test((b.textContent||'').trim())||/Entrar/.test(b.title||''));if(!b)return 'sin entrar';b.click();return 'ok'})()`)); await espera(12000);
  console.log('mercado', await clic('[data-ventana-plaza=mercado]')); await espera(2500);
  for (const [w, h] of [[1280, 720], [1600, 900], [1920, 1080], [1950, 1130]]) { await tam(w, h); await espera(1500); await foto(`proto_mercado_ordenes_${w}x${h}`); }
} finally { try { ws?.close(); } catch {} chrome.kill(); }
