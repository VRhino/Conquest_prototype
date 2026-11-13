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
};
const glifos = {};
for (const [id, glifo] of Object.entries(GLIFOS)) {
  const codigo = [...glifo].map((c) => c.codePointAt(0).toString(16)).filter((c) => c !== 'fe0f').join('-');
  glifos[id] = { glifo, codigo };
  if (!conEmoji || existsSync(join(destino, 'glifo', `${id}.svg`))) continue;
  const r = await fetch(`https://cdn.jsdelivr.net/gh/jdecked/twemoji@15.1.0/assets/svg/${codigo}.svg`);
  if (!r.ok) { console.error(`sin glifo de Twemoji para ${id} (${glifo}, ${codigo}): ${r.status}`); continue; }
  escribir('glifo', id, await r.text());
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
  edificio: Object.keys(datos.edificio), tropa: Object.keys(datos.tropa), parte: Object.keys(datos.parte), mapa: autores, recurso: recursos, glifo: glifos,
}, null, 1));
// Los créditos, junto a los iconos (las licencias obligan a citar a los autores).
const porAutor = {};
for (const [id, i] of Object.entries(autores)) (porAutor[i.autor] ??= []).push(`${i.icono} (${id})`);
writeFileSync(join(destino, 'CREDITOS.md'), `# Créditos de los iconos provisionales del mundo

Sacados de BronzeAgeClient por \`Tools/IconosDelPrototipo/generar.mjs\`; no se tocan a mano.

- \`edificio/\`, \`tropa/\` y \`parte/\`: dibujos propios del prototipo (\`src/ui/iconosEdificio.ts\`, \`iconosTropa.ts\`, \`hudInterior.ts\`).
- \`mapa/\`: iconos de [game-icons.net](https://game-icons.net) (repositorio https://github.com/game-icons/icons), con licencia
  [Creative Commons BY 3.0](https://creativecommons.org/licenses/by/3.0/). Autores:
${Object.entries(porAutor).map(([a, l]) => `  - **${a}**: ${l.join(', ')}`).join('\n')}
- \`recurso/\` y \`glifo/\`: emoji de [Twemoji](https://github.com/jdecked/twemoji) 15.1.0, © Twitter, Inc. y otros colaboradores, con licencia
  [Creative Commons BY 4.0](https://creativecommons.org/licenses/by/4.0/) (texto en \`recurso/LICENSE-GRAPHICS.txt\`). Mismo punto de código que
  \`RECURSO_ICONO\` y que los glifos sueltos del prototipo.
`);

console.log(`edificios ${Object.keys(datos.edificio).length}, partes ${Object.keys(datos.parte).length}, tropas ${Object.keys(datos.tropa).length}, mapa ${Object.keys(datos.mapa).length}, recursos ${Object.keys(recursos).length}, glifos ${Object.keys(glifos).length}${conEmoji ? ' (con emoji)' : ''}`);
