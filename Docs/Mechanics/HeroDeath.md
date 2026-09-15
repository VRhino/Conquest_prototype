# Muerte, retirada del squad y respawn del héroe

Actualizado: 2026-09-15. Este documento describe el comportamiento implementado; la regla de producto “retirada 10 segundos antes del respawn” que aparece en GDD/TDD no está implementada actualmente.

## Resumen vigente

```text
HeroHealthComponent.currentHealth <= 0
  → HeroRespawnSystem marca HeroLifeComponent.isAlive = false
  → inicia deathTimer = respawnCooldown
  → SquadOwnerDeathRetreatSystem compromete retirada inmediatamente
  → el héroe deja de producir/consumir movimiento local
  → al expirar deathTimer se restaura la salud
  → HeroSpawnComponent.hasSpawned = false
  → HeroSpawnSystem selecciona punto y publica nueva revisión de pose
  → LocalHeroCharacterMotor consume el teleport una vez
```

## Componentes de vida y posición

| Componente | Función |
|---|---|
| `HeroHealthComponent` | Salud actual y máxima usada por daño/respawn |
| `HeroLifeComponent` | `isAlive`, `deathTimer` y `respawnCooldown` |
| `HeroSpawnComponent` | Punto, pose, `hasSpawned` y `positionRevision` |
| `HeroMoveIntent` | Intención local; se limpia mientras el héroe no puede moverse |
| `HeroMotorStateComponent` | Velocidad/contactos confirmados del motor local |

`HeroRespawnSystem` procesa sólo la entidad con `IsLocalPlayer`. Detecta muerte a partir de `HeroHealthComponent`, no de componentes de salud de unidades.

## Secuencia de muerte

1. El daño reduce `HeroHealthComponent.currentHealth`.
2. `HeroRespawnSystem`, después de `DamageCalculationSystem`, marca `isAlive = false` y carga `deathTimer` con `respawnCooldown`.
3. `HeroMovementSystem` limpia `HeroMoveIntent` al ver al héroe muerto.
4. `LocalHeroCharacterMotor` tampoco ejecuta movimiento mientras `isAlive` sea falso.
5. `SquadOwnerDeathRetreatSystem`, después de respawn y antes de órdenes/swap/HUD, detecta la transición del owner y compromete la retirada del squad.

No existe actualmente un `HeroDeathEvent` necesario para este flujo; los sistemas observan los componentes de vida en orden explícito.

## Retirada del squad activo

La retirada comienza al detectar la muerte, no cerca del final del cooldown.

`SquadOwnerDeathRetreatSystem`:

- actualiza `SquadStateComponent.lastOwnerAlive`;
- añade `SquadOwnerDeathRetreatComponent` como decisión persistente;
- establece `currentState` y `transitionTo` en `Retreating`;
- activa `retreatTriggered`, que bloquea órdenes posteriores;
- elimina `IsLocalSquadActive`;
- busca un `SpawnPointComponent` activo del mismo equipo;
- añade `RetreatComponent` y `SquadNavigationComponent` cuando existe destino.

Si no existe punto aliado válido, la decisión permanece pendiente y el ancla se conserva. El sistema no inventa un destino `(0,0,0)` y el respawn del dueño no cancela la retirada ya comprometida.

`RetreatLogicSystem` completa el retiro cuando todos los supervivientes alcanzan sus destinos efectivos de slot o cuando vence `ownerDeathRetreatDuration`. Antes de destruir entidades:

- cuenta supervivientes;
- conserva `squadId`, `baseSquadID`, total inicial y efectivos vivos en `InactiveSquadElement`;
- marca la reserva eliminada cuando quedan cero;
- elimina `HeroSquadReference` sólo si sigue apuntando al squad que se retira.

Una retirada originada por swap no se reinicia ni cambia de causa al morir el héroe.

## Respawn

Mientras `isAlive == false`, `HeroRespawnSystem` decrementa `deathTimer`. Al expirar:

1. establece `isAlive = true`;
2. restaura `HeroHealthComponent.currentHealth` a `maxHealth`;
3. marca `HeroSpawnComponent.hasSpawned = false`.

`HeroSpawnSystem` busca primero el spawn ID seleccionado y equipo correctos, luego un punto activo del equipo como fallback. Sólo si encuentra uno:

- corrige altura de terreno;
- calcula rotación hacia el objetivo final enemigo;
- actualiza `spawnPosition` y `spawnRotation`;
- incrementa `positionRevision`;
- escribe `LocalTransform`;
- marca `hasSpawned = true`.

`LocalHeroCharacterMotor` consume la nueva revisión antes del movimiento normal, aplica un teleport seguro deshabilitando temporalmente el `CharacterController`, reinicia gravedad y publica la pose confirmada. Si no hay spawn válido, la revisión no cambia y el héroe no consume una pose ficticia.

## Squad después del respawn

El squad anterior continúa su retirada aunque el héroe ya haya revivido. `SquadSpawningSystem` no crea una segunda instancia mientras la referencia anterior o la reserva en retirada sigan activas. Cuando el cleanup termina, una selección válida puede desplegar nuevamente sólo los efectivos persistidos. Una reserva con cero supervivientes queda eliminada.

## Configuración

| Parámetro | Fuente | Valor por defecto en authoring |
|---|---|---|
| Cooldown de respawn | `HeroLifeComponent.respawnCooldown` | 5 s |
| Duración máxima de retirada por muerte | `SquadSpawnConfigComponent.ownerDeathRetreatDuration` | 5 s |
| Umbral de llegada | `SquadSpawnConfigComponent.retreatArrivalThreshold` | 0,5 m |

Los valores efectivos dependen de los authorings/prefabs de la escena. Una duración de retirada cero permite cleanup inmediato una vez creado el componente.

## Casos cubiertos por regresión

- héroe vivo sin retirada;
- muerte y retirada comprometida;
- ausencia temporal y posterior aparición de punto aliado;
- ancla congelada mientras falta destino;
- respawn antes o después del cleanup;
- persistencia y redespliegue de supervivientes;
- eliminación total;
- referencia de squad reemplazada que no debe borrarse;
- coexistencia con retirada por swap;
- muerte de la última unidad durante una retirada comprometida.

## Límites actuales

- Sólo el héroe local participa en `HeroRespawnSystem`; el ciclo equivalente de héroes de red no está implementado.
- La desaparición/desconexión de la entidad owner no se interpreta automáticamente como muerte.
- Cámara espectador, HUD de cooldown y navegación que evite concentración enemiga no quedan certificados por este pipeline.
- El comportamiento temporal del GDD/TDD difiere: allí la retirada se plantea cerca del respawn; el código actual la inicia inmediatamente.
- No hay todavía validación automatizada de una partida completa con todos los sistemas visuales y UI.

## Archivos principales

- `Assets/Scripts/Hero/Systems/HeroRespawn.System.cs`
- `Assets/Scripts/Hero/Systems/HeroSpawn.System.cs`
- `Assets/Scripts/Hero/Systems/HeroMovement.System.cs`
- `Assets/Scripts/Visual/LocalHeroCharacterMotor.cs`
- `Assets/Scripts/Squads/Systems/SquadOwnerDeathRetreat.System.cs`
- `Assets/Scripts/Squads/Systems/RetreatLogic.System.cs`
- `Assets/Scripts/Squads/Systems/SquadSpawning.System.cs`

Véase también [ciclo de vida del héroe](../Arquitectura/4_Ciclo_Vida_Heroe_2026-09-14.md), [retirada por muerte](../Arquitectura/6_Retirada_Muerte_Dueno_2026-09-14.md) y [motor local](../Arquitectura/14_Motor_Local_Heroe_2026-09-15.md).
