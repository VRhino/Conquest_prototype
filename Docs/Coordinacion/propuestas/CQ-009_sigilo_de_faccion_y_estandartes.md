# CQ-009 — Sigilo de Facción: estandartes, banderas e insignias de título

**Destino:** `Conquest_prototype`
**Estado:** BORRADOR
**Prioridad:** Baja — nada de la batalla depende de esto; es presentación de la identidad de cada Facción
**Solicitante:** BronzeAge / Claude
**Fecha:** 2026-10-05
**Origen:** `Docs/Mecanicas a desarrollar.md` §27 de `BronzeAgeFase0` (rama `claude/gracious-sinoussi-5b2ee7`, sin fusionar a `main`
todavía). Canon en `Docs/Game/2_Sistema_Politico_Facciones_y_Cargos.md` §2.8.1; modelo en
`Docs/Coordinacion/01_Modelo_de_datos_compartido.md` §23; decisiones en `Consideraciones/Identidad_Visual_Definicion.md`.

## Problema

Cada Facción tiene ahora un **sigilo**: su identidad visual, elegida por su Rey al crearla y **que no cambia nunca** durante la
partida. De lejos, un ejército ajeno es solo una bandera y unos estandartes (BronzeAge Doc 5.12.7), así que el sigilo es lo único
que distingue a una Facción de otra en el mapa. Conquest no sabe que existe.

**No afecta a la batalla ni a la cultura.** La cultura es del asentamiento y de sus edificios (BronzeAge BA-006), y no interviene
en el sigilo. `BattleTicket` y `BattleResult` no cambian.

## Qué llega

```text
Sigilo { formaId, campoId, emblemaId, colorPrimarioId, colorSecundarioId,
         colorEmblemaId, orlaId, colorOrlaId }     solo ids del catálogo (orlaId `ninguna` = sin marco)
Faccion.sigilo                    en la proyección del jugador (`facciones[]`, públicas), sin redactar
faccion.creada.payload.sigilo     el sigilo con el que nace la Facción
Titulo.tituloId                   faccionMasGrande | mayorPoderEconomico | mayorEjercito | granRey | mecenasAedas
titulo.nace / titulo.cambia_manos .payload.tituloId
catalogoSigilos.json              src/contratos/v1/catalogoSigilos.json (schema `CatalogoSigilos`): campos[], emblemas[],
                                  colores[{id,nombre,hex}], reservados{neutro,bandidos,mercenarios}
```

- Son **6 formas de escudo, 18 campos, 56 emblemas, 6 orlas y 12 colores** (más de 600 millones de combinaciones), versión 4 del catálogo. Los emblemas son símbolos documentados
  de cada civilización (micénica y minoica, hitita, mesopotámica y persa, fenicia, egipcia, helénica; referencias en
  `Consideraciones/Identidad_Visual_Definicion.md` §7). Los ids **solo se añaden, nunca se retiran** (hasta ahora no había partidas reales).
- **Único por servidor**: no hay dos Facciones con el sigilo exacto igual; los parecidos sí se permiten. Dos Facciones pueden
  compartir forma, campo, emblema o un color.
- **Reservados**: la gente sin Facción, los bandidos y los campamentos de mercenarios usan un sigilo del servidor (`reservados`);
  su emblema no está en `emblemas`.
- Conquest **no escribe** sigilos: solo los dibuja.

## Comportamiento solicitado

1. **Bandera de batalla y estandartes de ejército**: dibujar el sigilo de la Facción de cada bando. A tamaño de mapa (~24 px) debe
   distinguirse: probar con el catálogo real antes de fijar el arte (forma antes que color, por el daltonismo).
2. **Banderas de asentamiento y territorio**: usar el sigilo, y el color principal para teñir el territorio.
3. **Insignia por título de servidor**: una insignia fija por `tituloId` junto al nombre de la Facción que lo ostenta.
4. **Marcos derivados (sin dato propio)**: el Gran Rey añade una corona o marco al estandarte de su Facción; una Liga por vasallaje
   se muestra con el sigilo de la señora y una por alianza con la orla de los sigilos de sus miembros. Se calculan con
   `relaciones` y el `granReyFaccionId` de la Liga, públicos.
5. **Arte por id**: cada `campoId`, `emblemaId` y `colorId` del catálogo necesita su representación 3D/2D. Si falta una pieza, caer
   en el sigilo neutro de `reservados` en vez de fallar.

## Fuera de alcance

Cultura, estilo de edificios, música y audio: no entran aquí (BronzeAge `Docs/Mecanicas a desarrollar.md` §43).

## Referencia

BronzeAgeClient (jugador) ya lo dibuja en 2D (`src/sigilo/`): escudo SVG con forma, fondo, emblema y orla; la **corona del Gran Rey**
sobre el estandarte; la **Liga** con el sigilo de su señora (vasallaje) o con los de sus miembros (alianza); y las **insignias de los
5 títulos** (`imperio.ts`, `insignias.json`). Sirve de guía de composición. Los emblemas son iconos de [game-icons.net](https://game-icons.net) (CC BY 3.0): `src/sigilo/emblemas.json` lleva el
trazado de cada id con su autor, y Conquest puede usar los mismos (citando a los autores, ver `src/sigilo/CREDITOS.md`) o
hacer los suyos. Faltan, para encargar: escudo en ocho, Puerta de los Leones, águila bicéfala, toro alado, árbol sagrado, casco de
colmillos y sol de Vergina; si Conquest los dibuja, se añaden al catálogo.
