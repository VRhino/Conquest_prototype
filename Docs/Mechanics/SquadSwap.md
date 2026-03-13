# Squad Swap en Supply Points

## 1. Resumen

El héroe puede cambiar su squad activo en un supply point aliado. La interacción sigue el patrón tipo NPC: acercarse al centro del supply point → presionar F → seleccionar squad en la UI → confirmar → channeling de 1 segundo → swap ejecutado.

Solo se puede tener **un squad activo** a la vez. El squad saliente entra en retirada durante 5 segundos y, si sobrevive, queda disponible para ser invocado de nuevo en el futuro.

---

## 2. Precondiciones

Todas las siguientes condiciones deben cumplirse para que el héroe pueda iniciar un squad swap:

| Condición | Detalle |
|-----------|---------|
| Supply point aliado | `SupplyPointComponent.teamOwner == hero.team` |
| Supply point no contested | `SupplyPointComponent.isContested == false` |
| Héroe vivo | El héroe no está en estado muerto/respawning |
| Héroe dentro del radio | Posición del héroe dentro de `ZoneTriggerComponent.radius` del supply point |
| Squad actual fuera de combate | `SquadStateComponent.isInCombat == false` |
| Sin cooldown activo | Han pasado ≥ 10 segundos desde el último swap exitoso |
| Squad alternativo disponible | Existe al menos un squad inactivo que no haya sido eliminado |

Si alguna precondición no se cumple, el prompt de interacción no aparece o el botón de confirmar está deshabilitado (según corresponda al punto de fallo).

---

## 3. Flujo de interacción

```
┌─────────────────────────────────────────────────────────────────┐
│ 1. Héroe entra en zona del supply point aliado                  │
│    → Se habilita prompt en HUD: "F para interactuar"            │
│                                                                 │
│ 2. Héroe presiona F                                             │
│    → Se abre UI de selección de squad (SupplyPointUIController) │
│    → Se muestran squads disponibles (excluye activo y eliminados│
│                                                                 │
│ 3. Jugador selecciona un squad y presiona "Aceptar"             │
│    → Se validan precondiciones                                  │
│    → Si válido: inicia channeling de 1 segundo                  │
│                                                                 │
│ 4. Channeling (1 segundo)                                       │
│    → Barra de progreso visible en HUD                           │
│    → El héroe puede moverse libremente dentro del área          │
│    → Ver sección 8 para condiciones de cancelación              │
│                                                                 │
│ 5. Channeling completo                                          │
│    → Se ejecuta el swap (ver sección 4)                         │
│    → Inicia cooldown de 10 segundos                             │
└─────────────────────────────────────────────────────────────────┘
```

**Nota sobre la UI**: Durante la selección (paso 3), el juego NO se pausa. El héroe permanece vulnerable. Si las precondiciones dejan de cumplirse mientras la UI está abierta (e.g., un enemigo contesta el punto), la UI se cierra automáticamente.

---

## 4. Ejecución del swap

Una vez que el channeling se completa exitosamente, se ejecuta la siguiente secuencia:

1. **Squad saliente → estado Retreating**: El squad actual cambia su estado FSM a `Retreating` (ver sección 5).
2. **Spawn del nuevo squad**: El nuevo squad aparece frente al héroe como squad activo, usando el sistema de spawn estándar (`SquadSpawningSystem`).
3. **Actualización de datos de batalla**:
   - Nuevo squad = activo (`SquadStateComponent.isActive = true`)
   - Viejo squad = inactivo (manejado por el estado `Retreating`)
4. **Emisión de evento**: Se emite `SquadChangeEvent` con referencia al squad entrante y saliente.
5. **Inicio de cooldown**: Timer de 10 segundos asociado al héroe.

El spawn del nuevo squad y la transición a `Retreating` del viejo ocurren en el **mismo frame** para evitar estados intermedios sin squad.

---

## 5. Retirada del squad saliente

| Parámetro | Valor |
|-----------|-------|
| Estado FSM | `Retreating` |
| Duración | 5 segundos |
| Dirección de movimiento | Hacia el punto de inicio del mapa del equipo (spawn point) |
| Vulnerable a daño | Sí |
| Puede recibir órdenes | No |
| Puede atacar | No |

### Resultado al finalizar la retirada

- **Si sobrevive** (al menos 1 unidad viva al terminar los 5s):
  - Se destruyen todas las entidades ECS y GameObjects visuales del squad
  - Se guarda el estado del squad: cantidad de unidades vivas y muertas
  - El squad queda disponible para ser invocado de nuevo

- **Si muere** (todas las unidades eliminadas durante la retirada o antes):
  - El squad se marca como **eliminado permanentemente**
  - No aparecerá en la UI de selección en futuros swaps
  - Sus entidades ECS y visuales se destruyen al morir la última unidad

### Comportamiento durante la retirada

- Las unidades se mueven en formación compacta hacia el spawn point
- No responden a comandos del jugador (C, X, V, etc.)
- No inician combate con enemigos, pero reciben daño si son atacadas
- No bloquean ni esquivan — son vulnerables completamente

---

## 6. Persistencia de squads inactivos

El estado de un squad se preserva entre swaps según las siguientes reglas:

| Situación | Resultado al reinvocar |
|-----------|----------------------|
| Unidad viva al retirarse | Vuelve con **100% HP** |
| Unidad muerta antes de la retirada | Permanece muerta, no reaparece |
| Unidad muerta durante la retirada | Permanece muerta, no reaparece |
| Todas las unidades muertas | Squad **eliminado**, no puede ser invocado |

### Ejemplo

Un squad de 12 Squires donde 4 murieron en combate y 8 sobrevivieron la retirada:
- Al ser invocado de nuevo: aparecen **8 Squires con HP completo**
- Los 4 muertos no regresan

### Notas de implementación

- El conteo de unidades vivas/muertas se persiste en el `SquadDataService` o estructura equivalente al destruir las entidades ECS
- Al reinvocar, el `SquadSpawningSystem` usa el conteo guardado en lugar de los valores base del `SquadData`

---

## 7. Cooldown

| Parámetro | Valor |
|-----------|-------|
| Duración | 10 segundos |
| Inicio | Al completar el channeling (no al confirmar la UI) |
| Alcance | Por héroe, no por supply point |
| Persiste entre zonas | Sí — si el héroe se mueve a otro supply point, el cooldown sigue activo |
| Indicador visual | Timer en HUD junto al indicador de squad activo |

El cooldown **no se inicia** si el channeling es cancelado (ver sección 8).

---

## 8. Cancelaciones

El channeling se cancela inmediatamente si ocurre cualquiera de los siguientes eventos:

| Evento | Momento | Resultado |
|--------|---------|-----------|
| Héroe sale del área del supply point | Durante channeling | Cancela, sin cooldown |
| Enemigo entra en el área (punto pasa a contested) | Durante channeling | Cancela, sin cooldown |
| Héroe muere | Durante channeling | Cancela, sin cooldown |
| Supply point capturado por enemigo | Durante channeling | Cancela, sin cooldown |
| Jugador presiona Escape o cierra la UI | Antes de confirmar | Cancela selección, sin efecto |

### Comportamiento post-cancelación

- No se aplica cooldown
- No se consume ningún recurso
- El héroe puede intentar iniciar un nuevo swap inmediatamente (si las precondiciones se cumplen)
- Se muestra feedback visual/sonoro indicando la cancelación

---

## 9. Edge cases

| Caso | Resolución |
|------|-----------|
| Héroe intenta swap con 0 squads disponibles | Botón de interacción no aparece o UI muestra mensaje "No hay squads disponibles" |
| Squad saliente muere durante retirada y héroe intenta re-invocarlo | No aparece en UI — está eliminado |
| Héroe muere después del swap pero antes de que termine la retirada | La retirada del squad saliente continúa normalmente |
| Dos héroes del mismo equipo intentan swap en el mismo supply point | Cada uno opera independientemente — el supply point no tiene límite de swaps simultáneos |
| Supply point se vuelve contested después del swap pero durante la retirada | La retirada continúa — la cancelación solo aplica durante el channeling |
| Héroe intenta swap mientras el squad saliente anterior aún está en retirada | Permitido si el cooldown de 10s ha pasado — pueden coexistir múltiples squads en retirada |
| El nuevo squad tiene 0 unidades vivas (todas murieron en una invocación previa) | Este squad no aparece en la UI — se considera eliminado |

---

## 10. Infraestructura existente

Los siguientes archivos ya contienen implementación parcial o relacionada:

| Archivo | Rol |
|---------|-----|
| `Assets/Scripts/Squads/SquadSwapRequest.cs` | Evento de solicitud de swap con `newSquadId` y `zoneId` |
| `Assets/Scripts/Squads/Systems/SquadSwap.System.cs` | Sistema ECS que valida la solicitud y emite `SquadChangeEvent` |
| `Assets/Scripts/Squads/SquadChangeEvent.cs` | Evento emitido tras un swap exitoso |
| `Assets/Scripts/UI/Zone/SupplyPointUIController.cs` | UI con botones de selección de squad |
| `Assets/Scripts/Map/SupplyInteraction.System.cs` | Detección de héroes en zona, crea `SquadSwapRequest` al presionar F |
| `Assets/Scripts/Squads/Components/PlayerInteractionComponent.cs` | Captura input F del héroe |
| `Assets/Scripts/Squads/Components/SupplyPointComponent.cs` | Estado de la zona (contested, teamOwner, etc.) |

### Componentes nuevos necesarios (estimado)

| Componente / Sistema | Propósito |
|----------------------|-----------|
| `SquadSwapChannelingComponent` | Timer de channeling (1s), referencia al squad seleccionado |
| `SquadSwapCooldownComponent` | Timer de cooldown (10s) por héroe |
| `SquadRetreatComponent` | Timer de retirada (5s), dirección de movimiento |
| `InactiveSquadStateData` | Persistencia de estado de squads inactivos (unidades vivas/muertas) |
| `SquadSwapChannelingSystem` | Procesa el channeling, verifica cancelaciones frame a frame |
| `SquadRetreatSystem` | Mueve squads en retirada, destruye al finalizar, persiste estado |
| Modificación a `SquadFSMSystem` | Agregar estado `Retreating` al FSM |
| Modificación a `SquadSpawningSystem` | Soportar spawn con conteo reducido de unidades |

---

## 11. Technical Reference

Todos los archivos involucrados en la implementación del Squad Swap, organizados por categoría.

### Componentes ECS

| Archivo | Rol |
|---------|-----|
| `Assets/Scripts/Squads/SquadSwapRequest.cs` | Solicitud de swap creada por input del jugador (squad seleccionado + zona) |
| `Assets/Scripts/Squads/SquadSwapChanneling.Component.cs` | Timer de channeling (1s) y referencia al squad seleccionado |
| `Assets/Scripts/Squads/SquadSwapCooldown.Component.cs` | Cooldown de 10s post-swap asociado al héroe |
| `Assets/Scripts/Squads/SquadSwapExecuteTag.cs` | Tag que marca al héroe para ejecutar el swap en el frame actual |
| `Assets/Scripts/Squads/SquadChangeEvent.cs` | Evento emitido al completar un swap exitoso |
| `Assets/Scripts/Squads/InactiveSquadElement.cs` | Buffer element que almacena squads inactivos disponibles para reinvocación |
| `Assets/Scripts/Squads/SquadRetreatingFromSwap.Component.cs` | Tag que identifica squads en retirada originados por un swap |
| `Assets/Scripts/Squads/SquadIdMapElement.cs` | Buffer element que mapea índice int → string ID del squad |
| `Assets/Scripts/Squads/Components/SquadState.Component.cs` | Estado FSM del squad (incluye estado `Retreating`) |
| `Assets/Scripts/Squads/Components/Retreat.Component.cs` | Datos de retirada: timer, dirección, punto de destino |
| `Assets/Scripts/Squads/Components/SquadNavigation.Component.cs` | Navegación del squad hacia el punto de retirada |
| `Assets/Scripts/Hero/Components/HeroSquadReference.Component.cs` | Referencia al squad activo del héroe |
| `Assets/Scripts/Hero/Components/HeroSquadSelection.Component.cs` | Selección de squad para el próximo spawn |

### Sistemas ECS

| Archivo | Rol |
|---------|-----|
| `Assets/Scripts/Map/SupplyInteraction.System.cs` | Detecta input F en zona de supply point, crea `SquadSwapRequest` |
| `Assets/Scripts/Squads/Systems/SquadSwap.System.cs` | Valida precondiciones del request e inicia channeling |
| `Assets/Scripts/Squads/Systems/SquadSwapChanneling.System.cs` | Tick del timer de channeling, verifica condiciones de cancelación frame a frame |
| `Assets/Scripts/Squads/Systems/SquadSwapExecution.System.cs` | Retira squad viejo (→ Retreating), prepara spawn del nuevo squad |
| `Assets/Scripts/Squads/Systems/SquadSpawning.System.cs` | Spawn del nuevo squad con conteo de unidades reducido si fue invocado previamente |
| `Assets/Scripts/Squads/Systems/SquadFSM.System.cs` | Lockea estado `Retreating` para squads en retirada por swap |
| `Assets/Scripts/Squads/Systems/RetreatLogic.System.cs` | Mueve squad en retirada, persiste estado de unidades, destruye entidades al finalizar |
| `Assets/Scripts/Squads/Systems/SquadSwapCooldown.System.cs` | Tick del cooldown de 10s, remueve componente al expirar |

### UI y Setup

| Archivo | Rol |
|---------|-----|
| `Assets/Scripts/UI/Zone/SupplyPointUIController.cs` | Panel de selección de squad, barra de channeling, indicador de cooldown |
| `Assets/Scripts/UI/Zone/SquadButtonUI.cs` | Botón individual de squad en la UI de selección |
| `Assets/Scripts/UI/Battle/BattleSceneController.cs` | Popula el buffer de mapeo de squad IDs al iniciar la batalla |
| `Assets/Scripts/Shared/DataContainer.System.cs` | Crea el buffer `SquadIdMapElement` en la entidad de datos compartidos |
