# Bodyblock System — Repulsión cross-team sin carving

## Problema que resuelve

`NavMeshObstacle` con carving modifica la topología del NavMesh para **todos** los agentes,
sin distinción de equipo. Las unidades aliadas entraban en loops intentando llegar a sus slots.

## Approach adoptado

Repulsión física per-frame con `agent.Move()`:

- No toca el NavMesh (sin carving, sin CharacterController)
- `agent.Move(offset)` aplica desplazamiento one-shot y clampea al mesh automáticamente
- Compatible con `SetDestination()` que ya usa `UnitNavMeshSystem`

## Relación con la evitación de aliados (lote 2, C14)

El bodyblock solo separa **enemigos**. Los aliados no se empujan: se esquivan por velocidad en
`UnitAvoidanceSystem` (ORCA, ver `Docs/Design/Movimiento_Formaciones.md` §10). Entre enemigos no hay
evitación (D2): los mantiene a distancia la parada derivada del cuerpo y del alcance (C5) y, si llegan a
solaparse, este sistema.

## Archivo

`Assets/Scripts/Squads/Systems/UnitBodyblock.System.cs`

---

## Reglas de repulsión

> Modo por defecto (`bodyblockResolveOverlapOnly = true`): el contacto funciona como una cápsula. Solo se mueve la unidad en `Moving` (y no `Engaging`), deshaciendo el solapamiento `radio − distancia` (la mitad si ambas se mueven; todo si la otra está parada); nunca empuja más allá del contacto. `bodyblockRepulsionStrength` y `bodyblockWallStrength` solo cuentan con `bodyblockResolveOverlapOnly = false` (muelle heredado). Dos unidades `Engaging` se separan con `bodyblockEngagingRadius`.

| Unidad A        | Unidad B (enemigo)  | Push aplicado                          |
|-----------------|---------------------|----------------------------------------|
| Moving          | Formed / Waiting    | Solo A (la muro B empuja al móvil A)   |
| Formed / Waiting| Moving              | Solo B (la muro A empuja al móvil B)   |
| Moving          | Moving              | Ambos 50/50                            |
| Formed          | Formed              | Ninguno (evita vibración entre muros)  |

---

## Formaciones-muro vs formaciones abiertas

En el modo muelle heredado, la fuerza de repulsión varía según si la unidad estática es parte de una formación-muro:

| Formación       | Tipo       | Fuerza al bloquear (`WallStrength`) |
|-----------------|------------|--------------------------------------|
| Line            | Muro       | 60f                                  |
| Testudo         | Muro       | 60f                                  |
| Wedge           | Muro       | 60f                                  |
| Square          | Muro       | 60f                                  |
| ShieldWall      | Muro       | 60f                                  |
| Dispersed       | Abierta    | 8f (RepulsionStrength normal)        |
| Column          | Abierta    | 8f (RepulsionStrength normal)        |

La distinción asegura que los gaps en Dispersed sean atravesables (intencional) mientras
que Line/Testudo/Wedge/Square se comportan como paredes sólidas.

**Por qué `WallStrength = 60f`**: los NavMeshAgents se mueven a ~3-5 m/s. Con
`RepulsionStrength = 8f`, el push per-frame era ~0.7 m/s — insuficiente para contrarrestar
el pathfinding. Con 60f el push supera claramente la velocidad del agente en rango de overlap.

---

## Bloqueo del héroe

### Héroe remoto
Los héroes remotos tienen `NavMeshAgent` activo en NavMesh → son recogidos por la fase de
collect (query con `HeroSpawnComponent + NavAgentComponent + TeamComponent`).
Se tratan como estado `Moving` (sin `UnitFormationStateComponent`). Quedan bloqueados
por las mismas reglas que una unidad Moving.

### Héroe local
El héroe local usa `CharacterController` + sincronización de `LocalTransform`. Su
`NavMeshAgent` no está activo en el NavMesh (`isOnNavMesh = false`) → el sistema lo
excluye automáticamente. El bloqueo se delega a física. Los tres prefabs de unidad
registrados contienen un `CapsuleCollider` no trigger; la matriz ignora héroe-unidad
del mismo equipo y conserva la colisión contra el equipo enemigo. El contrato está
cubierto por PlayMode.

---

## Spatial Grid

Escala máxima: 900 unidades (450 por equipo). Brute-force O(n²) → inaceptable.

- **Cell size = `BodyblockRadius` (0.8 m)**
- Cada entidad se inserta en `(floor(x/cellSize), floor(z/cellSize))`
- Consulta: 9 celdas vecinas (3×3) → ~18 checks/entidad → ~16,200 checks totales
- Estructura: `NativeParallelMultiHashMap<int2, int>` persistente, consumido por el job Burst; se retiró el grid managed duplicado sin consumidores.
- Tras el calentamiento, se reutilizan capacidad del grid y buffers nativos sin asignación administrada en el Update medido del hilo principal.

---

## Configuración (`SquadSpawnConfig`)

| Campo                           | Valor por defecto | Descripción                                      |
|---------------------------------|-------------------|--------------------------------------------------|
| `bodyblockRadius`               | 0.8 m             | Radio de colisión y tamaño de celda              |
| `bodyblockResolveOverlapOnly`   | true              | Contacto tipo cápsula en vez de muelle           |
| `bodyblockRepulsionStrength`    | 8                 | Fuerza Moving vs Moving / Formed no-muro         |
| `bodyblockWallStrength`         | 60                | Fuerza cuando una formación-muro bloquea         |
| `bodyblockMaxPushSpeed`         | 18 m/s            | Velocidad máxima de corrección, independiente FPS|
| `bodyblockEngagingRadius`       | 0.35 m            | Radio reducido entre unidades Engaging           |
| `bodyblockEngagingStrength`     | 3                 | Fuerza suave entre unidades Engaging             |

---

## Orden de ejecución

```
UnitNavMeshSystem → UnitFollowFormationSystem → UnitAvoidanceSystem → UnitBodyblockSystem → NavMeshPositionSyncSystem
```

El destino y la velocidad se deciden primero, la evitación fija `agent.velocity` entre aliados, el bodyblock aplica
`agent.Move()` entre enemigos y la posición corregida se copia a ECS en el mismo ciclo.

---

## Edge cases

| Caso                                | Manejo                                                               |
|-------------------------------------|----------------------------------------------------------------------|
| Unidad entre múltiples enemigos     | Offsets se acumulan → sale por el hueco natural                      |
| Push fuera del NavMesh              | `agent.Move()` clampea automáticamente                               |
| Unidad desplazada de su slot        | `UnitFormationStateSystem` detecta → Moving → se auto-corrige        |
| Agent no en NavMesh                 | Guard `agent.isOnNavMesh` (mismo patrón que UnitNavMeshSystem)       |
| Dos enemigos exactamente solapados  | Dirección horizontal determinista derivada del par de entidades      |
| Héroe local sin NavMeshAgent activo | Excluido por `isOnNavMesh`; bloqueado por CapsuleCollider físico     |
| Dos formaciones-muro enfrentadas    | `Formed vs Formed = sin push` → sin vibración, contacto estático OK  |

---

## Verificación en play mode

1. Squad aliado `Line/Testudo` Formed → squad enemigo Moving intenta cruzar → bloqueado
2. Squad aliado `Dispersed` Formed → squad enemigo Moving → puede pasar por gaps (correcto)
3. Ambos squads Moving cruzándose → se empujan mutuamente, ninguno se detiene completamente
4. Héroe remoto Moving vs formación Line enemiga → héroe bloqueado
5. Héroe local (CharacterController) vs unidades con CapsuleCollider → bloqueado físicamente
6. Sin errores NavMesh ni warnings de pathfinding en consola.
7. Tunear `WallStrength` y `BodyblockRadius` si el efecto es demasiado suave/agresivo
