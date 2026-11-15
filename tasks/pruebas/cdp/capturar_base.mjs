// Capturas del prototipo (BronzeAgeClient en Vite contra mi servidor de prueba 7891) con Chrome sin pantalla, por CDP.
// Uso: node capturar_base.mjs <url> <json de localStorage> <carpeta> [pestañas separadas por comas]
import { spawn } from 'node:child_process';
import { writeFileSync, mkdirSync } from 'node:fs';
import { join } from 'node:path';

const [url, almacen, carpeta, pestanasArg] = process.argv.slice(2);
const pestanas = (pestanasArg ?? 'Tropas').split(',');
const tamanos = [[1280, 720], [1600, 900], [1920, 1080], [1950, 1130]];
mkdirSync(carpeta, { recursive: true });
const chrome = spawn('C:/Program Files/Google/Chrome/Application/chrome.exe', ['--headless=new', '--remote-debugging-port=9333', '--hide-scrollbars',
  `--user-data-dir=${join(carpeta, 'perfil')}`, 'about:blank'], { stdio: 'ignore' });
const espera = (ms) => new Promise((r) => setTimeout(r, ms));
let ws, id = 0;
const pendientes = new Map();
const cdp = (method, params = {}) => new Promise((res, rej) => { const i = ++id; pendientes.set(i, { res, rej }); ws.send(JSON.stringify({ id: i, method, params })); });
try {
  let lista;
  for (let k = 0; k < 40 && !lista; k++) { try { lista = await (await fetch('http://127.0.0.1:9333/json')).json(); } catch { await espera(250); } }
  const pagina = lista.find((x) => x.type === 'page');
  ws = new WebSocket(pagina.webSocketDebuggerUrl);
  await new Promise((r) => ws.addEventListener('open', r));
  ws.addEventListener('message', (m) => { const d = JSON.parse(m.data); if (d.id && pendientes.has(d.id)) { const p = pendientes.get(d.id); pendientes.delete(d.id); d.error ? p.rej(new Error(d.error.message)) : p.res(d.result); } });
  await cdp('Page.enable'); await cdp('Runtime.enable');
  await cdp('Emulation.setDeviceMetricsOverride', { width: 1600, height: 900, deviceScaleFactor: 1, mobile: false });
  await cdp('Page.navigate', { url });
  await espera(2500);
  await cdp('Runtime.evaluate', { expression: `(()=>{const d=${almacen};for(const k in d)localStorage.setItem(k,d[k]);})()` });
  await cdp('Page.reload');
  await espera(12000);
  const evalua = async (js) => (await cdp('Runtime.evaluate', { expression: js, returnByValue: true })).result.value;
  // La Base: el botón de la barra.
  console.log('base:', await evalua(`(()=>{const b=[...document.querySelectorAll('button')].find(x=>x.textContent.trim()==='Base'||x.getAttribute('aria-label')==='Base'||x.title==='Base');if(!b)return 'sin botón';b.click();return 'ok'})()`));
  await espera(2000);
  for (const p of pestanas) {
    console.log(p, await evalua(`(()=>{const b=[...document.querySelectorAll('[data-base-pestana]')].find(x=>x.textContent.trim().startsWith('${p}'));if(!b)return 'sin pestaña';b.click();return 'ok'})()`));
    await espera(1500);
    for (const [w, h] of tamanos) {
      await cdp('Emulation.setDeviceMetricsOverride', { width: w, height: h, deviceScaleFactor: 1, mobile: false });
      await espera(1200);
      const { data } = await cdp('Page.captureScreenshot', { format: 'png' });
      writeFileSync(join(carpeta, `base_${p.toLowerCase()}_${w}x${h}.png`), Buffer.from(data, 'base64'));
    }
  }
} finally {
  try { ws?.close(); } catch {}
  chrome.kill();
}
