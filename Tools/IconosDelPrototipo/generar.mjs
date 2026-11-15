// Los iconos provisionales de BronzeAgeClient en Conquest, por script (no a mano). Desde la carpeta de Conquest:
//   node Tools/IconosDelPrototipo/generar.mjs            (necesita ../BronzeAgeClient con su node_modules, por esbuild)
//   node Tools/IconosDelPrototipo/generar.mjs --emoji    (además baja los emoji de recursos de Twemoji, CC BY 4.0)
// Escribe SVG en Assets/Resources/MundoUI/Iconos/{edificio,tropa,mapa,recurso}/<id>.svg y `iconos.json` con la lista y los colores.
// Los dibujos a mano del prototipo usan `currentColor` (trazo) y `--relleno` (superficies) para teñirse según el estado: aquí van en blanco
// (el relleno con su opacidad) y la interfaz los tiñe con `-unity-background-image-tint-color`. Unity los importa como VectorImage.
import { execFileSync } from 'node:child_process';
import { mkdirSync, readFileSync, writeFileSync, existsSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const aqui = dirname(fileURLToPath(import.meta.url));
const conquest = resolve(aqui, '../..');
const prototipo = resolve(conquest, '../BronzeAgeClient');
const destino = join(conquest, 'Assets/Resources/MundoUI/Iconos');

// Las claves de la tabla de dibujos de los edificios, leídas del fuente (el módulo solo exporta la función).
const fuenteEdificios = readFileSync(join(prototipo, 'src/ui/iconosEdificio.ts'), 'utf8');
const tabla = fuenteEdificios.slice(fuenteEdificios.indexOf('const DIBUJOS'));
const edificios = [...tabla.matchAll(/^\s{2}([a-zA-Z]+):/gm)].map((m) => m[1]);
// Y las de las partes del interior que no son un edificio (`hudInterior.ts`, tabla `DIBUJO`).
const fuentePartes = readFileSync(join(prototipo, 'src/ui/hudInterior.ts'), 'utf8');
const inicioPartes = fuentePartes.indexOf('const DIBUJO = {');
const tablaPartes = fuentePartes.slice(inicioPartes, fuentePartes.indexOf('};', inicioPartes));
const partes = [...tablaPartes.matchAll(/^\s{2}([a-zA-Z]+):/gm)].map((m) => m[1]);

const esbuild = join(prototipo, 'node_modules/esbuild/bin/esbuild');
const paquete = execFileSync(process.execPath, [esbuild, join(aqui, 'entrada.ts'), '--bundle', '--platform=node', '--format=esm', '--log-level=error'], { encoding: 'utf8', maxBuffer: 64 << 20 });
const temporal = join(aqui, '.paquete.mjs');
writeFileSync(temporal, paquete);
const datos = JSON.parse(execFileSync(process.execPath, [temporal], { encoding: 'utf8', maxBuffer: 64 << 20, env: { ...process.env, EDIFICIOS: JSON.stringify(edificios), PARTES: JSON.stringify(partes) } }));

/** Un dibujo a mano del prototipo, teñible: trazo y relleno en blanco (el relleno con la opacidad que tenía). */
function tenible(svg) {
  return svg
    .replace(/fill="var\(--relleno[^"]*\)"/g, 'fill="#ffffff" fill-opacity="0.25"')
    .replace(/currentColor/g, '#ffffff')
    .replace(/ class="[^"]*"/g, '')
    .replace(/ aria-hidden="true"/g, '')
    .replace('<svg ', '<svg xmlns="http://www.w3.org/2000/svg" ');
}

function escribir(carpeta, id, svg) {
  mkdirSync(join(destino, carpeta), { recursive: true });
  writeFileSync(join(destino, carpeta, `${id}.svg`), svg);
}

for (const [id, svg] of Object.entries(datos.edificio)) escribir('edificio', id, tenible(svg));
for (const [id, svg] of Object.entries(datos.tropa)) escribir('tropa', id, tenible(svg));
for (const [id, svg] of Object.entries(datos.parte)) escribir('parte', id, tenible(svg));
// Los del mapa (game-icons.net, CC BY 3.0): un trazado en 512×512, en blanco para teñirlo con su color.
for (const [id, i] of Object.entries(datos.mapa))
  escribir('mapa', id, `<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="0 0 512 512"><path d="${i.d}" fill="#ffffff"/></svg>`);

// Los emoji de los recursos: el mismo punto de código, dibujado por Twemoji (CC BY 4.0).
const recursos = {};
const conEmoji = process.argv.includes('--emoji');
for (const [id, emoji] of Object.entries(datos.recurso)) {
  const codigo = [...emoji].map((c) => c.codePointAt(0).toString(16)).filter((c) => c !== 'fe0f').join('-');
  recursos[id] = { emoji, codigo, color: datos.colorRecurso[id] ?? null };
  if (!conEmoji) continue;
  const fichero = join(destino, 'recurso', `${id}.svg`);
  if (existsSync(fichero)) continue;
  const r = await fetch(`https://cdn.jsdelivr.net/gh/jdecked/twemoji@15.1.0/assets/svg/${codigo}.svg`);
  if (!r.ok) { console.error(`sin emoji de Twemoji para ${id} (${emoji}, ${codigo}): ${r.status}`); continue; }
  escribir('recurso', id, await r.text());
}
// Los glifos sueltos que el prototipo pone en las pantallas ya migradas (no son recursos), con el mismo punto de código y de Twemoji.
// Los que no son emoji (➜ ⧗ ⚑ ⛟ ⌂ ♜ ✦ ♛ •) no están en Twemoji y no se traen. Dónde va cada uno: Docs/Arquitectura/27.
const GLIFOS = {
  atacar: '⚔', entrar: '🚪', inspeccionar: '👁', abrir: '🎁', viveres: '🌾', // los botones de la ficha (main.ts, `AccionPuerta.icono`)
  mercenarios: '⛺', bandidos: '☠', aeda: '📜', obra: '⚒', // la lista bajo el clic (ui/eleccionClic.ts) y las etiquetas del mapa
  gente: '👥', bosque: '🌲', rio: '〰', costa: '🌊', // «Nace tu héroe» (ui/eleccionCampamento.ts); la fertilidad lleva 🌾
  reclutas: '🧑', // el chip de reclutas de la tropa del campamento (ui/pantallaCampamento.ts)
  aviso: '⚠', // los errores de acceso y partidas (`.error-banner`)
  monedas: '💰', escudo: '🛡', sitio: '📍', campamento: '🏕', casa: '🏠', // la Base personal (ui/basePersonal.ts)
  nobleza: '👑', mantenimiento: '🛠', espera: '⏳', // el HUD de la plaza (ui/hudPlaza.ts, ui/panelCentro.ts)
  prestigio: '🏆', reputacion: '⚖', alianza: '🤝', templo: '🏛', estatua: '🗿', // el lateral de tu Facción (ui/pestanaFaccion.ts)
  fusion: '🔗', bots: '🤖', // la Diplomacia: la fusión de «Nueva propuesta» y «Ocultar las de bots» (ui/panelDiplomacia.ts, ui/pestanaFaccion.ts)
};
// Los símbolos del prototipo que no son emoji: no están en Twemoji ni en Inter o Cinzel (saldrían como cuadros), así que se dibujan aquí,
// en 32×32 y en blanco para teñirlos como los dibujos a mano (el prototipo los pinta en el color del texto, \`--oro\` en la lista).
const SIMBOLOS = {
  flecha: ['➜', '<path d="M5 16h18" stroke="#ffffff" stroke-width="3.2" stroke-linecap="round"/><path d="M17 8.5 25.5 16 17 23.5" fill="none" stroke="#ffffff" stroke-width="3.2" stroke-linecap="round" stroke-linejoin="round"/>'],
  reloj: ['⧗', '<path d="M8 4.5h16M8 27.5h16" stroke="#ffffff" stroke-width="2.4" stroke-linecap="round"/><path d="M10 5.5h12L16 16l6 10.5H10L16 16Z" fill="#ffffff"/>'],
  bandera: ['⚑', '<path d="M9 4v25" stroke="#ffffff" stroke-width="2.6" stroke-linecap="round"/><path d="M10 5h15l-4.5 5.5L25 16H10Z" fill="#ffffff"/>'],
  carro: ['⛟', '<path d="M3 8h17v13H3Z" fill="#ffffff"/><path d="M20 12h5.5l4 5v4H20Z" fill="#ffffff"/><circle cx="8.5" cy="23.5" r="3.2" fill="#ffffff"/><circle cx="23.5" cy="23.5" r="3.2" fill="#ffffff"/>'],
  casa: ['⌂', '<path d="M5.5 27V14.5L16 5.5l10.5 9V27Z" fill="none" stroke="#ffffff" stroke-width="2.4" stroke-linejoin="round"/>'],
  torre: ['♜', '<path d="M9 5h3.2v3h2.2V5h3.2v3h2.2V5H23v7.5H9Z" fill="#ffffff"/><path d="M11.5 13h9l1 10.5h-11Z" fill="#ffffff"/><path d="M7.5 24.5h17V28h-17Z" fill="#ffffff"/>'],
  estrella: ['✦', '<path d="M16 3c1.2 7.6 5.4 11.8 13 13-7.6 1.2-11.8 5.4-13 13-1.2-7.6-5.4-11.8-13-13 7.6-1.2 11.8-5.4 13-13Z" fill="#ffffff"/>'],
  formacion: ['⛨', '<path d="M16 3.5 26.5 7v8.5c0 6.2-4.4 10.6-10.5 13-6.1-2.4-10.5-6.8-10.5-13V7Z" fill="none" stroke="#ffffff" stroke-width="2.4" stroke-linejoin="round"/><path d="M16 9.5v13M10.5 14h11" stroke="#ffffff" stroke-width="2.2" stroke-linecap="round"/>'],
  corona: ['♛','<path d="M4.5 10.5 9 22h14l4.5-11.5-6.2 5.6L16 7.5l-5.3 8.6Z" fill="#ffffff"/><path d="M8.5 23.8h15V27h-15Z" fill="#ffffff"/><circle cx="4.5" cy="9" r="2" fill="#ffffff"/><circle cx="16" cy="6" r="2" fill="#ffffff"/><circle cx="27.5" cy="9" r="2" fill="#ffffff"/>'],
};
const simbolos = {};
for (const [id, [simbolo, dibujo]] of Object.entries(SIMBOLOS)) {
  simbolos[id] = simbolo;
  escribir('simbolo', id, `<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="0 0 32 32">${dibujo}</svg>`);
}
// El borde de línea discontinua de los huecos de tropa vacíos («Salir al mundo», `.salida-hueco.vacio` del prototipo): UI Toolkit no pinta
// bordes discontinuos, así que va de fondo, a la medida del hueco (110 × 174) y en blanco para teñirlo.
escribir('simbolo', 'hueco', '<svg xmlns="http://www.w3.org/2000/svg" width="110" height="174" viewBox="0 0 110 174"><rect x="1" y="1" width="108" height="172" rx="3" fill="none" stroke="#ffffff" stroke-width="1.5" stroke-dasharray="6 4"/></svg>');
simbolos.hueco = '┄';
// La casa (resides aquí o no) y la corona (capital) del blasón de la plaza (`ui/hudPlaza.ts`, `CASA` y `CORONA`), leídas del fuente, en blanco.
const fuenteHud = readFileSync(join(prototipo, 'src/ui/hudPlaza.ts'), 'utf8');
for (const [id, constante] of [['residencia', 'CASA'], ['capital', 'CORONA']]) {
  const svg = fuenteHud.match(new RegExp(`const ${constante} = '([^']*)'`))[1];
  escribir('simbolo', id, svg.replace(/currentColor/g, '#ffffff').replace(/ aria-hidden="true"/g, '').replace('<svg ', '<svg xmlns="http://www.w3.org/2000/svg" '));
  simbolos[id] = constante === 'CASA' ? '⌂' : '♛';
}
// Los botones del riel de la plaza (`ui/hudPlaza.ts`, tabla `ICONO_RIEL`: Construcción, Gobierno…), leídos del fuente con su trazo (`T`) y su
// relleno (`R`), teñibles como los edificios.
const inicioRiel = fuenteHud.indexOf('export const ICONO_RIEL');
const tablaRiel = fuenteHud.slice(inicioRiel, fuenteHud.indexOf('} as const', inicioRiel));
const trazoT = fuenteHud.match(/const T = '([^']*)'/)[1], rellenoR = fuenteHud.match(/const R = '([^']*)'/)[1];
const riel = Object.fromEntries([...tablaRiel.matchAll(/^\s{2}([a-zA-Z]+): `([^`]*)`/gm)].map((m) => [m[1], m[2].replace(/\$\{T\}/g, trazoT).replace(/\$\{R\}/g, rellenoR)]));
for (const [id, svg] of Object.entries(riel)) escribir('riel', id, tenible(svg));
// El candado de lo bloqueado (🔒 en las recetas y la reserva del prototipo): no es de Twemoji bajado, se dibuja aquí.
{
  escribir('simbolo', 'candado', '<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="0 0 32 32"><path d="M10 14v-4a6 6 0 0 1 12 0v4" fill="none" stroke="#ffffff" stroke-width="2.8"/><path d="M7 14h18v14H7Z" fill="#ffffff"/></svg>');
  simbolos.candado = '🔒';
}

// Los iconos de la barra del jugador (`ui/barraJugador.ts`, tabla `ICONO`: el yelmo de la Base, la campana de Avisos…), leídos del fuente
// como los edificios, en el mismo trazo (24×24, 1,7) y en blanco para teñirlos. `menu` no es del prototipo: el menú de la barra de Conquest.
const fuenteBarra = readFileSync(join(prototipo, 'src/ui/barraJugador.ts'), 'utf8');
const inicioBarra = fuenteBarra.indexOf('const ICONO');
const tablaBarra = fuenteBarra.slice(inicioBarra, fuenteBarra.indexOf('};', inicioBarra));
const barra = Object.fromEntries([...tablaBarra.matchAll(/^\s{2}([a-zA-Z]+): '([^']*)'/gm)].map((m) => [m[1], m[2]]));
barra.menu = '<path d="M4 7h16M4 12h16M4 17h16"/>';
for (const [id, trazo] of Object.entries(barra))
  escribir('barra', id, `<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="#ffffff" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">${trazo}</svg>`);

const glifos = {};
for (const [id, glifo] of Object.entries(GLIFOS)) {
  const codigo = [...glifo].map((c) => c.codePointAt(0).toString(16)).filter((c) => c !== 'fe0f').join('-');
  if (conEmoji && !existsSync(join(destino, 'glifo', `${id}.svg`))) {
    const r = await fetch(`https://cdn.jsdelivr.net/gh/jdecked/twemoji@15.1.0/assets/svg/${codigo}.svg`);
    if (r.ok) escribir('glifo', id, await r.text());
    else console.error(`sin glifo de Twemoji para ${id} (${glifo}, ${codigo}): ${r.status}`);
  }
  // En la lista solo los que ya están (sin --emoji no se bajan; la interfaz los deja sin icono).
  if (existsSync(join(destino, 'glifo', `${id}.svg`))) glifos[id] = { glifo, codigo };
}

// Y el de los recursos sin emoji propio (el prototipo pone `?? '📦'`).
if (conEmoji && !existsSync(join(destino, 'recurso', '_otro.svg'))) {
  const r = await fetch('https://cdn.jsdelivr.net/gh/jdecked/twemoji@15.1.0/assets/svg/1f4e6.svg');
  if (r.ok) escribir('recurso', '_otro', await r.text());
}
if (conEmoji && !existsSync(join(destino, 'recurso', 'LICENSE-GRAPHICS.txt'))) {
  const l = await fetch('https://cdn.jsdelivr.net/gh/jdecked/twemoji@15.1.0/LICENSE-GRAPHICS');
  if (l.ok) writeFileSync(join(destino, 'recurso', 'LICENSE-GRAPHICS.txt'), await l.text());
}

const autores = Object.fromEntries(Object.entries(datos.mapa).map(([id, i]) => [id, { autor: i.autor, icono: i.icono }]));
writeFileSync(join(destino, 'iconos.json'), JSON.stringify({
  edificio: Object.keys(datos.edificio), tropa: Object.keys(datos.tropa), parte: Object.keys(datos.parte), mapa: autores, recurso: recursos, glifo: glifos, simbolo: simbolos, barra: Object.keys(barra), riel: Object.keys(riel),
}, null, 1));
// Los créditos, junto a los iconos (las licencias obligan a citar a los autores).
const porAutor = {};
for (const [id, i] of Object.entries(autores)) (porAutor[i.autor] ??= []).push(`${i.icono} (${id})`);
writeFileSync(join(destino, 'CREDITOS.md'), `# Créditos de los iconos provisionales del mundo

Sacados de BronzeAgeClient por \`Tools/IconosDelPrototipo/generar.mjs\`; no se tocan a mano.

- \`edificio/\`, \`tropa/\` y \`parte/\`: dibujos propios del prototipo (\`src/ui/iconosEdificio.ts\`, \`iconosTropa.ts\`, \`hudInterior.ts\`).
- \`simbolo/\`: los símbolos del prototipo que no son emoji (➜ ⧗ ⚑ ⛟ ⌂ ♜ ✦ ♛ ⛨), dibujados en este script (no están en Twemoji ni en las letras).
- \`mapa/\`: iconos de [game-icons.net](https://game-icons.net) (repositorio https://github.com/game-icons/icons), con licencia
  [Creative Commons BY 3.0](https://creativecommons.org/licenses/by/3.0/). Autores:
${Object.entries(porAutor).map(([a, l]) => `  - **${a}**: ${l.join(', ')}`).join('\n')}
- \`recurso/\` y \`glifo/\`: emoji de [Twemoji](https://github.com/jdecked/twemoji) 15.1.0, © Twitter, Inc. y otros colaboradores, con licencia
  [Creative Commons BY 4.0](https://creativecommons.org/licenses/by/4.0/) (texto en \`recurso/LICENSE-GRAPHICS.txt\`). Mismo punto de código que
  \`RECURSO_ICONO\` y que los glifos sueltos del prototipo.
`);

console.log(`edificios ${Object.keys(datos.edificio).length}, partes ${Object.keys(datos.parte).length}, tropas ${Object.keys(datos.tropa).length}, mapa ${Object.keys(datos.mapa).length}, recursos ${Object.keys(recursos).length}, glifos ${Object.keys(glifos).length}, símbolos ${Object.keys(simbolos).length}${conEmoji ? ' (con emoji)' : ''}`);
