# CQ-006 — Tropas y edificios de las Eras I-III de BronzeAge

**Destino:** `Conquest_prototype`
**Estado:** LISTO_PARA_REVISION
**Prioridad:** Media — necesaria antes de la primera batalla real con una tropa nueva
**Solicitante:** BronzeAge / Claude
**Fecha:** 2026-09-29
**Origen:** la revisión de BA-006 ("Progresión histórica, tecnología y roster") con el autor del proyecto. Es la
"otra propuesta CQ" que anunciaba la revisión de CQ-003 para las tropas nuevas. Fuentes en `BronzeAgeFase0`:
catálogo publicado `src/contratos/v1/catalogoTropas.json` (`version` 4); reglas en `Docs/Game/5_Sistema_Militar_y_Combate.md`
§5.8 (roster) y §5.12.5 (velocidad), y `Docs/Game/4_Sistema_de_Poblacion_Construccion_y_Mantenimiento.md` §4.2.1
(edificios); respuesta a BA-006 en `Docs/Coordinacion/propuestas/BA-006_progresion_historica_y_roster.md`, al final.

## Problema

BronzeAge ha implementado las tres primeras Eras de su progresión (tecnología por Facción, de ~1300 a ~600 a. C.). Su
roster pasa de 11 a 26 tropas, tres tropas existentes cambian de escalón y el asentamiento gana tres edificios. Conquest
no tiene definición para ninguna de las 15 tropas nuevas, así que hoy una batalla de Unity con una de ellas no puede
instanciarse (CQ-003: un `tropaId` desconocido rechaza la batalla).

## Comportamiento solicitado

### 1. Una `SquadData` por cada tropa nueva, con `id` = `tropaId`

Mismo criterio que CQ-003: BronzeAge decide unidades, coste de Liderazgo y escalón (y lo manda en el ticket); Conquest
decide todo lo táctico. "Familia" es una sugerencia de por dónde empezar, no una copia de estadísticas.

| `tropaId` | Nombre | Escalón | Unidades | Liderazgo | Cómo combate | Equipo (BronzeAge) | Familia sugerida |
|---|---|---:|---:|---:|---|---|---|
| `lenadores` | Leñadores | 1 | 30 | 7 | cuerpo a cuerpo, **sin formación** | hacha de leñador | leva, desorganizada |
| `granjeros` | Granjeros | 1 | 30 | 7 | cuerpo a cuerpo, **sin formación** | aperos | leva, desorganizada |
| `exploradores_caballo` | Exploradores a caballo | 2 | 20 | 14 | montada ligera, floja en combate | arma de cobre + caballo | caballería ligera |
| `carros_guerra` | Carros de guerra | 4 | 15 | 32 | carro de choque (auriga + arquero/lancero) | carro + 2 caballos + arma de bronce | carro |
| `jinetes_asirios` | Jinetes asirios | 2 | 20 | 14 | montada ligera | arma de hierro + caballo | caballería ligera |
| `guerreros_filisteos` | Guerreros filisteos (Peleset) | 3 | 18 | 22 | infantería de línea | arma de hierro + corselete | Squires |
| `shardana` | Shardana | 4 | 15 | 32 | infantería de choque, espada larga y escudo redondo | espada de bronce de calidad | Squires |
| `hequetai` | Hequetai (guardia de bronce) | 5 | 12 | 45 | infantería pesada de élite | bronce de calidad + panoplia | Spearmen / Squires |
| `honderos_rodios` | Honderos rodios | 3 | 18 | 22 | a distancia, **honda** de largo alcance | honda + armadura básica | módulo de honda |
| `peltastas` | Peltastas | 3 | 18 | 22 | a distancia, **jabalina**, sin armadura | 2 jabalinas de hierro | módulo de jabalina |
| `jinetes_escitas` | Jinetes escitas | 3 | 18 | 22 | **montada a distancia** (arco a caballo) | arco + caballo | caballería + módulo de arco |
| `hoplitas_ciudadanos` | Hoplitas ciudadanos | 3 | 18 | 22 | infantería de lanza en **falange** | lanza de hierro + armadura de bronce | Spearmen |
| `caballeria_asiria` | Caballería asiria | 3 | 18 | 22 | montada de choque (lanceros) | lanza de hierro + armadura de bronce + caballo | caballería de choque |
| `arqueros_escitas` | Arqueros escitas | 5 | 12 | 45 | a distancia, arco de élite | arco + armadura básica | Levy Archers |
| `espartiatas` | Espartiatas | 4 | 15 | 32 | falange de élite | hierro de calidad + bronce de calidad | Spearmen |

Leñadores y granjeros son "muy malos y muy desorganizados" por diseño: su rasgo en batalla es que no forman.

### 2. Tres tropas existentes cambian de escalón

El catálogo publicado ya lo refleja (unidades sin cambio, Liderazgo sí):

| `tropaId` | Escalón antes → ahora | Liderazgo antes → ahora | `SquadRarity` inicial (tabla de CQ-003) |
|---|---|---|---|
| `milicia_lanceros` | 1 → 2 | 7 → 14 | `levy_tier` → `trained_tier` |
| `lanceros_mimbre` | 1 → 2 | 7 → 14 | `levy_tier` → `trained_tier` |
| `hacheros_armados` | 4 → 3 | 32 → 22 | `veteran_tier` → `seasoned_tier` |

### 3. Caballería y carros

BronzeAge estrena una cuarta clase de velocidad estratégica, **montada**: 28, la única que alcanza a las caravanas
rápidas. Los carros van a 20. Afecta al mapa de BronzeAge, no a la batalla; en Unity, cómo se mueve y carga la
caballería es de Conquest. Hoy el catálogo publica toda la caballería y los carros con `tipo: cuerpo_a_cuerpo` (el
campo solo admite `cuerpo_a_cuerpo` y `a_distancia`).

### 4. Edificios nuevos del asentamiento (vista 3D, BA-003/BA-005)

| `EdificioTipo` | Huella (celdas) | Dónde | Nota |
|---|---|---|---|
| `caballerizas` | 3×2 | barrio militar, como Barracón y Galería | caballería y carros; niveles internos 1-3 |
| `salaConsejo` | 3×3 | sin barrio temático, como el Palacio | requisito del nivel 4; nombre neutro, el aspecto lo pone la cultura |
| `minaHierro` | 1×1 | **mapa general**, sobre su yacimiento, como las otras minas | no entra en la rejilla del asentamiento |

El `palacio` pasa a tener niveles internos 1-3, con la misma huella 4×4. Ninguna huella existente cambia, así que
`LAYOUT_VERSION` no sube.

## Invariantes de autoridad

Los de CQ-003, sin cambios: unidades y Liderazgo los decide BronzeAge (`efectivosAutorizados` prevalece sobre
`unitCount`); un `tropaId` desconocido rechaza la batalla; `SquadRarity` nunca decide reglas estratégicas.

## Compatibilidad

- Catálogo de tropas `version` 4. Los tickets citan `BattleRules.versionCatalogoTropas`.
- Qué tropas puede reclutar cada Facción lo decide BronzeAge (tecnología, edificio, equipo) antes de que exista la
  escuadra. El `BattleTicket` lleva escuadras, no tecnologías: Unity no necesita saber nada de las Eras.
- Hasta que Conquest tenga una definición, BronzeAge puede resolver con números las batallas en las que aparezca esa
  tropa; no se instancia nada por defecto.

## Criterios de aceptación

- Las 15 tropas nuevas tienen `SquadData` con `id` = `tropaId`, con el módulo que corresponde a cómo combaten
  (melee, honda, jabalina, arco; montada donde aplique).
- Las tres tropas que cambian de escalón usan su `SquadRarity` nuevo.
- Un ticket con cada tropa nueva se instancia con las unidades del ticket.
- Caballerizas y Sala del Consejo se dibujan en `SettlementPreview` con su huella; la Mina de hierro, en el mapa general.

## Dudas para Codex

- ¿Conviene ampliar `tipo` del catálogo con `montada` y `montada_a_distancia` (cambio de contrato, `schemaVersion`), o
  basta con que Conquest lo deduzca de su propia `SquadData`?
- ¿El carro de guerra es una escuadra de "unidades-carro" (15 carros) o necesita otra representación en ECS?
- ¿La falange (hoplitas, Espartiatas) y la formación cerrada (`disciplina_formacion`) son la misma mecánica táctica
  en Conquest, o dos distintas?
- ¿Qué base provisional preferís para la caballería mientras no haya modelos de caballo?
