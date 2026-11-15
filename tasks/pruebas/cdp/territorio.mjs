// Paso 9 (análisis): la ventana Territorio de la plaza y el mapa 2D del prototipo (BronzeAgeClient en Vite contra mi servidor 7891), con Chrome
// sin pantalla, por CDP. Uso: node territorio.mjs <url> <servidor> <nickDelGobernadorResidente> <carpeta>
import { spawn } from 'node:child_process';
import { writeFileSync, mkdirSync } from 'node:fs';
import { join } from 'node:path';
const [url, servidor, nick, carpeta] = process.argv.slice(2);
mkdirSync(carpeta, { recursive: true });
const chrome = spawn('C:/Program Files/Google/Chrome/Application/chrome.exe', ['--headless=new', '--remote-debugging-port=9340', '--hide-scrollbars', `--user-data-dir=${join(carpeta, 'perfil-territorio')}`, 'about:blank'], { stdio: 'ignore' });
const espera = (ms) => new Promise((r) => setTimeout(r, ms));
let ws, id = 0; const pend = new Map();
const cdp = (method, params = {}) => new Promise((res, rej) => { const i = ++id; pend.set(i, { res, rej }); ws.send(JSON.stringify({ id: i, method, params })); });
const evalua = async (js) => (await cdp('Runtime.evaluate', { expression: js, returnByValue: true })).result.value;
const foto = async (n) => { const { data } = await cdp('Page.captureScreenshot', { format: 'png' }); writeFileSync(join(carpeta, n + '.png'), Buffer.from(data, 'base64')); };
const tam = (w, h) => cdp('Emulation.setDeviceMetricsOverride', { width: w, height: h, deviceScaleFactor: 1, mobile: false });
const clic = (sel) => evalua(`(()=>{const e=document.querySelector(${JSON.stringify(sel)});if(!e)return "sin "+${JSON.stringify(sel)};e.click();return "ok"})()`);
try {
  let lista; for (let k = 0; k < 40 && !lista; k++) { try { lista = await (await fetch('http://127.0.0.1:9340/json')).json(); } catch { await espera(250); } }
  ws = new WebSocket(lista.find((x) => x.type === 'page').webSocketDebuggerUrl);
  await new Promise((r) => ws.addEventListener('open', r));
  ws.addEventListener('message', (m) => { const d = JSON.parse(m.data); if (d.id && pend.has(d.id)) { const p = pend.get(d.id); pend.delete(d.id); d.error ? p.rej(new Error(d.error.message)) : p.res(d.result); } });
  await cdp('Page.enable'); await cdp('Runtime.enable');
  const r = await (await fetch(`${servidor}/v1/sesiones`, { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ nick, clave: 'clave-de-prueba-1' }) })).json();
  await tam(1600, 900);
  await cdp('Page.navigate', { url }); await espera(2000);
  await evalua(`(()=>{localStorage.clear();const d=${JSON.stringify({ bac_jugador_sesion_id: r.token, bac_jugador_usuario: r.nick, bac_jugador_game_id: 'mundo' })};for(const k in d)localStorage.setItem(k,d[k]);})()`);
  await cdp('Page.reload'); await espera(15000);
  console.log('territorio', await clic('[data-ventana-plaza=territorio]')); await espera(2500);
  await foto('proto_territorio_1600x900');
  console.log('marcar', await clic('[data-pueblo-abrir=marcar]')); await espera(1500);
  await foto('proto_territorio_marcar_1600x900');
  console.log('elegir aldea', await clic('[data-pueblo-accion=elegir-aldea]')); await espera(3000);
  await foto('proto_mapa_elegir_aldea_1600x900');
  await evalua(`document.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape'}))`); await espera(1500);
  console.log('territorio otra vez', await clic('[data-ventana-plaza=territorio]')); await espera(2000);
  console.log('elegir atalaya', await clic('[data-pueblo-accion=elegir-avanzada]')); await espera(3000);
  await foto('proto_mapa_elegir_atalaya_1600x900');
  await evalua(`document.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape'}))`); await espera(1500);
  await evalua(`document.querySelectorAll('.ventana-cerrar,[data-cerrar-ventana]').forEach(b=>b.click())`); await espera(1000);
  console.log('mapa', await clic('[data-ver-mapa]')); await espera(3000);
  await foto('proto_mapa_2d_1600x900');
  await evalua(`(()=>{const c=document.querySelector('#mapa-interior');if(!c)return;for(let i=0;i<8;i++)c.dispatchEvent(new WheelEvent('wheel',{deltaY:300,clientX:c.width/2,clientY:c.height/2,bubbles:true}));})()`); await espera(2000);
  await foto('proto_mapa_2d_lejos_1600x900');
} finally { try { ws?.close(); } catch {} chrome.kill(); }
