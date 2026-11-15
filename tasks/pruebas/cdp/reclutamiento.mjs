// La ficha corta del árbol de reclutamiento de la plaza del prototipo (BronzeAgeClient en Vite contra mi servidor 7891), con Chrome sin pantalla, por CDP.
// Uso: node reclutamiento.mjs <url> <servidor> <nick> <carpeta> [tropa]
import { spawn } from 'node:child_process';
import { writeFileSync, mkdirSync } from 'node:fs';
import { join } from 'node:path';
const [url, servidor, nick, carpeta, tropa = 'lanceros_mimbre'] = process.argv.slice(2);
mkdirSync(carpeta, { recursive: true });
const chrome = spawn('C:/Program Files/Google/Chrome/Application/chrome.exe', ['--headless=new', '--remote-debugging-port=9338', '--hide-scrollbars', `--user-data-dir=${join(carpeta, 'perfil-recl')}`, 'about:blank'], { stdio: 'ignore' });
const espera = (ms) => new Promise((r) => setTimeout(r, ms));
let ws, id = 0; const pend = new Map();
const cdp = (method, params = {}) => new Promise((res, rej) => { const i = ++id; pend.set(i, { res, rej }); ws.send(JSON.stringify({ id: i, method, params })); });
const evalua = async (js) => (await cdp('Runtime.evaluate', { expression: js, returnByValue: true })).result.value;
const foto = async (n) => { const { data } = await cdp('Page.captureScreenshot', { format: 'png' }); writeFileSync(join(carpeta, n + '.png'), Buffer.from(data, 'base64')); };
try {
  let lista; for (let k = 0; k < 40 && !lista; k++) { try { lista = await (await fetch('http://127.0.0.1:9338/json')).json(); } catch { await espera(250); } }
  ws = new WebSocket(lista.find((x) => x.type === 'page').webSocketDebuggerUrl);
  await new Promise((r) => ws.addEventListener('open', r));
  ws.addEventListener('message', (m) => { const d = JSON.parse(m.data); if (d.id && pend.has(d.id)) { const p = pend.get(d.id); pend.delete(d.id); d.error ? p.rej(new Error(d.error.message)) : p.res(d.result); } });
  await cdp('Page.enable'); await cdp('Runtime.enable');
  const r = await (await fetch(`${servidor}/v1/sesiones`, { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ nick, clave: 'clave-de-prueba-1' }) })).json();
  await cdp('Emulation.setDeviceMetricsOverride', { width: 1600, height: 900, deviceScaleFactor: 1, mobile: false });
  await cdp('Page.navigate', { url }); await espera(2000);
  await evalua(`(()=>{localStorage.clear();const d=${JSON.stringify({ bac_jugador_sesion_id: r.token, bac_jugador_usuario: r.nick, bac_jugador_game_id: 'mundo' })};for(const k in d)localStorage.setItem(k,d[k]);})()`);
  await cdp('Page.reload'); await espera(15000);
  console.log('reclutamiento', await evalua(`(()=>{const b=document.querySelector('[data-ventana-plaza=reclutamiento]');if(!b)return 'sin botón: '+[...document.querySelectorAll('[data-ventana-plaza]')].map(x=>x.dataset.ventanaPlaza).join(',');b.click();return 'ok'})()`)); await espera(2500);
  console.log('tip', await evalua(`(()=>{const c=document.querySelector('.ta-carta[data-nodo=${tropa}]');if(!c)return 'sin carta';c.scrollIntoView({block:'center',inline:'center'});const b=c.getBoundingClientRect();c.dispatchEvent(new MouseEvent('mouseenter',{clientX:b.right-10,clientY:b.top+10}));return 'ok'})()`)); await espera(1000);
  await foto('proto_reclutamiento_tip_1600x900');
} finally { try { ws?.close(); } catch {} chrome.kill(); }
