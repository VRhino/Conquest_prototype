# Bodyblock System — Repulsión cross-team sin carving

## Problema que resuelve

`NavMeshObstacle` con carving modifica la topología del NavMesh para **todos** los agentes,
sin distinción de equipo. Las unidades aliadas entraban en loops intentando llegar a sus slots.

## Approach adoptado

Repulsión física per-frame con `agent.Move()`:

- No toca el NavMesh (sin carving, sin CharacterController)
- `agent.Move(offset)` aplica desplazamiento one-shot y clampea al mesh automáticamente
- Compatible con `SetDestination()` que ya usa `UnitNavMeshSystem`

## Archivo

`Assets/Scripts/Squads/Systems/UnitBodyblock.System.cs`

---

## Reglas de repulsión

| Unidad A        | Unidad B (enemigo)  | Push aplicado                          |
|-----------------|---------------------|----------------------------------------|
| Moving          | Formed / Waiting    | Solo A (la muro B empuja al móvil A)   |
| Formed / Waiting| Moving              | Solo B (la muro A empuja al móvil B)   |
| Moving          | Moving              | Ambos 50/50                            |
| Formed          | Formed              | Ninguno (evita vibración entre muros)  |

---

## Formaciones-muro vs formaciones abiertas

La fuerza de repulsión varía según si la unidad estática es parte de una formación-muro:

| Formación       | Tipo       | Fuerza al bloquear (`WallStrength`) |
|-----------------|------------|--------------------------------------|
| Line            | Muro       | 60f                                  |
| Testudo         | Muro       | 60f                                  |
| Wedge           | Muro       | 60f                                  |
| Square          | Muro       | 60f                                  |
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
excluye automáticamente. El bloqueo se delega a física:

**Acción requerida en Unity Editor:**
Agregar un `CapsuleCollider` (non-trigger) a cada prefab visual de unidad:
- Radius ≈ `0.35f`, Height ≈ `1.8f`, Center Y ≈ `0.9f`
- Layer: `Default` (o el layer que usa el CharacterController del héroe)

El `CharacterController` del héroe no puede atravesar colliders no-trigger → bloqueo
natural sin código adicional.

---

## Spatial Grid

Escala máxima: 900 unidades (450 por equipo). Brute-force O(n²) → inaceptable.

- **Cell size = `BodyblockRadius` (0.8 m)**
- Cada entidad se inserta en `(floor(x/cellSize), floor(z/cellSize))`
- Consulta: 9 celdas vecinas (3×3) → ~18 checks/entidad → ~16,200 checks totales
- Estructura: `Dictionary<(int,int), List<int>>` — `.Clear()` cada frame

---

## Constantes (tunear en play mode)

| Constante          | Valor  | Descripción                                        |
|--------------------|--------|----------------------------------------------------|
| `BodyblockRadius`  | 0.8 m  | Radio de colisión; también es el `CellSize`        |
| `RepulsionStrength`| 8f     | Fuerza Moving vs Moving / Formed no-muro           |
| `WallStrength`     | 60f    | Fuerza cuando formación-muro bloquea a un móvil    |
| `MaxPushPerFrame`  | 0.3 m  | Clamp máximo de desplazamiento por frame           |

---

## Orden de ejecución

```
UnitNavMeshSystem → UnitBodyblockSystem
```

`UpdateAfter(UnitNavMeshSystem)` garantiza que el `SetDestination()` ya fue emitido.

---

## Edge cases

| Caso                                | Manejo                                                               |
|-------------------------------------|----------------------------------------------------------------------|
| Unidad entre múltiples enemigos     | Offsets se acumulan → sale por el hueco natural                      |
| Push fuera del NavMesh              | `agent.Move()` clampea automáticamente                               |
| Unidad desplazada de su slot        | `UnitFormationStateSystem` detecta → Moving → se auto-corrige        |
| Agent no en NavMesh                 | Guard `agent.isOnNavMesh` (mismo patrón que UnitNavMeshSystem)       |
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
