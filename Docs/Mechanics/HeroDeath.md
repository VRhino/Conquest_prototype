# Muerte del Héroe y Respawn

## 1. Resumen

Cuando el héroe es eliminado en batalla se activa un ciclo de penalización controlada:

- El héroe entra en un **cooldown de respawn progresivo** (aumenta con cada muerte consecutiva).
- La **cámara cambia a modo espectador**: el jugador puede observar a aliados mientras espera.
- El **squad activo mantiene posición** en su ubicación actual mientras el héroe está muerto.
- **10 segundos antes del respawn**: el squad inicia retirada automática hacia el spawn point.
- **5 segundos después de iniciar la retirada**: el squad desaparece del campo de batalla.
- Al completarse el cooldown, el héroe reaparece en el spawn point seleccionado durante la fase de preparación, con el squad que le quede vivo (o solo si no hay unidades).

---

## 2. Trigger de muerte

La muerte del héroe se detecta en `HeroAttack.System.cs` cuando la vida del héroe llega a 0:

1. `HeroLifeComponent.isAlive` se establece a `false`.
2. Se emite un evento de muerte que dispara las consecuencias paralelas (ver sección 3).
3. La entidad del héroe permanece en el mundo ECS pero deja de procesar sistemas de input y movimiento.

El héroe no puede ser atacado ni interactuar con el entorno mientras `isAlive == false`.

---

## 3. Consecuencias inmediatas

Al detectarse `isAlive == false`, en el mismo frame:

| Consecuencia | Sistema responsable |
|--------------|---------------------|
| Squad activo recibe orden de **mantener posición** | `HeroRespawn.System.cs` o `SquadOrderSystem` |
| Cámara cambia a **modo espectador** | `HeroCameraController` |
| Se inicia el **timer de cooldown de respawn** | `HeroRespawn.System.cs` |
| Se deshabilita el procesamiento de **input del héroe** | `HeroInputSystem` (no lee input si `!isAlive`) |

---

## 4. Comportamiento del squad mientras el héroe está muerto

| Fase | Tiempo | Comportamiento |
|------|--------|----------------|
| **Espera** | Desde muerte hasta T-10s del respawn | Squad mantiene posición; puede recibir órdenes pasivas de posición pero no persigue |
| **Retirada automática** | T-10s antes del respawn | Squad inicia movimiento hacia el spawn point evitando zonas de alta presencia enemiga |
| **Desaparición** | 5 s después de iniciar retirada | Squad desaparece del campo de batalla (entidades ECS y visuales destruidas) |
| **Respawn del héroe** | Al completarse el cooldown | Héroe reaparece con el squad restante, o solo si no quedan unidades |

> El squad **puede recibir daño durante la retirada** — no es invulnerable.

---

## 5. Cooldown de respawn

El tiempo de respawn **aumenta progresivamente** con cada muerte consecutiva del héroe en la misma partida:

| Muerte # | Cooldown |
|----------|----------|
| 1ª | [configurable — verificar en código] |
| 2ª | [configurable — verificar en código] |
| N-ésima | [configurable — verificar en código] |

> Los valores exactos no están especificados en el GDD — verificar constantes en `HeroRespawn.System.cs` o en la config asociada.

El timer de cooldown es visible para el jugador en la pantalla de espectador.

---

## 6. Cámara espectador

Durante el cooldown, `HeroCameraController` cambia a modo espectador:

- El jugador puede **ciclar entre aliados** con una tecla (tecla [configurable — verificar en código]).
- La cámara es **libre sobre el aliado seleccionado** (sigue al héroe aliado en tercera persona).
- El HUD muestra el **timer de respawn** de forma prominente.
- La cámara vuelve automáticamente al propio héroe al completarse el cooldown.

---

## 7. Secuencia de respawn

Una vez que el cooldown expira:

```
┌─────────────────────────────────────────────────────────────────┐
│ 1. HeroRespawn.System.cs detecta cooldown = 0                   │
│                                                                 │
│ 2. HeroLifeComponent.isAlive = true                             │
│    → Se restaura vida completa del héroe                        │
│                                                                 │
│ 3. Héroe reposicionado en el spawn point seleccionado           │
│    en la fase de preparación                                    │
│                                                                 │
│ 4. Squad respawneado (si quedan unidades vivas)                 │
│    → Spawn junto al héroe con las unidades sobrevivientes       │
│    → Si 0 unidades: héroe reaparece solo                        │
│                                                                 │
│ 5. Input del héroe vuelve a procesarse                          │
│    Cámara vuelve al héroe propio                                │
└─────────────────────────────────────────────────────────────────┘
```

---

## 8. Edge cases

| Caso | Resolución |
|------|-----------|
| Héroe muere durante channeling de squad swap | El channeling se cancela inmediatamente (ver `SquadSwap.md` sección 8); no se aplica cooldown de swap |
| Héroe muere con squad ya en retirada (de un swap previo) | La retirada del swap continúa independientemente; el hero death inicia su propio ciclo |
| Ambos héroes aliados mueren simultáneamente | Cada uno procesa su propio cooldown de forma independiente; la cámara del jugador cicla solo por los aliados con `isAlive == true` |
| Squad eliminado durante la retirada automática | El squad se marca como eliminado permanentemente y no estará disponible para futuros swaps |
| Héroe muere exactamente cuando el squad desaparece (T-5s) | El squad ya se considera fuera del campo; el respawn del héroe genera un squad nuevo desde el estado guardado |
| Héroe reaparece y su squad del respawn tiene 0 unidades | El héroe reaparece solo; podrá hacer swap en un supply point si tiene squads disponibles |

---

## 9. Infraestructura existente

| Archivo | Rol |
|---------|-----|
| `Assets/Scripts/Hero/Components/HeroLife.Component.cs` | `isAlive`, `currentHealth`, `maxHealth` |
| `Assets/Scripts/Hero/HeroLife.Authoring.cs` | Configuración de vida inicial desde Inspector |
| `Assets/Scripts/Hero/Systems/HeroRespawn.System.cs` | Timer de cooldown, lógica de respawn, repositionamiento |
| `Assets/Scripts/Hero/Systems/HeroAttack.System.cs` | Aplica daño al héroe y detecta condición de muerte (`health <= 0`) |
| `Assets/Scripts/Camera/HeroCameraController.cs` | Cambia entre modo normal y modo espectador; ciclado entre aliados |

### Componentes / sistemas nuevos necesarios (estimado)

| Componente / Sistema | Propósito |
|----------------------|-----------|
| `HeroDeathEvent` | Evento emitido al detectarse muerte para comunicar a squad, cámara y UI |
| `HeroRespawnTimerComponent` | Timer de cooldown con multiplicador progresivo por muerte |
| Modificación a `SquadOrderSystem` | Aceptar orden de "mantener posición" emitida por sistema de muerte |

---

## 10. Technical Reference

### Componentes ECS

| Archivo | Rol |
|---------|-----|
| `Assets/Scripts/Hero/Components/HeroLife.Component.cs` | Estado de vida del héroe; `isAlive`, HP actual y máximo |

### Sistemas ECS

| Archivo | Rol |
|---------|-----|
| `Assets/Scripts/Hero/Systems/HeroAttack.System.cs` | Aplica daño recibido; escribe `isAlive = false` al llegar a 0 HP |
| `Assets/Scripts/Hero/Systems/HeroRespawn.System.cs` | Gestiona cooldown progresivo, reposicionamiento y restauración de estado |

### MonoBehaviours

| Archivo | Rol |
|---------|-----|
| `Assets/Scripts/Hero/HeroLife.Authoring.cs` | Baker que configura `HeroLifeComponent` desde el Inspector |
| `Assets/Scripts/Camera/HeroCameraController.cs` | Transición a espectador, ciclado de aliados, vuelta al propio héroe |
