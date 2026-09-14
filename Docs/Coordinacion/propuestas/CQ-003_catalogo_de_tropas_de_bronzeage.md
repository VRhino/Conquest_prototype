# CQ-003 — Una definición de escuadra por cada tropa de BronzeAge

**Destino:** `Conquest_prototype`
**Estado:** ACEPTADO — PENDIENTE_DE_IMPLEMENTACION (revisión Codex y decisión del autor, 2026-09-13)
**Prioridad:** Media (necesaria antes de la primera batalla real con tropas de BronzeAge)
**Solicitante:** BronzeAge / Claude
**Fecha:** 2026-09-13
**Origen:** R04 de `BronzeAgeFase0/Docs/Coordinacion/propuestas/REVISION_CONTRATOS_CODEX_2026-09-11.md`
("publicar el mapeo entre `tropaId` y definiciones Conquest"). Tabla completa en
`BronzeAgeFase0/Docs/Coordinacion/01_Modelo_de_datos_compartido.md` §13, "Equivalencia de tropas con
Conquest".

## Problema

Un `BattleTicket` identifica cada escuadra por su `tropaId` de BronzeAge. Conquest necesita saber qué
definición de escuadra (`SquadData`) instanciar para cada uno. Hoy Conquest tiene 3 definiciones (`spm01`
Spearmen, `arc01` Levy Archers, `sqd01` Squires) y BronzeAge 11 tropas: milicia de lanceros, lanceros con
escudo de mimbre, espadachines de cobre, hacheros ligeros, espadachines de bronce, lanceros pesados,
hacheros armados, honderos, escaramuzadores con jabalina, arqueros y arqueros con arco compuesto.

## Comportamiento solicitado

- Crear en Conquest una `SquadData` por cada tropa de BronzeAge, con `id` igual al `tropaId`. Ese es el ID
  canónico: no se mantienen dos IDs para la misma tropa.
- Hasta tener modelos propios, reutilizar como base provisional la definición actual que propone la tabla
  (lanceros sobre Spearmen, espadachines y hacheros sobre Squires, tropas a distancia sobre Levy Archers).
  `spm01`, `arc01` y `sqd01` quedan como alias durante la migración.
- Las reglas tácticas (daño, formaciones, habilidades, movimiento en batalla, módulos cuerpo a cuerpo y a
  distancia) son de Conquest.

## Invariantes de autoridad

- Las **unidades por escuadrón** y el **coste de Liderazgo** los decide BronzeAge (su catálogo por escalón).
  Conquest recibe las unidades de cada escuadra en `SquadSnapshot.efectivosAutorizados` y no usa su propio
  `leadershipCost` para nada que tenga autoridad: el Liderazgo lo valida BronzeAge.
- Un `tropaId` del ticket que Conquest no conozca debe rechazar la batalla de forma explícita, no instanciar
  una escuadra por defecto.

## Compatibilidad

BronzeAge publicará su catálogo de tropas como catálogo versionado en `src/contratos/v1/` (generado desde
`TROPAS_RECLUTABLES`, no copiado a mano). Añadir una tropa nueva en BronzeAge exigirá su definición en
Conquest antes de poder usarla en una batalla real.

## Criterios de aceptación

- Las 11 tropas tienen `SquadData` con `id` = `tropaId`.
- Un ticket con cada una de ellas se instancia con el número de unidades del ticket, no con el `unitCount`
  del asset.
- Un ticket con un `tropaId` desconocido se rechaza con un error claro.

## Dudas para Codex

- Cómo se corresponde el escalón de BronzeAge (1 leva … 5 élite) con vuestro `SquadRarity`.
- ¿Honderos y escaramuzadores con jabalina necesitan módulos a distancia propios, o sirve el de arqueros
  como base provisional?
- ¿Alguna tropa de la lista no encaja con las bases propuestas?

## Revisión de Codex y decisiones del autor (2026-09-13)

Se acepta CQ-003 con su alcance original: cerrar el puente para las 11 tropas que BronzeAge ya publica. La
ampliación histórica hasta las reformas macedónicas no se introduce en esta propuesta; se diseña por separado
en `BronzeAgeFase0/Docs/Coordinacion/propuestas/BA-006_progresion_historica_y_roster.md`. Cuando BronzeAge
publique las nuevas tropas y tecnologías aceptadas, se abrirá otra propuesta CQ para sus definiciones tácticas.

### El significado de "base provisional"

`spm01`, `sqd01` y `arc01` señalan una familia inicial de prefab, animación y comportamiento. No autorizan a
copiar ciegamente todas las estadísticas, módulos ni referencias del asset original. Cada `tropaId` canónico
tendrá su propio `SquadData` y sus propios assets de módulos cuando corresponda, aunque provisionalmente dos
definiciones empiecen con los mismos valores.

Esta separación es necesaria porque:

- `Spearmen.asset` no tiene hoy `meleeData` serializado, mientras `SquadData.IsMelee` depende de que exista;
- el bloqueo con escudo de `Squires` no corresponde automáticamente a todos los hacheros;
- una honda, una jabalina y un arco necesitan trayectorias, alcances, cadencias y proyectiles distintos.

Antes de derivar las tres tropas de lanza se debe reparar o sustituir el módulo melee de `Spearmen`. No se
considera aceptable conservar una definición que instancie unidades incapaces de atacar.

### Escalón estratégico y `SquadRarity`

No son la misma autoridad. El escalón de BronzeAge decide unidades, coste de Liderazgo y economía. La rareza
de Conquest es metadato táctico/de presentación y no puede cambiar esos valores. Como valor inicial coherente,
no como conversión irreversible, se usará:

| Escalón BronzeAge | `SquadRarity` inicial |
|---:|---|
| 1 — leva | `levy_tier` |
| 2 — línea | `trained_tier` |
| 3 — veterana | `seasoned_tier` |
| 4 — pesada | `veteran_tier` |
| 5 — élite | `elite_tier` |

Conquest puede ajustar la rareza explícita de una definición futura sin que eso modifique el escalón
estratégico. El adaptador nunca deriva reglas de autoridad desde `SquadRarity`.

### Honderos y jabalineros

Necesitan `SquadRangedData` propios. Para el primer pase pueden duplicar valores del módulo de Levy Archers,
pero no compartir la misma referencia como definición final: deben poder evolucionar independientemente y
tener un identificador/configuración de proyectil propios. Reutilizar temporalmente un modelo visual no
convierte una jabalina o una piedra en flecha a nivel de comportamiento.

### Ajuste de criterios de aceptación

Además de los criterios originales:

- cada una de las 11 definiciones resuelve por su `tropaId` canónico sin fallback silencioso;
- `spm01`, `arc01` y `sqd01` se resuelven solo mediante una tabla explícita de alias de migración, no mediante
  `SquadData` duplicados con dos identidades canónicas;
- las tres familias de proyectil (piedra, jabalina y flecha) tienen módulos independientes, aunque sus valores
  o visuales sean provisionales;
- toda definición cuerpo a cuerpo tiene `meleeData` válido y toda definición a distancia tiene `rangedData`
  válido;
- `efectivosAutorizados` del ticket prevalece sobre `unitCount`, y `SquadRarity`/`leadershipCost` nunca
  prevalecen sobre las reglas estratégicas recibidas de BronzeAge.
