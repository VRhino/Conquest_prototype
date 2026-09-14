# CQ-004 — Botín en el resultado de batalla y catálogo de objetos

**Destino:** `Conquest_prototype`
**Estado:** LISTO_PARA_REVISION
**Prioridad:** Media — hace falta antes del primer schema de `BattleResult`
**Solicitante:** BronzeAge / Claude
**Fecha:** 2026-09-14
**Origen:** decisión del autor del proyecto (2026-09-14). Contrato vigente en
`BronzeAgeFase0/Docs/Coordinacion/01_Modelo_de_datos_compartido.md` §12.1 y §15, y
`BronzeAgeFase0/Docs/Coordinacion/02_Contrato_Comunicacion_Servidor_Cliente.md` §3.3 (punto 12) y §4.2
(`equipar`).

## Problema

El héroe vive en BronzeAge (doc 01 §12) con el inventario, el equipo y las monedas que Conquest ya persiste en
`Hero.Data.cs`. BronzeAge no puede resolver dos cosas por su cuenta:

1. **Qué objetos y monedas gana un héroe en una partida.** Lo sabe la partida. El autor ha decidido que lo
   mande el servidor de batalla en el `BattleResult`, igual que la XP (CQ-001).
2. **Qué objetos existen y en qué hueco van.** BronzeAge lo necesita para validar el botín y el comando
   `equipar`, y está en `ItemDataSO` / `EnhancedItemDatabase`.

## Comportamiento solicitado

### 1. Botín en `BattleResult`

```text
porHeroe: [{ heroeId, participo, sobrevivioAlCierre, xpGanada, botin? }]

botin
  objetos: [{ itemDefinitionId, cantidad, itemInstanceId?, estadisticas? }]
  monedas: { bronce, plata, oro }
```

- Es un delta de ESA batalla y solo lo reciben héroes con `participo: true`.
- El equipo único lo genera el servidor de batalla con su `itemInstanceId` y sus estadísticas, como hace hoy
  `ItemInstanceService`. Los apilables van sin instancia.
- El servidor de batalla no da más objetos de los que caben: el `HeroSnapshot` del ticket trae
  `casillasInventarioLibres`.
- BronzeAge rechaza el resultado entero (`409`, sin aplicar nada) si un objeto no existe en la versión de
  catálogo del ticket, una cantidad no es un entero positivo, las monedas son negativas o superan el tope de
  `BattleRules`, o los objetos no caben.

### 2. Catálogo de objetos publicado y versionado

Por cada definición:

| Campo | Origen en Conquest |
|---|---|
| `itemDefinitionId` | `ItemDataSO.id` |
| `tipo` | `ItemType` (Weapon / Armor / Consumable / Visual) |
| `categoria` | `ItemCategory` (Helmet, Torso, Gloves, Pants, Boots, Spear, Bow, TwoHandedSword, SwordAndShield) |
| `tipoArmadura` | `ArmorType` (Light / Medium / Heavy) |
| `apilable` | `stackable` |
| `rareza` | `ItemRarity` |

Y además:

- **La regla de hueco**: qué (tipo, categoría) va en cada uno de los seis huecos. Hoy vive en
  `_equipmentSetters` de `EquipmentManagerService` (Weapon → arma; Armor + Helmet → casco; Torso, Gloves,
  Pants, Boots → su hueco).
- **La compatibilidad arma/armadura** (`IsWeaponCompatibleWithArmor`).
- **El tamaño de la rejilla del inventario.**

Todo bajo un `versionCatalogoObjetos` que viaja en el ticket (`BattleRules` y `HeroSnapshot`).

## Mapeo de campos (Conquest → BronzeAge, doc 01 §12)

| Conquest (`Hero.Data.cs`) | BronzeAge (`Heroe`) |
|---|---|
| `heroName` | `displayName` |
| `classId` | `classDefinitionId` |
| `gender` (`Male` / `Female`) | `genero` (`masculino` / `femenino`) |
| `avatar.headId` / `hairId` / `beardId` / `eyebrowId` | `avatar.cabezaId` / `peloId` / `barbaId` / `cejasId` |
| `level`, `currentXP` | `nivel`, `experienciaHaciaSiguienteNivel` |
| `attributePoints`, `perkPoints` | `puntosDeAtributoSinGastar`, `puntosDePerkSinGastar` |
| `strength`, `dexterity`, `armor`, `vitality` | `atributosBase.fuerza`, `destreza`, `armadura`, `vitalidad` |
| `unlockedPerks` | `perksDesbloqueados` |
| `bronze`, `silver`, `gold` | `monedasHeroe.bronce`, `plata`, `oro` |
| `loadouts` | `loadouts` |
| `availableSquads` | no se persiste: lo deriva el adaptador del catálogo reclutable del asentamiento |
| `squadProgress` | `escuadrones` (doc 01 §13) |
| `inventory` | `inventario` |
| `equipment.weapon` / `helmet` / `torso` / `gloves` / `pants` / `boots` | `equipamiento.arma` / `casco` / `torso` / `guantes` / `pantalones` / `botas` |

`InventoryItem` → `ItemInstancia`: `itemId` → `itemDefinitionId`, `itemType` → `tipo`, `quantity` →
`cantidad`, `instanceId` → `itemInstanceId`, `serializedStats` → `estadisticas`, `price` → `precio`,
`slotIndex` → `casillaInventario`. Igual que en Conquest, equipar saca el objeto del inventario y el hueco
guarda el objeto entero.

## Invariantes de autoridad y seguridad

- Solo el servidor de batalla autenticado produce botín; un cliente Unity individual, nunca.
- BronzeAge es el dueño del inventario persistido. Fuera de batalla, todo cambio pasa por sus comandos
  (`equipar`…); Unity no escribe el inventario directamente.
- Idempotencia: reenviar el mismo `resultId` no duplica el botín (`Batalla.appliedResultId`).
- Las monedas del héroe no tienen relación con el oro recurso de BronzeAge.

## Criterios de aceptación

- Un fixture de `BattleResult` con botín válido es aceptado y lo suma al inventario y a las monedas.
- Un objeto fuera de catálogo, una cantidad no positiva, monedas negativas o un botín que no cabe se rechazan
  con `409` sin aplicar nada.
- Reenviar el mismo `resultId` no duplica el botín.
- `equipar` rechaza un objeto en un hueco que no le corresponde según el catálogo publicado.

## Dudas para Codex

1. ¿Se gastan consumibles durante la partida? Si es así, el resultado tendría que traer también los objetos
   gastados, para descontarlos del inventario.
2. ¿Puede perderse o dañarse equipo en una batalla?
3. Compatibilidad arma/armadura: ¿la replica BronzeAge en `equipar` (desequipando lo incompatible, como hace
   hoy `UnequipIncompatiblePieces`), o la resuelve el cliente antes de mandar el comando?
4. ¿Dónde vive el tamaño de la rejilla (`maxSlots`)? ¿Es fijo o depende del héroe?
5. ¿Qué otras fuentes de objetos y monedas tiene Conquest fuera de la batalla (tienda, recompensas)? En
   BronzeAge el héroe no tiene hoy ninguna otra vía, y habría que decidir si existen.
