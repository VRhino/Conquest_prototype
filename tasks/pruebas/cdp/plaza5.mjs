// La plaza del prototipo: el HUD a los cuatro tamaños y cada ventana del riel a 1600x900. Uso: node plaza.mjs <url> <almacen> <carpeta>
import { spawn } from 'node:child_process';
import { writeFileSync, mkdirSync } from 'node:fs';
import { join } from 'node:path';
const [url, almacen, carpeta] = process.argv.slice(2);
mkdirSync(carpeta, { recursive: true });
const chrome = spawn('C:/Program Files/Google/Chrome/Application/chrome.exe', ['--headless=new', '--remote-debugging-port=9335', '--hide-scrollbars', `--user-data-dir=${join(carpeta, 'perfil')}`, 'about:blank'], { stdio: 'ignore' });
const espera = (ms) => new Promise((r) => setTimeout(r, ms));
let ws, id = 0; const pend = new Map();
const cdp = (method, params = {}) => new Promise((res, rej) => { const i = ++id; pend.set(i, { res, rej }); ws.send(JSON.stringify({ id: i, method, params })); });
const evalua = async (js) => (await cdp('Runtime.evaluate', { expression: js, returnByValue: true })).result.value;
const foto = async (n) => { const { data } = await cdp('Page.captureScreenshot', { format: 'png' }); writeFileSync(join(carpeta, n + '.png'), Buffer.from(data, 'base64')); };
const tam = (w, h) => cdp('Emulation.setDeviceMetricsOverride', { width: w, height: h, deviceScaleFactor: 1, mobile: false });
try {
  let lista; for (let k = 0; k < 40 && !lista; k++) { try { lista = await (await fetch('http://127.0.0.1:9335/json')).json(); } catch { await espera(250); } }
  ws = new WebSocket(lista.find((x) => x.type === 'page').webSocketDebuggerUrl);
  await new Promise((r) => ws.addEventListener('open', r));
  ws.addEventListener('message', (m) => { const d = JSON.parse(m.data); if (d.id && pend.has(d.id)) { const p = pend.get(d.id); pend.delete(d.id); d.error ? p.rej(new Error(d.error.message)) : p.res(d.result); } });
  await cdp('Page.enable'); await cdp('Runtime.enable'); await tam(1600, 900);
  await cdp('Page.navigate', { url }); await espera(2500);
  await cdp('Runtime.evaluate', { expression: `(()=>{const d=${almacen};for(const k in d)localStorage.setItem(k,d[k]);})()` });
  await cdp('Page.reload'); await espera(14000);
  const clic = (sel) => evalua(`(()=>{const e=document.querySelector(${JSON.stringify(sel)});if(!e)return "sin "+${JSON.stringify(sel)};e.click();return "ok"})()`);
  const vista = (n) => foto(n);
  const T = [[1280, 720], [1600, 900], [1920, 1080], [1950, 1130]];
  // la Armería: a la cola desde el cajón (5 min de obra)
  console.log("construccion", await clic("[data-ventana-plaza=construccion]")); await espera(1200);
  console.log("talleres", await clic("[data-grupo-construir=talleres]")); await espera(800);
  console.log("armeria", await clic("[data-construir=armeria]")); await espera(1500);
  await clic("[data-riel-volver]"); await espera(800);
  for (const [w, h] of T) {
    await tam(w, h); await espera(1500);
    console.log("defensa", await clic("[data-ventana-plaza=defensa]")); await espera(1500); await vista(`proto_guarnicion_${w}x${h}`);
    console.log("muralla", await clic("[data-sub=muralla]")); await espera(1200); await vista(`proto_muralla_${w}x${h}`);
    console.log("puerta", await clic("[data-sub=puerta]")); await espera(1200); await vista(`proto_puerta_${w}x${h}`);
    await clic("[data-sub=guarnicion]"); await espera(400);
    await clic("[data-ventana-cerrar]"); await espera(800);
    console.log("reclutamiento", await clic("[data-ventana-plaza=reclutamiento]")); await espera(2500); await vista(`proto_reclutamiento_${w}x${h}`);
    await clic("[data-ventana-cerrar]"); await espera(800);
  }
  let hay = false;
  for (let k = 0; k < 60 && !hay; k++) { hay = await evalua(`Boolean(document.querySelector("[data-ventana-plaza=armeria]"))`); if (!hay) await espera(10000); }
  console.log("armería en pie", hay);
  for (const [w, h] of T) {
    if (!hay) break;
    await tam(w, h); await espera(1500);
    console.log("arm", await clic("[data-ventana-plaza=armeria]")); await espera(1800); await vista(`proto_armeria_${w}x${h}`);
    console.log("forja", await clic("[data-sub=forja]")); await espera(1500); await vista(`proto_forja_${w}x${h}`);
    console.log("abrir", await clic("[data-sub=abrir]")); await espera(1500); await vista(`proto_abrir_forja_${w}x${h}`);
    await clic("[data-sub=tienda]"); await espera(400);
    await clic("[data-ventana-cerrar]"); await espera(800);
  }
  console.log(await evalua(`document.body.innerText.slice(0,600)`));
} finally { try { ws?.close(); } catch {} chrome.kill(); }
