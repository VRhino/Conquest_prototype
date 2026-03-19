# TDD

## Índice Técnico (TDD)

### 1. 🧱 Arquitectura General del Proyecto

- 1.1 Versión y configuración de Unity
- 1.2 Render Pipeline: elección y justificación
- 1.3 Estructura general de escenas
- 1.4 Modularidad y separación por sistemas
- 1.5 Arquitectura ECS con Unity DOTS
- 1.6 Integración con Netcode for GameObjects

### 2. 🎮 Control del Jugador y Cámara

- 2.1 Movimiento y control del héroe (TPS)
- 2.2 Control de cámara según estado del héroe
- 2.3 Comandos a escuadras (hotkeys, UI, radial)
- 2.4 Feedback visual y navegación(x)
- 2.5 Modo espectador tras muerte(x)

### 3. 🧠 IA de Escuadras y Unidades

- 3.1 Sistema de navegación (NavMesh)
- 3.2 Comportamiento en formación reactivo
- 3.3 IA de escuadra grupal vs individual
- 3.4 Coordinación de habilidades de escuadra
- 3.5 FSM para estados de escuadras y transición a retirada

### 4. 🏗️ Construcción de Mapas y Escenarios

- 4.1 Herramientas para creación de mapas (Unity Terrain / externos)
- 4.2 Implementación de elementos destructibles (puertas, obstáculos)
- 4.3 Sistema de zonas y triggers físicos (Supply, captura, visibilidad)
- 4.4 Configuración del mapa MVP y puntos claves

### 5. ⚔️ Sistema de Combate y Daño

- 5.1 Combate del héroe (colliders y animaciones)
- 5.2 Combate de escuadras (detección y ataques sincronizados)
- 5.3 Tipos de daño y resistencias (blunt, slashing, piercing)
- 5.4 Cálculo de daño y penetración en C#
- 5.5 Gestión de cooldowns y tiempos de habilidad
- 5.6 Sistema de Bloqueo y Mitigación por Colisión

### 6. 🔄 Flujo de Partida

- 6.1 Transiciones entre escenas (Feudo → Preparación → Combate → Post)
- 6.2 Ciclo de vida del héroe (muerte, respawn, cooldown)
- 6.3 Estado y retirada de escuadra al morir el héroe
- 6.4 Reglas del sistema de captura y uso de supply points
- 6.5 Asignación de spawn inicial
- 6.6 Panatlla de Preparación de Batalla

### 7. 🧬 Progresión y Guardado de Datos

- 7.1 Progresión del héroe (nivel, atributos, perks)
- 7.2 Guardado local en MVP
- 7.3 Estructura de ScriptableObjects para perks y escuadras
- 7.4 Sistema de perks: carga, activación y visualización
- 7.5 Sistema de clases de heroe
- 7.6 Progresión Avanzada de Escuadras y Sinergias
- 7.7 Control de Estados entr Héroe y Unidades del Escuadrón
- 7.8 Estructura de Persistencia del Jugador (MVP y Post MVP)
- 7.9 DataCacheService: Cálculo y Cache de Atributos

## 8. 🌐 Multijugador (MVP)

- 8.1 Arquitectura de red: servidor dedicado
- 8.2 Sincronización de escuadras y héroes (Snapshots o comandos, decisión final)
- 8.3 Interpolación de movimiento y predicción
- 8.4 Comunicación entre jugadores (chat básico)
- 8.5 Cambios de escuadra desde supply points (restricciones de sincronización)

## 9. 🖥️ UI y HUD

- 9.1 Sistema de UI (Canvas con Unity UI)
- 9.2 HUD de batalla: salud, habilidades, escuadra, órdenes
- 9.3 Minimapa dinámico (feudo y combate)
- 9.4 Interfaz de preparación y loadouts
- 9.5 Menús de interacción con supply y puntos de captura
- 9.6 Sistema de Marcadores de Destino (Hold Position)
- 9.7 Scoreboard de Batalla (Panel de Estado Activado con `Tab`)

## 9.8 Post-Battle UI & Victory/Defeat System

`BattleSceneController` (MonoBehaviour en BattleScene) gestiona el ciclo completo de fin de partida:

**Responsabilidades de BattleSceneController:**
- **Inicialización** (`Awake`): Lee `BattleTransitionData`, resetea `DialogueUIState.IsDialogueOpen = false` para limpiar estado que puede haber filtrado de la escena anterior, y activa fallback a `TestEnvironmentInitializer` si no hay `BattleData`
- **Layer culling** (`ConfigureCameraLayerCulling`): Configura `camera.layerCullDistances` — Units a 120 m, Heroes a 150 m; activa `layerCullSpherical = true` para culling esférico en lugar de planar
- **Loading screen**: Se descarta cuando todos los `HeroVisualInstance` esperados están listos + 3 s de delay (`LoadingScreenDelay`). Timeout de seguridad a los 30 s (`MaxLoadingScreenTime`) para evitar loading screen infinita
- **Victory/Defeat** (`HandleMatchEnd`): Monitorea `MatchStateComponent.currentState == MatchState.EndMatch` cada frame. Al detectar fin de partida, activa `_victoryDefeatPanel` y muestra `_victoryObject` o `_defeatObject` según el equipo local. Guarda `WinnerTeam` en `BattleTransitionData` para la post-battle scene
- **Transición** a `PostBattleScene` tras `PostMatchDelay = 10s`
- **Timer**: Cuando el timer llega a 0, los defensores ganan (`winnerTeam = 2`)

**Flujo Victory/Defeat:**
```
MatchStateComponent.EndMatch detectado
  → _victoryDefeatPanel.SetActive(true)
  → Comparar IsLocalPlayer.TeamComponent vs matchState.winnerTeam
  → _victoryObject.SetActive(localWon) / _defeatObject.SetActive(!localWon)
  → BattleTransitionData.WinnerTeam = matchState.winnerTeam
  → Esperar PostMatchDelay (10s) → SceneTransitionService.LoadScene(PostBattle)
```

## 5.7 Hitbox Weapon System (Commit e59b3d62)

El sistema de combate del héroe usa hitboxes por arma (no raycast). Las armas tienen colliders habilitados solo durante el swing activo:

- **Shields**: colliders propios que bloquean proyectiles y ataques frontales
- **Detección de enemigos**: los hitboxes del arma detectan entidades enemigas con `TeamComponent` diferente al atacante
- **Capas**: las armas y escudos usan layers dedicados para evitar auto-colisión entre el héroe y sus propias armas
- El sistema se activa via animación (Animation Events) — no via ECS Systems — porque la detección de hitbox es una responsabilidad visual/física del lado GameObject

## 10. 🔐 Seguridad y Backend (Para expansión futura)

- 10.1 Estado actual (solo local)
- 10.2 Recomendaciones para transición a backend (login, matchmaking, almacenamiento)
- 10.3 Gestión segura de progresión futura

## 11. ⚙️ Extras Técnicos

- 11.1 Sistema de liderazgo (restricciones en loadouts)
- 11.2 Sistema de estamina y gasto por acción
- 11.3 Visualización de formaciones y selección de unidades
- 11.4 Optimización de escena y assets (nivel MVP)

## 12. 📘 Glosario tecnico

---

## 1. 🧱 Arquitectura General del Proyecto

### 1.1 🏗️ Motor y Versión

- **Motor:** Unity
- **Versión:** Unity 2022.3.62f1 (LTS)

### 1.2 🎨 Render Pipeline

- **Pipeline:** URP (Universal Render Pipeline)
- **Justificación:**
    - Buen balance entre rendimiento y calidad visual.
    - Ideal para entornos con escuadras numerosas.
    - Compatible con dispositivos de gama media.

### 1.3 🏗️ Arquitectura Técnica

- **Paradigma Base:** ECS (Entity Component System)
- **Implementación:** Unity Entities 1.3.14 (DOTS)
- **Justificación:**
    - Escalabilidad con múltiples unidades en pantalla.
    - Separación clara entre lógica y datos.
    - Rendimiento optimizado para combate en masa.

### 1.4 🏛️ Organización Modular por Escenas

- El proyecto se divide en **múltiples escenas funcionales**:
    - **Login / Selección de personaje**
    - **Feudo (hub social)**
    - **Barracón (gestión de escuadras)**
    - **Preparación de batalla**
    - **Mapa de batalla**
    - **Post-partida**

> Cada escena tiene su propio sistema de UI, lógica de flujo y referencia a sistemas compartidos.
> 

### 1.5 🌐 Networking

- **Solución de red:** Unity Netcode for GameObjects (con ECS wrapper donde sea necesario).
- **Topología:** Cliente-servidor con servidor dedicado.
- **Estado sincronizado:**
    - Posición y estado de héroes.
    - Posición, formaciones y acciones de escuadras.
    - Eventos de combate y habilidades.
- **Autoridad:** Cliente predice, servidor valida.
- **Interpolación:** Movimiento interpolado con buffers de posición para héroes y escuadras.

---

## 2. 🎮 Control del Jugador y Cámara

---

### 2.1 🎯 Control del Héroe

### 🎯 Descripción General:

El jugador controla directamente al **héroe** en tercera persona durante la batalla. El movimiento, ataques y uso de habilidades son de estilo **action-RPG táctico**, similar a *Conqueror’s Blade*.

### 🧩 Componentes Principales:

- `HeroControllerSystem` (SystemBase):
    - Sistema de movimiento basado en **EntityCommandBuffer** y **Input System**.
    - Controla desplazamiento (`WASD`), sprint (`LeftShift`), y bloqueo de movimiento si está aturdido o muerto.
    - Referencia el componente `HeroStats` para consultar la estamina, velocidad, etc.
- `HeroInputComponent` (IComponentData):
    - Contiene inputs actuales del frame: movimiento, ataque, habilidades, órdenes, etc.
- `HeroStatsComponent` (IComponentData):
    - Velocidad base, fuerza, estamina, cooldowns, vitalidad, etc.
- `HeroAnimationControllerAuthoring` (baker + animation state data):
    - Maneja las transiciones entre animaciones (Idle, Run, Attack, Stunned).
    - Usa parámetros de estado recibidos desde el `HeroControllerSystem`.
- `StaminaSystem`:
    - Gestiona el gasto y recuperación de estamina.
    - Valida si un input puede ejecutarse según el estado del héroe.

### ⚙️ Lógica de funcionamiento:

- El input se captura desde `Unity.InputSystem`.
- Se convierte en un `HeroInputComponent` al que los sistemas acceden.
- Las acciones (mover, atacar, usar habilidades) son evaluadas por distintos sistemas (`MovementSystem`, `SkillSystem`, `StaminaSystem`).
- Si el héroe muere, se desactiva el control local y se activa el modo cámara espectador (ver 2.2).

---

### 2.2 🎥 Cámara

### 📌 Normal:

- **Tipo:** Tercera persona con seguimiento.
- **Movimiento:**
    - Rotación libre (ejes X/Y).
    - Zoom con scroll limitado entre dos distancias fijas.
- **Colisión de cámara:** Evita que la cámara atraviese paredes u objetos grandes.
- **Opcional:** Tecla para modo táctica ligera (eleva el ángulo y aleja la cámara).

### 🧩 Componentes:

- `HeroCameraFollowSystem`:
    - Ajusta posición y rotación suavemente (Lerp).
    - Controla zoom, orientación y modo táctica.
- `CameraSettingsComponent`:
    - Datos: distancia, altura, sensibilidad, suavizado, límite de zoom.
- `CameraStateComponent`:
    - Flags: modoNormal / modoTáctico / modoEspectador.
- `CameraCollisionSystem`:
    - Detecta colisiones para ajustar la posición y evitar clipping.

### 🧟 Modo Espectador (al morir):

- Se activa cuando el héroe entra en estado `KO` o `Muerte`.
- Permite cambiar entre aliados vivos (`← / →` o `Tab`).
- Desactiva HUD activo y reemplaza por un modo espectador mínimo.
- Retorna al héroe al final del cooldown automáticamente.

---

### 2.3 🛡️ Control de Escuadras

### 📌 Descripción:

El jugador controla **una escuadra activa** a la vez. Puede darle órdenes y cambiar formaciones. Todo se hace mediante **hotkeys**, **UI de escuadra**, o **clics contextuales**.

### 🧩 Componentes Técnicos:

- `SquadControlSystem`:
    - Toma inputs y aplica órdenes a la escuadra activa.
    - Comunica con `FormationSystem`, `OrderSystem`, `SkillSystem`.
- `SquadInputComponent`:
    - Contiene flags: orden actual, tipo de orden, formación activa.
- `SquadStateComponent`:
    - Datos de la escuadra: posición, formación actual, cooldowns, estado de unidad (alerta, en combate, flanqueado, etc.).
- `FormationSystem`:
    - Ajusta la colocación de unidades dentro de la escuadra según la formación seleccionada.
- `SquadOrderSystem`:
    - Ejecuta las órdenes dadas por el jugador:
        - `C`: seguir al héroe
        - `X`: mantener posición
        - `V`: atacar
    - También se puede activar un **menú radial con ALT** o emitir órdenes con **click derecho en terreno** (raycast desde la cámara).

### 🧠 Formaciones Soportadas:

- Línea (`F1`)
- Dispersa (`F2`)
- Testudo (`F3`)
- Cuña (`F4`), según escuadra

Cada formación está representada en ECS como una `NativeArray<LocalPosition>` relativa al líder de escuadra.

### 🔄 **Nueva Funcionalidad: Cambio Cíclico de Formaciones**

**Doble clic en `X`**: Cambia automáticamente a la siguiente formación disponible en el array de formaciones del escuadrón.

🧩 **Lógica implementada:**

- **Primer clic en `X`**: Ejecuta orden `HoldPosition` (comportamiento original)
- **Doble clic rápido en `X`** (< 0.5 segundos): Cancela `HoldPosition` y cambia formación
- **Rotación cíclica**: Al llegar al último índice, regresa al primer índice del array

🔧 **Ejemplo de funcionamiento:**

```
Formaciones disponibles: [0: Line, 1: Testudo, 2: Wedge]
Estado inicial: Line (índice 0)
Doble clic X → Testudo (índice 1)
Doble clic X → Wedge (índice 2)  
Doble clic X → Line (índice 0) // Vuelve al inicio
```

⚙️ **Implementación técnica:**
- `SquadControlSystem` detecta doble clic mediante `Time.time` y threshold de 0.5s
- Busca índice actual en el array de formaciones del `SquadDataComponent`
- Calcula siguiente índice usando operador módulo: `(currentIndex + 1) % formations.Length`
- Actualiza `SquadInputComponent.desiredFormation` con nueva formación

### 🧩 UI de Escuadra Activa:

- Sistema basado en Unity UI (Canvas) que muestra:
    - Icono de la escuadra
    - Formación actual
    - Botones para habilidades (1, 2, 3...)
    - Indicadores visuales del estado (vida, cooldowns, etc.)
- Interactúa con `SquadSelectionSystem` para cambiar entre escuadras fuera de combate (en supply points).

### 🔄 Interacción entre sistemas:

- `HeroControllerSystem` ↔ `SquadControlSystem`:
    - El héroe da la orden, la escuadra reacciona según estado.
- `FormationSystem` ↔ `SquadAIController`:
    - Una vez recibida una orden, las unidades se reacomodan según la formación activa.
- `SkillSystem`:
    - Escuadra puede ejecutar habilidades activas propias (en base a cooldown y trigger manual).

---

## 3. 🧠 IA de Escuadras y Unidades

---

### 3.1 🧭 Sistema de Navegación (NavMesh)

📌 Descripción:

Las escuadras se mueven utilizando NavMesh con un agente maestro (pivot) que lidera la formación. Las unidades individuales se posicionan relativamente a ese pivot siguiendo una lógica de patrón de formación.

🧩 Componentes:

NavMeshAgentAuthoring (GameObject conversion)

SquadNavigationComponent (IComponentData)

targetPosition: destino global

isMoving: bool

formationOffset[]: offsets locales para cada unidad

🔧 Sistemas:

SquadNavigationSystem:

Calcula el path principal del pivot.

Distribuye posiciones a cada unidad de escuadra según formationOffset.

UnitFollowFormationSystem:

Mueve cada unidad hacia su LocalToWorld esperado.

Interpola o reubica para mantener la cohesión.

SquadObstacleAvoidanceSystem:

Detecta obstáculos en el camino y ajusta temporalmente la forma.

---

### 3.2 🧱 Comportamiento Reactivo en Formación

### 📌 Descripción:

Las escuadras **reaccionan dinámicamente** al entorno manteniendo su formación mientras maniobran. Adaptan posiciones en pasajes estrechos, evitan colisiones y mantienen la unidad visual.

### 🧩 Componentes:

- `EnvironmentAwarenessComponent`:
    - Define rango de detección y tipo de entorno (estrecho, abierto, escaleras, etc.).
- `FormationAdaptationSystem`:
    - Cambia dinámicamente la formación según:
        - Tamaño del terreno
        - Obstáculos (muros, enemigos)
        - Tipo de formación permitida
- `UnitSpacingSystem`:
    - Ajusta la separación entre unidades.
    - Evita solapamiento, especialmente en combate.

---

### 3.3 👥 IA de Escuadra Grupal vs Individual

### 📌 Descripción:

El comportamiento es **grupal**, pero con **unidad mínima de decisión** en cada soldado (solo para microacciones: evasión, rotación, targeting).

### 🧩 Componentes:

- `SquadAIComponent`:
    - `tacticalIntent`: enum (Idle, Atacando, Reagrupando, Defendiendo, Retirada)
    - `groupTarget`: entidad enemiga prioritaria sugerida para todas las unidades
    - `isInCombat`: bool
- `UnitCombatComponent`:
    - Posición relativa
    - `attackCooldown`
    - Estado local (cubierto, flanqueado, suprimido)



* tacticalIntent representa la intención táctica asignada por el jugador o IA. No refleja el estado actual de ejecución, que es gestionado por SquadStateComponent.
* groupTarget es sugerido por el SquadAISystem con base en la táctica (tacticalIntent), pero las unidades individuales pueden sobreescribirlo localmente si tienen mejor opción en rango o línea de visión, evaluado por UnitTargetingSystem.

📌 ¿Qué es `tacticalIntent`?

`SquadAIComponent.tacticalIntent` es un campo enum que representa la **intención táctica activa** del escuadrón. Esta intención es establecida por el jugador (o IA) mediante una orden directa (por ejemplo, presionar `C`, `V`, o `X`), y puede tomar los siguientes valores:

```csharp
enum TacticalIntent {
  Idle,
  Atacando,
  Reagrupando,
  Defendiendo,
  Retirada
}

```

Este valor **no refleja el estado actual físico o lógico de la escuadra**, sino su meta deseada. La ejecución de esta intención depende del entorno, estados internos y lógica FSM.

---

⚙️ Flujo y sincronización entre sistemas

| Sistema | Acción |
| --- | --- |
| `SquadOrderSystem` | Cambia el valor de `tacticalIntent` en base a la orden del jugador. |
| `SquadAISystem` | Lee `tacticalIntent` y determina qué acciones tomar: movimiento, targeting, cambio de formación, etc. |
| `SquadFSMSystem` | Intenta alinear el `SquadStateComponent.currentState` con el `tacticalIntent`, si las condiciones del entorno lo permiten. |
| `SquadNavigationSystem` | Calcula caminos y rutas en función del `tacticalIntent`, por ejemplo mover hacia un enemigo si la intención es `Atacando`. |
| `SquadCombatSystem` | Se activa en consecuencia si `tacticalIntent == Atacando` y hay enemigos en rango. |

---

🔄 Transición Táctica → Estado FSM

| `tacticalIntent` | Estado FSM esperado en `SquadStateComponent.currentState` |
| --- | --- |
| `Idle` | `Idle` |
| `Atacando` | `Moving → InCombat` |
| `Reagrupando` | `Moving → HoldingPosition` |
| `Defendiendo` | `HoldingPosition` |
| `Retirada` | `Retreating → KO` (si se completa) |

> El SquadFSMSystem es responsable de esta transición. Si la escuadra no puede alcanzar la intención (ej: sin camino libre), permanece en el estado actual hasta que se reevalúe.
> 

---

🧠 Ejemplo de flujo completo

1. El jugador presiona `V` (Atacar).
2. `SquadOrderSystem` asigna `tacticalIntent = Atacando`.
3. `SquadAISystem` identifica un objetivo cercano y asigna un path.
4. `SquadFSMSystem` cambia `currentState` de `Idle` a `Moving`, y luego a `InCombat` al alcanzar al enemigo.
5. Si el enemigo muere y no hay nuevos targets, `SquadFSMSystem` revierte a `Idle`.

---

✅ Ventajas de esta separación

- Permite modularidad: distintos sistemas pueden leer la intención sin interferencia directa.
- Evita desincronización: el FSM refleja **estado observable**, no deseo interno.
- A futuro: facilita implementación de intenciones complejas, como flanqueo, distracción o formación dinámica.

### 🧩 Sistemas:

- `SquadAISystem`:
    - Lógica de toma de decisiones grupal
    - Inicia combate si enemigo dentro de rango
    - Cambia formación si está siendo flanqueado
- `UnitTargetingSystem`:
    - Asigna enemigo cercano a cada unidad
    - Maneja “sobretargeting” (más de 3 soldados contra 1 objetivo = redistribución)
Notas:
* Este sistema se encarga de asignar blancos individuales a cada unidad, partiendo de groupTarget cuando sea válido, o buscando uno propio si está fuera de rango/visión. Si varias unidades se sobrecargan contra un mismo blanco, se redistribuye el targeting automáticamente (“sobretargeting”).
- `UnitAttackSystem`:
    - Verifica cooldowns
    - Ejecuta animaciones de ataque si tiene target
    - Usa `criticalChance` del arma para aplicar golpes críticos de 1.5x
---
⚠️ Nota: attackCooldown aplica solo a ataques básicos individuales. No interfiere ni comparte lógica con cooldowns[] en SquadSkillComponent, que gestiona habilidades activas del escuadrón.
---

### 3.4 🧠 Coordinación de Habilidades de Escuadra

### 📌 Descripción:

Habilidades de escuadra se ejecutan de forma **coordinada y sincronizada**, basadas en señales del jugador (hotkey) y condiciones tácticas (posición, formación, enemigos).

**Activación:** Las habilidades de escuadra se activan mediante hotkeys directos `1`, `2`, `3` para activación rápida, y también son accesibles desde el **menú radial** (`ALT`).

### 🧩 Componentes:

- `SquadSkillComponent`:
    - `cooldowns[]`
    - `triggerFlags[]` (true cuando se presiona el botón)
    - `isExecuting`: bool
- `SkillExecutionSystem`:
    - Verifica condiciones tácticas (formación, rango, vista de enemigo).
    - Activa la animación grupal (vía trigger ECS → Animator).
    - Envía evento de red si es multijugador.
- `FormationConstraintSystem`:
    - Algunas habilidades solo se ejecutan en formaciones concretas (ej. Muro de Escudos).
    - Si no está en la formación correcta, no puede activarse.

 Los cooldowns en este componente aplican solo a habilidades especiales del escuadrón. No afectan ni dependen del cooldown de ataque básico (attackCooldown) de las unidades individuales.
---

### 3.5 🔁 FSM para Estados de Escuadras y Transición a Retirada

### 📌 Descripción:

Cada escuadra tiene un sistema FSM (Finite State Machine) que rige su **estado actual** y transiciones. Esto es clave para el combate, la retirada, reubicación y respuesta táctica.

### 🧩 Estados:

- `Idle`: sin orden activa
- `Moving`: reposicionándose
- `InCombat`: cuerpo a cuerpo o ataque a distancia
- `HoldingPosition`: estática y en formación
- `Retreating`: en retirada hacia punto seguro
- `KO`: destruida (si pierde todas sus unidades)

### 🧩 Componentes:

- `SquadStateComponent`:
    - `currentState`: enum
    - `timer`: duración del estado
    - `transitionTo`: próximo estado deseado
- `SquadFSMSystem`:
    - Controla transiciones lógicas:
        - Si el héroe muere → `HoldingPosition`
        - Si recibe daño masivo → `Retreating`
        - Si está a salvo → `Idle`
- `RetreatLogicSystem`:
    - Calcula ruta de retirada (alejándose de enemigos).
    - Emite evento para desaparición si llega a zona segura.

## 🏗️ 4. Construcción de Mapas y Escenarios

---

### 4.1 🛠️ Herramientas para Creación de Mapas

### 📌 Descripción:

El mapa MVP será creado **a mano en Unity** utilizando herramientas internas de terreno y modelado modular (prefabs). Elementos como murallas, puertas, torres y obstáculos se integran como objetos con colliders y tags específicos.

### 🧩 Herramientas / Sistemas:

- **Unity Terrain Tools** para topografía básica.
- Prefabs de murallas, edificios, escaleras y rampas.
- Sistema de etiquetas y layers para detección de interacción (`LayerMask` personalizado: Terreno, Obstáculo, SupplyPoint, Capturable, etc.).
- Diseño modular en `Grid Snapping` para facilitar pruebas.

### 🧩 Código:

- `MapAuthoringComponent` (GameObject → Entity):
    - `zoneType`: enum (Default, Capturable, Supply, Respawn)
    - `isInteractable`: bool
- `TerrainTagSystem`:
    - Marca dinámicamente elementos importantes durante carga de mapa.
    - Se integra con pathfinding y FSM de escuadras para lógica de evasión o captura.

---

### 4.2 🚪 Implementación de Elementos Destructibles

### 📌 Descripción:

Puertas y obstáculos específicos pueden **ser destruidos** por escuadras o maquinaria de asedio. Utilizan animaciones sincronizadas (no físicas) para representar destrucción.

### 🧩 Componentes:

- `DestructibleComponent`:
    - `hp`: puntos de vida
    - `isDestructible`: bool
    - `onDestroyedAnimation`: referencia a animación
- `SiegeInteractComponent`:
    - `type`: enum (Ariete, Torre, etc.)
    - `interactableBySquad`: bool
    - `progress`: float (progreso de empuje / interacción)

### 🔧 Sistemas:

- `DamageToStructureSystem`:
    - Aplicación de daño por habilidades o ataques pesados.
    - Destruye estructura y lanza animación si `hp <= 0`.
- `SiegePushSystem`:
    - Escuadras designadas "empujan" arietes/torres cuando se les ordena.
    - Se mueve el objeto por ruta spline o puntos clave hasta destino.

---

### 4.3 📦 Sistema de Zonas y Triggers Físicos

### 📌 Descripción:

El mapa está lleno de **zonas funcionales**, cada una identificada por **colliders con triggers**, etiquetas y lógica conectada al gameplay:

- Puntos de captura
- Zonas de suministro
- Áreas de visibilidad extendida (torres)
- Spawn points

### 🧩 Componentes:

- `ZoneTriggerComponent`:
    - `zoneType`: enum (CapturePoint, SupplyPoint, VisionArea, Spawn)
    - `teamOwner`: int (0 = neutral, 1 = azul, 2 = rojo)
    - `radius`: float
- `ZoneInteractionSystem`:
    - Detecta si entidades (héroes o escuadras) entran en rango.
    - Lanza lógica según el tipo de zona:
        - `CapturePoint`: inicia barra de captura.
        - `SupplyPoint`: permite curar o cambiar escuadra si no está en disputa.
        - `Spawn`: determina ubicación inicial del héroe.
- `CapturePointProgressComponent`:
    - `captureProgress`: float
    - `isContested`: bool
    - `heroesInZone`: buffer de Entity
- `SupplyInteractionSystem`:
    - Verifica si el punto no está en disputa.
    - Permite al jugador reconfigurar su escuadra activa (si tiene otras vivas).

### 🧩 Visual:

- Círculos en el terreno para visualizar radio de acción.
- Cambian color según propiedad (neutral, aliado, enemigo).

---

### 4.4 🗺️ Configuración del Mapa MVP y Puntos Claves

### 📌 Descripción:

El MVP incluye **un único mapa simétrico asimétrico**, con elementos específicos:

- 2 puntos de spawn (por bando)
- N supply points por lado (cantidad definida por el mapa, puede ser asimétrica)
- 1 bandera principal de base
- 1 bandera de captura intermedia
- Obstáculos estratégicos, puntos de visión, zonas estrechas

### 🧩 Estructura:

```
plaintext
CopiarEditar
AZUL SPAWN     --[SUPPLY]--        [CAPTURE POINT A]        --[SUPPLY]--    ROJO SPAWN
                                ↘               ↙
                             [BASE FLAG]

```

### 📦 Componentes del mapa:

- `CaptureFlagA`: zona intermedia
- `BaseFlag`: punto de victoria (solo desbloqueable si A fue capturado)
- `SupplyPoints` x4: zonas funcionales
- `SpawnPointComponent`:
    - `spawnTeam`: int
    - `position`: Vector3
    - `isSelected`: bool

### 📌 Código:

- `MapSceneManager` (MonoBehaviour + Bootstrap):
    - Coloca zonas como entidades ECS al cargar la escena.
    - Inicializa estado de puntos, propietarios, colores, HUD de mapa.
    - Enlaza lógica de transición entre escena de preparación → combate.
    
    ---
    
    ### 4.4.1 🏳️ Puntos de Captura
    
    ### 📌 Descripción:
    
    Los puntos de captura son zonas estratégicas que deben ser conquistadas por el bando atacante para avanzar y ganar la partida. Su funcionamiento es diferente al de los supply points:
    
    - **Propiedad inicial:** Todos los puntos de captura pertenecen al bando defensor al inicio de la partida.
    - **Captura irreversible:** Una vez que un punto de captura es conquistado por el bando atacante, no puede ser recuperado por el bando defensor durante esa partida.
    - **Desbloqueo secuencial:** Algunos puntos de captura están bloqueados al inicio y solo se pueden capturar si se ha conquistado previamente el punto anterior (precondición). Un punto bloqueado no puede ser capturado hasta que se desbloquee.
    - **Punto de base:** Si el atacante conquista el punto de base, la partida termina inmediatamente con la victoria del bando atacante.
    - **Extensión de tiempo:** Capturar un punto de captura normal (no base) añade tiempo al timer de la partida. El máximo absoluto es 30 minutos (`battleDurationSeconds = 1800`). Esta mecánica previene estancamientos donde el bando defensor domina sin presión temporal.
    - **Progresión:** Al capturar un punto previo, se desbloquea el siguiente punto de captura en la secuencia, permitiendo el avance del equipo atacante.
    - **Diferencia con supply points:** A diferencia de los supply points, los puntos de captura no pueden cambiar de dueño varias veces; su captura es definitiva para el resto de la partida.
    
    ### 🧩 Componentes:
    
    - `CaptureZoneComponent`:
        - `captureProgress`: float (0 a 100)
        - `isContested`: bool
        - `teamOwner`: int (0: neutral, 1/2: equipos)
        - `isBase`: bool
        - `isLocked`: bool (indica si el punto está bloqueado y no puede capturarse)
        - `unlockCondition`: referencia al punto previo que debe ser capturado
    - `CaptureZoneTriggerSystem`:
        - Detecta héroes dentro del radio
        - Si hay atacantes y **ningún defensor**: avanza `captureProgress`
        - Si hay defensores (con o sin atacantes): **pausa** la captura (`isContested = true`), el progreso se mantiene
        - Si solo hay defensores (sin atacantes): el progreso **decrementa** gradualmente
        - Si solo hay atacantes de nuevo: la captura **continúa desde donde quedó**
        - Al completarse la captura (progress >= 100): cambia `teamOwner`, desbloquea siguiente punto si aplica
        - Si es punto de base y es capturado: termina la partida inmediatamente
    - `CaptureProgressUISystem`:
        - Sincroniza HUD de progreso
        - Envía eventos de captura completada
    
    ### 🧩 Interacción:
    
    - El HUD recibe cambios de color, íconos o tiempo.
    - El resultado de la captura puede desbloquear zonas (ej.: Base se desbloquea tras capturar A/B).
    - Los puntos de captura no pueden ser recuperados por el bando defensor una vez perdidos.
    - Los supply points pueden cambiar de dueño varias veces durante la partida, pero los puntos de captura no.
    
    ---
    
    ### 4.4.2 🩺 Supply Points
    
    ### 📌 Descripción:
    
    Zonas del mapa que permiten curar al héroe/squad y cambiar de escuadra si no están en disputa. Su cantidad y posición la define cada mapa (puede ser asimétrica entre bandos). A diferencia de los puntos de captura, pueden cambiar de dueño múltiples veces durante la partida.
    
    ### 🧩 Componentes:
    
    - `SupplyPointComponent`:
        - `teamOwner`: int
        - `isContested`: bool
        - `isAvailable`: bool (determinado por presencia de enemigos)
    - `SupplyInteractionComponent`:
        - Detecta entrada del jugador
        - Muestra UI de cambio o activa curación
        - Envío de acción: “Retirar escuadra”, “Traer escuadra 2”
    - `SquadSwapSystem`:
        - Verifica si se puede hacer el cambio
        - Elimina la escuadra actual, instancia la nueva si está disponible
        - Lanza `SquadChangeEvent` para sincronizar el nuevo estado
    
    ### 🧩 Curación:
    
    - `HealingZoneSystem`:
        - Revisa que haya permanencia del héroe/squad dentro del collider por X tiempo
        - Incrementa vida por tick
        - Aplica solo si `isContested = false`
    
    ---
    
    ### 4.4.3 🧭 Spawn Points
    
    ### 📌 Descripción:
    
    Zonas de aparición de héroes y escuadras. Definidas por el equipo en la fase de preparación.
    
    ### 🧩 Componentes:
    
    - `SpawnPointComponent`:
        - `teamID`: int
        - `spawnID`: int
        - `position`: `float3`
    - `RespawnSystem`:
        - Cuando un héroe muere, se activa un cooldown.
        - Al terminar el cooldown, reaparece en su punto designado.
    - `SpawnSelectionSystem` (en fase previa al combate):
        - Permite al jugador seleccionar el spawn inicial desde UI

## ⚔️ 5. Sistema de Combate y Daño

---

### 5.1 🧍 Combate del Héroe (Colliders y Animaciones)

📌 **Descripción:**

El héroe combate en tercera persona mediante **ataques animados y habilidades**, ejecutados con colliders sincronizados con la animación. Cada **clase de arma** (espada + escudo, lanza, etc.) tiene su propio set de animaciones y habilidades.

Las acciones ofensivas consumen **stamina**, tienen **cooldown**, y son definidas desde el **loadout**.

🧩 **Componentes clave:**

```csharp
HeroCombatComponent (IComponentData)
- activeWeapon: Entity
- currentStamina: float
- attackCooldown: float
- abilityPrimary: Entity (Q)
- abilitySecondary: Entity (E)
- ultimateAbility: Entity (R)
- isAttacking: bool
```

```csharp
HeroStaminaSystem
- Reduce stamina al atacar, correr, esquivar, usar habilidad
- Recupera stamina fuera de combate
```

```csharp
HeroAttackSystem
- Escucha input
- Valida cooldown y stamina
- Reproduce animación correcta
- Activa collider de arma mediante Animation Events
- Genera golpes críticos con multiplicador 1.5x según `criticalChance` del arma
```

```csharp
WeaponColliderAuthoring (MonoBehaviour → Baker)
- Maneja habilitación/deshabilitación de PhysicsShape
- Se activa desde la animación (frame exacto)
```

🔁 **Interacción:**

- Requiere sistema de input (HeroInputSystem)
- Sincroniza con `DamageSystem` para aplicar daño
- Enlazado al HUD (barra de stamina, cooldowns de habilidades)
- Coordina con el sistema de animaciones por clase (`HeroAnimationStateSystem`)

---

### 5.2 🪖 Combate de Escuadras (Detección y Ataques Sincronizados)

📌 **Descripción:**

Las escuadras atacan como **entidad colectiva**. Las unidades detectan enemigos en su rango de ataque, y ejecutan ataques por intervalos. El daño se calcula por **unidad**, pero la ejecución es **coordinada desde el squad**.

🧩 **Componentes clave:**

```csharp
SquadCombatComponent (IComponentData)
- attackRange: float
- attackInterval: float
- attackTimer: float
- targetEntities: DynamicBuffer<Entity> // lista de enemigos dentro de rango, usada para análisis de amenaza y ataque sincronizado.
```

```csharp
UnitWeaponComponent (IComponentData)
- damageProfile: Entity (referencia a ScriptableObject con daño, tipo, penetración)
- criticalChance: float (probabilidad de crítico para héroe y unidades)
```

```csharp
SquadAttackSystem
- Escanea enemigos dentro del rango
- Selecciona objetivos por unidad
- Ejecuta daño cada `attackInterval`
- Sincroniza animaciones por unidad (opcional)
```

📌 **Sincronización y simplificación MVP:**

- El MVP usará **ataques por intervalo y animaciones genéricas**.
- A futuro, se puede migrar a colisiones reales por unidad.

---

### 5.3 ⚔️ Tipos de Daño y Resistencias

📌 **Descripción:**

Todo daño en el juego es de tipo:

- `Blunt` (Contundente)
- `Slashing` (Cortante)
- `Piercing` (Perforante)

Cada unidad tiene defensas diferenciadas por tipo y los ataques tienen **penetraciones específicas** que ignoran parte de esa defensa.

🧩 **Componentes:**

```csharp

enum DamageType { Blunt, Slashing, Piercing }

enum DamageCategory { Normal, Critical, Ability }

`DamageCategory` define la representación visual del daño. El valor
`Critical` aplica un multiplicador de **1.5x** y se muestra con un popup
mayor y color distinto. `Ability` corresponde a efectos como sangrado y
usa su propio color.

DamageProfile (ScriptableObject)
- baseDamage: float
- damageType: DamageType
- penetration: float

```

```csharp

DefenseComponent (IComponentData)
- bluntDefense: float
- slashDefense: float
- pierceDefense: float

```

```csharp

PenetrationComponent (IComponentData)
- bluntPenetration: float
- slashPenetration: float
- piercePenetration: float

```

🔁 **Interacción:**

- Leídos por `DamageCalculationSystem` cuando un ataque impacta.
- El tipo de daño determina qué defensa y qué penetración se aplican.

---
### 5.4 🧮 Cálculo de Daño y Penetración (Lógica en C#)

El sistema de daño combina múltiples factores para determinar el daño final aplicado a una unidad. Estos factores incluyen el tipo de daño, la armadura del objetivo, la penetración del atacante, y condiciones contextuales como flanqueo o desorganización.

---

**📌 Fuentes de penetración**

- **DamageProfile.penetration**: Valor base definido por el tipo de ataque o habilidad. Es estático y siempre presente.
- **PenetrationComponent**: Define valores específicos por tipo (bluntPenetration, slashPenetration, piercePenetration). Es dinámico y refleja buffs, perks o efectos temporales.

⚠️ El sistema combina ambas fuentes si están disponibles para obtener la penetración efectiva.

---

**🛡️ Tabla de efectividad: Tipo de Daño vs Tipo de Armadura**

| DamageType | LightArmor | MediumArmor | HeavyArmor | Shielded |
| --- | --- | --- | --- | --- |
| Slash | ✔️ Alta | ⚠️ Media | ❌ Baja | ❌ Baja |
| Pierce | ⚠️ Media | ✔️ Alta | ⚠️ Media | ❌ Baja |
| Blunt | ❌ Baja | ⚠️ Media | ✔️ Alta | ⚠️ Media |

El daño base se multiplica según la combinación del tipo de daño y el tipo de armadura del objetivo.

---

**🎯 Críticos contextuales**

Un ataque se considera crítico si:

1. El atacante impacta desde un **flanco o retaguardia**.
2. El objetivo está **fuera de formación** (`UnitFormationState = Dispersed`).

No se usa RNG ni categoría marcada. Se evalúa la situación tácticamente:

```csharp
bool IsCriticalHit(Entity attacker, Entity target)
{
    bool flanking = IsOutsideFrontCone(attacker, target);
    bool disrupted = target.HasComponent<UnitFormationStateComponent>() &&
                     target.GetComponent<UnitFormationStateComponent>().state == Dispersed;

    return flanking || disrupted;
}

```

---

**🧮 Lógica de cálculo completa**

---

### `CalculateFinalDamage` – Solo cálculo numérico

```csharp
float GetEffectivePenetration(DamageType type, DamageProfile profile, Entity attacker)
{
    float basePen = profile.GetPenetrationFor(type);
    float bonusPen = 0f;

    if (HasComponent<PenetrationComponent>(attacker))
        bonusPen = GetComponent<PenetrationComponent>(attacker).GetBonusFor(type);

    return basePen + bonusPen;
}

float CalculateFinalDamage(DamageProfile profile, Entity attacker, Entity target)
{
    DamageType type = profile.damageType;
    ArmorType armor = target.GetComponent<ArmorComponent>().armorType;

    float baseDamage = profile.baseDamage;
    float armorMultiplier = DamageArmorMultiplier(type, armor);
    float adjustedDamage = baseDamage * armorMultiplier;

    float penetration = GetEffectivePenetration(type, profile, attacker);
    float defense = target.GetComponent<DefenseComponent>().value;

    float mitigatedDefense = Mathf.Max(0, defense - penetration);
    float finalDamage = Mathf.Max(0, adjustedDamage - mitigatedDefense);

    if (IsCriticalHit(attacker, target))
        finalDamage *= 1.5f;

    return finalDamage;
}

```

---

### `ApplyDamageAndEffects` – Daño real + efectos secundarios

```csharp
void ApplyDamageAndEffects(DamageProfile profile, Entity attacker, Entity target)
{
    float finalDamage = CalculateFinalDamage(profile, attacker, target);

    // Aplicar daño a la salud
    var health = target.GetComponent<HealthComponent>();
    health.currentHealth = Mathf.Max(0, health.currentHealth - finalDamage);
    SetComponent(target, health);

    // Aplicar efectos secundarios definidos en el perfil
    foreach (var effect in profile.statusEffects)
    {
        PendingStatusEffect pending = new PendingStatusEffect
        {
            type = effect.type,                 // Bleed, Burn, etc.
            duration = effect.duration,
            magnitude = effect.magnitude,
            source = attacker,
            refreshPolicy = effect.refreshPolicy
        };

        AddBuffer<PendingStatusEffectsBuffer>(target).Add(pending);
    }

}

```

### 🔥 Efectos secundarios (Bleed, Burn)

Ciertos ataques pueden aplicar efectos secundarios definidos en el `DamageProfile`:

- **Bleed**: aplica daño por segundo durante varios ticks.
- **Burn**: inflige daño prolongado que se intensifica si el objetivo permanece en área ardiente.

Estos efectos se definen como `StatusEffect[]` en el perfil del daño y se procesan tras aplicar el daño principal.

> **Nota sobre Stagger:** El stagger NO se activa por umbral de daño. Solo existe como consecuencia de un guard break (ruptura de bloqueo): 1s para el héroe, 2s para unidades. Ver sección 5.6 para detalles.

---

### ⚙️ Modularización por pasos

1. **Recolección de datos**: DamageProfile, stats, buffs, armor, estado.
2. **Resolución de penetración**: base + modificadores.
3. **Resolución de defensa**: `mitigatedDefense = defense - penetration`.
4. **Aplicación de modificadores**: tipo vs tipo, crítico, flanco.
5. **Cálculo del daño neto**: `adjustedDamage - mitigatedDefense`.
6. **Aplicación**: se reduce `HealthComponent.currentHealth`.
7. **Evaluación de efectos secundarios**: se aplican como `StatusEffect`.
8. **Reacción del objetivo**: muerte, ruptura de formación.

---

### 🧮 Ejemplo paso a paso

**Datos:**

- Daño base: `40`
- Tipo de daño: `Piercing`
- Armadura: `MediumArmor`
- Defensa: `25`
- Penetración base: `10`
- Penetración adicional: `5`
- Crítico: **Sí** (flanqueo)

**Cálculo:**

- Modificador tipo vs tipo: `1.0`
- `adjustedDamage = 40 * 1.0 = 40`
- `penetration = 10 + 5 = 15`
- `mitigatedDefense = max(0, 25 - 15) = 10`
- `finalDamage = max(0, 40 - 10) = 30`
- `finalDamage *= 1.5 (crítico) = 45`

**Resultado:**

- Daño aplicado: `45`
- Efecto adicional: `Bleed (5 DPS durante 4s)`

**🧩 Sistemas involucrados**

- **DamageCalculationSystem**
    - Lee: `DamageProfile`, `DefenseComponent`, `ArmorComponent`, `PenetrationComponent`, `UnitFormationStateComponent`
    - Calcula: tipo vs tipo, penetración efectiva, daño mitigado, y críticos contextuales
    - Aplica el daño a `HealthComponent`
- **HealthComponent** (IComponentData)
    - `maxHealth: float`
    - `currentHealth: float`

Si `currentHealth <= 0`, se notifica al `DeathSystem`, que puede:

- Activar animaciones de muerte
- Retirar la unidad del mapa
- Liberar su slot de formación

---

Este diseño separa claramente los aspectos estáticos (perfil del arma) de los dinámicos (contexto del combate), permite decisiones tácticas significativas, y mantiene un flujo lógico unificado de cálculo de daño.
---

### 5.5 ⏱️ Gestión de Cooldowns y Tiempos de Habilidad

📌 **Descripción:**

Cada habilidad equipada tiene su **cooldown individual**, que se reduce con el tiempo. Si no hay stamina suficiente, no se puede usar.

🧩 **Componentes:**

```csharp
CooldownComponent (IComponentData)
- cooldownDuration: float
- currentCooldown: float
- isReady: bool
```

```csharp
HeroAbilityComponent (IComponentData)
- referencia al ScriptableObject de habilidad activa
- staminaCost: float
- cooldownComponent: Entity
```

```csharp
CooldownSystem
- Reduce `currentCooldown` cada frame
- Marca `isReady = true` cuando cooldown llega a 0
```

🔁 **Integración:**

- `HeroInputSystem` detecta el input y consulta `HeroAbilityComponent`
- Si hay stamina y cooldown listo, se activa la habilidad
- El HUD debe mostrar:
    - Icono gris → en cooldown
    - Números → segundos restantes
    - Animación de “cooldown completado”

---
### 5.6 🛡️ Sistema de Bloqueo y Mitigación por Colisión

📌 **Descripción:**

El sistema de bloqueo permite anular o reducir el daño entrante, tanto en el héroe (bloqueo activo) como en unidades defensivas (bloqueo pasivo). Se activa por colisión directa del golpe con el **hitbox del escudo o arma**, evaluando si el impacto fue **frontal** y si el **bloqueo está activo o disponible**.

---

🧍‍♂️ **Bloqueo del Héroe (Activo)**

- **Input:** se mantiene el botón derecho del mouse (`RMB`) para activar el bloqueo.
- **Movimiento:** al bloquear, el héroe solo puede caminar a velocidad reducida.
- **Hitbox:** cada arma tiene su propio `GameObject` con collider físico habilitado en `blockingMode`.
- **Validación:** si un ataque colisiona con el collider de bloqueo antes que con el `HeroCollider`, se considera un **bloqueo exitoso**.
- **Mitigación:** se consume estamina proporcional al daño:
  - `Cortante`: 1:1
  - `Contundente`: x2
  - `Perforante`: x0.7
- **Ruptura:** si la estamina cae a 0 al bloquear → estado `Stagger` por 1s (bloquea input, animación de retroceso).
- **Fallos:** si no hay estamina suficiente → bloqueo no se aplica, recibe daño completo.
- **Ángulo de bloqueo:** determinado por el collider físico del arma/escudo, no por un ángulo numérico.

---

🛡️ **Bloqueo de Unidades (Pasivo)**

- **Elegibilidad:** solo escuadras con escudo tienen acceso a este sistema.
- **Stats:** se usa el campo `bloqueo` en `UnitStatsComponent` como resistencia acumulada.
- **Validación:** si el ataque colisiona con el `EscudoCollider`, se considera un **bloqueo válido**.
- **Reducción de `bloqueo`:** se resta el daño recibido al valor actual de `bloqueo`. Si llega a 0:
  - Se activa estado `StaggerUnit` por `2s - recuperaciónBloqueo`
- **Recuperación de bloqueo:** atributo oculto que reduce la duración del stagger (escala con perks o mejoras).
- **Regeneración:** el valor de `bloqueo` se recupera pasivamente con el tiempo.
- **Formaciones:** bonus de bloqueo se aplican según la formación activa (`Testudo`, `Muro de Escudos`, etc).

---

🧩 **Componentes nuevos**

```csharp
public struct BlockingComponent : IComponentData {
    public bool isBlocking;
    public Entity weaponCollider; // referencia al escudo o arma que bloquea
    public float staminaDrainMultiplier;
}

public struct StaggerComponent : IComponentData {
    public float duration;
    public float timer;
    public bool isStaggered;
}

public struct BlockValueComponent : IComponentData {
    public float currentBlock;
    public float maxBlock;
    public float regenRate;
    public float staggerDuration; // base 2s, modificado por perks
}
```
🧠  **Sistemas involucrados**

- HeroBlockSystem: activa bloqueo si input detectado y suficiente stamina.
- UnitBlockSystem: aplica lógica de reducción pasiva y rotación defensiva.
- StaggerSystem: bloquea input o AI si una entidad entra en estado de ruptura.
- DamageCalculationSystem: consulta bloqueo antes de aplicar daño, ajusta el valor si fue mitigado.

---
### 5.7 🎯 Jerarquía de Targeting (Unidad vs Escuadra)

El sistema de targeting está dividido en dos niveles de decisión: **nivel grupal** y **nivel individual**, para permitir tácticas coordinadas sin limitar la autonomía de cada unidad.

**Componentes involucrados**

- `SquadAIComponent.groupTarget`: entidad enemiga prioritaria sugerida por el sistema de IA grupal (`SquadAISystem`).
- `SquadCombatComponent.targetEntities`: lista dinámica de enemigos cercanos, para análisis de amenaza.
- `UnitCombatComponent.target`: blanco actual de cada unidad, determinado por lógica local.
  
**Flujo de decisión**

1. `SquadAISystem` asigna un `groupTarget` basado en el `tacticalIntent` del escuadrón.
2. `UnitTargetingSystem` evalúa para cada unidad si el `groupTarget` es visible y está dentro de su alcance efectivo.
3. Si el `groupTarget` no es válido, la unidad selecciona un blanco propio desde `targetEntities`, priorizando distancia y tipo.
4. Si varias unidades coinciden en el mismo blanco, se aplica una redistribución (sobretargeting mitigation) para diversificar los objetivos.
5. Si la unidad está en cooldown, desorganizada o sin línea de visión, no toma acción ofensiva hasta reevaluación del targeting.

**Reglas adicionales**

- Las unidades no pueden atacar blancos fuera de su visión ni ignorar `tacticalIntent`.
- La reasignación de targets es reactiva ante cambios de visibilidad, muerte del blanco o entrada de nuevas amenazas.

> ⚠️ Nota: Este sistema permite una coordinación efectiva sin requerir micromanagement completo del jugador, manteniendo el foco en decisiones tácticas de alto nivel.

---

### 5.8 🧩 Lógica de Formaciones y Jerarquía de Sistemas

El sistema de formaciones se estructura en torno a dos representaciones principales:

- `formationPattern: Vector3[]` (en `FormationComponent`): patrón ideal de slots relativos. Es la **fuente de verdad estructural** y no cambia dinámicamente.
- `formationOffset[]` (en `SquadNavigationComponent`): offsets aplicados para cada unidad, adaptados según terreno u obstáculos.

📌 **Jerarquía de responsabilidad**

| Sistema | Función | Modifica `formationPattern` | Modifica `formationOffset[]` |
|--------|---------|-----------------------------|-------------------------------|
| `FormationSystem` | Asigna formaciones y genera offsets base. | ✔ Sí | ✔ Sí |
| `FormationAdaptationSystem` | Ajusta offsets ante colisiones u obstáculos. | ✖ No | ✔ Sí |
| `UnitFollowFormationSystem` | Mueve unidades hacia sus offsets asignados. | ✖ No | ✖ No |
| `FormationConstraintSystem` | Verifica si la formación está incompleta o rota. | ✖ No | ✖ No |

> ⚠️ Regla: `formationPattern` solo puede ser modificado por `FormationSystem`. Los demás sistemas operan únicamente sobre los offsets instanciados.

🔄 **Flujo de datos**

1. El jugador u orden externa cambia la formación (`FormationSystem`).
2. Se establece un nuevo `formationPattern` y se recalculan los `formationOffset[]`.
3. Si el terreno o entorno interfiere, `FormationAdaptationSystem` ajusta los offsets temporalmente.
4. `UnitFollowFormationSystem` dirige a las unidades hacia sus offsets activos.
5. Si la formación no puede mantenerse, `FormationConstraintSystem` puede notificar ruptura.

🧠 **Concepto clave**

```plaintext
formationPattern = Lo ideal
formationOffset[] = Lo posible
formación ejecutada = Lo real
```
📘 **Notas adicionales**

- El patrón base permanece constante hasta un nuevo cambio de formación.
- La adaptación no sobreescribe el patrón original.
- El sistema puede volver a recalcular los offsets en cualquier momento a partir del patrón si se elimina una adaptación temporal.

---
### 5.9 🐎 Masa, Formación y Dinámica de Carga

El sistema de cargas se basa en la **interacción de masas entre escuadras**, afectadas por la **formación activa**, la **velocidad de impacto**, y el **tipo de unidad**. Esta sección define la lógica unificada para calcular la masa efectiva y resolver interacciones entre formaciones en situaciones de carga.

---

📌 **1. Masa efectiva de escuadra**

La **masa efectiva** se calcula en runtime con base en las unidades activas y su formación actual:

```csharp
float CalcularMasaEfectivaEscuadra(Entity squad)
{
    float masaBase = 0f;
    var unidades = GetBuffer<SquadUnitElement>(squad);

    foreach (var unidad in unidades)
    {
        masaBase += GetComponent<UnitStatsComponent>(unidad.entity).masa;
    }

    float formacionMultiplicador = GetFormacionMultiplicador(squad); // e.g., x2.0 para Testudo
    return masaBase * formacionMultiplicador;
}

```

- **`UnitStatsComponent.masa`**: valor base por unidad.
- **Multiplicador de formación**:
    - `Dispersa`: x0.5
    - `Línea`: x1.0
    - `Cuña`: x1.3
    - `Muro de escudos`: x1.5
    - `Schiltron`: x1.5
    - `Testudo`: x2.0

---

### ⚔️ 2. Lógica de resolución de carga

Cuando una escuadra realiza una carga contra otra, el resultado depende de:

1. **Masa efectiva del atacante y defensor**.
2. **Velocidad de impacto del atacante** (`SquadMovementComponent.velocity`).
3. **Tipo de unidad defensora** (ej. picas o escudos resisten mejor).

**Flujo simplificado de resolución:**

```csharp
csharp
CopiarEditar
float fuerzaImpacto = masaAtacante * velocidadImpacto;

if (fuerzaImpacto > masaDefensora * resistenciaTipoUnidad)
    Resultado = FormaciónDefensoraRota;
else
    Resultado = AtacanteInterrumpido;

```

- `resistenciaTipoUnidad`: multiplicador definido por tipo (ej. Spearmen: x1.5, arqueros: x0.75).
- `FormaciónDefensoraRota`: aplica `Dispersed` a las unidades afectadas.
- `AtacanteInterrumpido`: reduce momentum o cambia estado a `Stagger`.

---

🧱 **3. Efectos posibles tras la colisión**

- Si el atacante supera la resistencia:
    - La formación defensora se rompe (estado `Dispersed`).
    - Las unidades pueden recibir daño adicional (colisión física o por desorganización).
- Si el defensor resiste:
    - El atacante es frenado.
    - Se puede aplicar `Stagger` si el impacto fue parcial.
    - La IA puede cambiar a "Retirada táctica" o "Mantener posición".

---

**🧠 4. Diseño emergente**

- Escuadras ligeras en `Dispersa` tienen baja masa → excelente evasión, pero no aguantan carga.
- Escuadras defensivas en `Muro de Escudos` pueden resistir incluso cargas frontales.
- Una carga en `Cuña` con alta velocidad puede penetrar formaciones si se enfoca en un punto débil.

---

**🛠️ Recomendación de implementación**

- Centralizar la lógica de masa en un `SquadMassUtilitySystem`.
- El sistema de combate o navegación debería consultar la masa efectiva para:
    - Cálculo de fuerza de colisión.
    - Decisión de ruta (evitar formaciones más pesadas).
- Las animaciones de impacto o ruptura deberían estar vinculadas al resultado de esta lógica.

---

### 5.10 🐎 Daño por Carga

El **daño por carga** es un tipo especial de daño físico aplicado en el momento de colisión entre escuadras, cuando una de ellas está en movimiento ofensivo a velocidad elevada. Se calcula de forma separada al combate cuerpo a cuerpo.

---

📌 **¿Cuándo se considera una carga?**

Una escuadra se considera que está realizando una carga si:

- Su `SquadStateComponent.currentState == Moving`
- Tiene una **velocidad superior a cierto umbral** (`velocity > 3.0f`)
- Su `tacticalIntent == Atacando`
- Y su formación es compatible (ver tabla abajo)

---

🛠️ **Cálculo del daño por carga**

```csharp
float CalcularDañoCarga(Entity atacante, Entity defensor)
{
    float masa = CalcularMasaEfectivaEscuadra(atacante);
    float velocidad = GetComponent<SquadMovementComponent>(atacante).velocity;
    float defensa = GetComponent<DefenseComponent>(defensor).value;

    float fuerzaImpacto = masa * velocidad;
    float baseImpacto = fuerzaImpacto * 0.12f; // coeficiente ajustable
    float mitigado = Mathf.Max(0, baseImpacto - defensa * 0.5f);

    return mitigado;
}

```

- `masa`: incluye el modificador de formación.
- `velocidad`: tomada del componente de navegación.
- `defensa`: puede ser mitigada parcialmente si hay escudos o perks.

---

📊 **Compatibilidad de formaciones con carga**

| Formación | ¿Permite carga? | Modificador de masa | Comentario |
| --- | --- | --- | --- |
| **Línea** | ✅ Sí | x1.0 | Estándar ofensivo |
| **Cuña** | ✅ Sí | x1.3 | Ideal para romper líneas |
| **Dispersa** | ✅ Sí | x0.5 | Carga ligera y rápida (caballería) |
| **Schiltron** | ❌ No | x1.5 | Formado para resistir, no atacar |
| **Muro de escudos** | ❌ No | x1.5 | Defensiva, inmóvil |
| **Testudo** | ❌ No | x2.0 | Defensa total, no permite velocidad ni impulso |

---

🧩 **Aplicación del daño**

1. En el momento de colisión detectado por `SquadCollisionSystem`:
    - Se evalúa si el atacante cumple condiciones de carga.
    - Se calcula el daño con `CalcularDañoCarga()`.
    - Se reparte entre las unidades afectadas del defensor.
    - Si la fuerza de impacto supera la masa defensora, puede provocar ruptura de formación.
2. También puede provocar ruptura de formación (estado `Dispersed`) si se supera la **masa efectiva** del defensor.

---

🔥 **Ejemplo**

- Masa efectiva atacante: `320`
- Velocidad: `4.5`
- Defensa del defensor: `20`

```
yaml
Fuerza de impacto: 320 * 4.5 = 1440
Base daño: 1440 * 0.12 = 172.8
Mitigado: 172.8 - (20 * 0.5) = 162.8 de daño por carga

```
## 🔄 6. Flujo de Partida

📌 **Duración de partida:** Timer base corto con extensión por captura de puntos. Máximo 30 minutos (`battleDurationSeconds = 1800`).

---

### 6.1 🧭 Transiciones entre Escenas

*(Feudo → Preparación → Combate → Post Batalla)*

📌 **Descripción:**

El juego está dividido en **escenas independientes** que representan los diferentes estados de juego. Cada transición debe manejarse limpiamente para conservar los datos del jugador (loadouts, perks, escuadras seleccionadas).

🧩 **Elementos a implementar:**

```csharp
SceneFlowManager (Singleton)
- Estado actual del juego: enum {Feudo, Preparación, Combate, PostPartida}
- Carga asíncrona de escenas usando Addressables
- Persiste datos del jugador entre escenas (DataContainer)
```

```csharp
DataContainer (ScriptableObject o Singleton en DontDestroyOnLoad)
- Clase del héroe seleccionada
- Escuadras activas
- Configuración de perks y loadout
```

🔁 **Interacción:**

- Carga previa (`Feudo`) permite elegir escuadras y perks
- En combate, los datos del `DataContainer` son leídos por los sistemas de spawning e inicialización
- Post batalla lee datos de rendimiento para recompensas

---

### 6.2 ☠️ Ciclo de Vida del Héroe (Muerte, Respawn, Cooldown)

📌 **Descripción:**

Cuando el héroe muere, entra en un estado de cooldown creciente. Durante ese tiempo, la cámara se convierte en **modo espectador** y la escuadra entra en estado pasivo.

🧩 **Componentes clave:**

```csharp
HeroLifeComponent
- isAlive: bool
- respawnCooldown: float
- deathTimer: float
- deathsCount: int
```

```csharp
HeroRespawnSystem
- Al morir, activa `deathTimer = base + (deathCount × incremento)`
- Cuenta atrás hasta llegar a 0
- Ejecuta el respawn en punto seleccionado

HeroSpectatorCameraSystem
- Reemplaza cámara por seguimiento de aliados vivos
- Navegación con ← / → o Tab
```

🔁 **Interacción:**

- `HUDSystem` cambia a modo espectador (UI reducida)
- La `HeroSpawnManager` debe acceder a spawn points válidos

---

### 6.3 🪖 Estado y Retiro de Escuadra al Morir el Héroe

📌 **Descripción:**

La escuadra que tenía el héroe queda **manteniendo posición** al morir el jugador. Al faltar 10 segundos para que el héroe reviva, la escuadra inicia su **retirada inteligente** y desaparece después de 5 segundos si no muere antes.

🧩 **Componentes:**

```csharp
SquadStateComponent
- currentState: enum {Activo, Defendiendo, Retirada, Desaparecida}
- lastOwnerAlive: bool
- retreatTriggered: bool
```

```csharp
SquadRetreatSystem
- Espera `respawnTime - 10s`
- Busca ruta con menor presencia enemiga (usando mapa de calor o navmesh tags)
- Cambia estado → Retirada
- Desactiva escuadra si llega a punto seguro o tras 5s

SquadVisibilitySystem
- Maneja fade-out visual si se retira correctamente
```

🔁 **Interacción:**

- `NavMeshSystem` o un sistema de evasión será necesario para evitar zonas hostiles
- `HUD` puede marcar el estado de la escuadra (p.ej., ícono de retirada)

---

### 6.4 🏳️ Reglas del Sistema de Captura y Uso de Supply Points

📌 **Descripción:**

Los supply points permiten al jugador **curar** su escuadra y cambiar su loadout solo si no están **en disputa**. Los puntos tienen 3 estados: aliado, neutral y enemigo.

🧩 **Componentes:**

```csharp
SupplyPointComponent
- ownerTeam: enum {None, TeamA, TeamB}
- isContested: bool
- captureProgress: float
- healingRadius: float
```

```csharp
SupplyCaptureSystem (lógica incluida en SupplyInteractionSystem)
- Detecta héroes enemigos en zona sin defensores → inicia barra de captura
- Si entra un héroe del bando propietario → **resetea captureProgress a 0** completamente
- La captura no avanza mientras haya algún héroe propietario en el área
- Al completar captura (progress >= 100) → cambia `ownerTeam`, lanza `SupplyZoneCapturedEvent`

SupplyInteractionSystem
- Si supply está en estado aliado y sin disputa:
    - Permite cambiar escuadra activa
    - Cura pasivamente a héroe y escuadra en área
```

🔁 **Interacción:**

- Se conecta con `HeroInputSystem` (para menú de cambio)
- `HUD` muestra progreso de captura si el jugador está en rango

---

### 6.5 📍 Asignación de Spawn Inicial

📌 **Descripción:**

En la pantalla de preparación, el jugador elige un **punto de spawn** entre los disponibles. Este se usa al inicio y también en sus respawns durante la partida.

🧩 **Componentes:**

```csharp
SpawnPointComponent
- team: Team
- isActive: bool
- position: Vector3
```

```csharp
SpawnSelectionSystem
- Interfaz que muestra puntos válidos en el mapa
- Permite elegir uno antes de iniciar partida

HeroSpawnSystem
- Spawnea al héroe en el punto elegido
- Lo reutiliza para futuros respawns
```

🔁 **Interacción:**

- `MapUIController` para seleccionar spawn
- `GameManager` o `MatchController` asigna posición real al iniciar partida

### 6.6 🧭 Pantalla de Preparación de Batalla

📌 *Descripción General*

La **pantalla de preparación de batalla** es una escena transicional crítica entre el lobby de matchmaking y el inicio de la partida. Su propósito es permitir al jugador configurar su estrategia antes del despliegue: seleccionar escuadras, perks, punto de spawn y revisar el mapa táctico.

Esta escena se gestiona como parte del flujo general definido por `SceneFlowManager`, y actúa como punto de validación de datos de entrada para la escena de batalla.

---

⚙️ *Sistemas Involucrados*

- `SceneFlowManager`: gestiona la transición entre escenas y define el estado actual del juego (`enum GamePhase { Feudo, Preparacion, Combate, PostPartida }`).
- `DataContainer`: almacena los datos persistentes del jugador entre escenas (héroe, escuadras, perks, spawn).
- `SpawnSelectionSystem`: permite seleccionar un punto de aparición válido sobre el mapa táctico.
- `LoadoutSystem`: valida que el total de liderazgo del jugador no sea superado y aplica configuraciones de escuadras.
- `HeroPreviewSystem`: opcionalmente muestra al héroe con su equipamiento actual.
- `TimerSystem_PreparationPhase`: gestiona la cuenta regresiva y el paso automático si el jugador no confirma.

---

🖥️ *Interfaz de Usuario (UI)*

🎯 Panel Central
- `MapUIController`: minimapa interactivo que muestra:
  - Puntos de spawn válidos
  - Supply points
  - Objetivos de captura
  - Indicadores visuales de selección

🧰 Panel Inferior
- `LoadoutBuilderUI`: slots de escuadra
  - Arrastrar y soltar escuadras desde las disponibles
  - Visualización de liderazgo usado / máximo
- Botones de selección rápida de loadouts predefinidos

🔧 Panel Inferior Derecho
- `HeroPreviewWidget`: muestra modelo 3D del héroe con su equipamiento y arma activa

🌲 Panel Izquierdo
- Lista de jugadores del equipo con su estado de confirmación

🧠 Panel Superior
- Temporizador de preparación
- Estado global de confirmaciones

✅ Botón Confirmar
- Se habilita si:
  - Al menos una escuadra seleccionada
  - No se excede el liderazgo
  - Hay un spawn seleccionado
- Al presionarlo:
  - Se bloquea la UI
  - Se marca al jugador como listo
  - Espera al resto o al final del tiempo

---

🔄 *Lógica de Flujo*

1. Carga inicial desde `DataContainer`.
2. Visualización de estado actual (loadout, spawn, perks, equipamiento).
3. Selección de escuadras (drag & drop), perks y punto de spawn.
4. Confirmación manual o automática al terminar el tiempo.
5. Validación final y carga de la escena de combate (`AsyncSceneLoader`).

---

📦 *Componentes Clave*

- `SpawnPointComponent`: posición, team, isSelected
- `SquadData` (ScriptableObject): habilidades, formaciones, liderazgo
- `HeroPerk`: perks activos y pasivos
- `HeroData`: clase, equipamiento, atributos
- `LoadoutSaveData`: presets de escuadras y perks

---

✅ *Validaciones Técnicas*

- ❌ Escuadra vacía → botón deshabilitado
- ❌ Liderazgo excedido → UI bloquea selección
- ❌ Sin spawn seleccionado → advertencia
- ✅ Si el tiempo expira → se guarda estado actual como definitivo

---


## 🧬 7. Progresión y Guardado de Datos

---

### 7.1 🧠 Progresión del Héroe (Nivel, Atributos, Perks)

📌 **Descripción:**

El héroe puede subir del **nivel 1 al 30**. Cada nivel otorga puntos para mejorar sus atributos base (`Fuerza`, `Destreza`, `Armadura`, `Vitalidad`) y puntos para desbloquear perks activos o pasivos.

🧩 **Componentes:**

```csharp
HeroProgressComponent
- level: int
- currentXP: float
- xpToNextLevel: float
- attributePoints: int
- perkPoints: int
```

```csharp
HeroAttributesComponent
- fuerza: int
- destreza: int
- armadura: int
- vitalidad: int
```

```csharp
HeroLevelSystem
- Al finalizar partida, añade XP basada en rendimiento
- Si supera `xpToNextLevel`, sube de nivel
- Asigna nuevos puntos de atributo y perk
```

🔁 **Interacción:**

- Con `HUDSystem` para mostrar nivel, barra de XP y atributos
- Con `PerkSystem` para validar puntos disponibles

---

### 7.2 💾 Guardado Local en MVP

📌 **Descripción:**

Toda la progresión del jugador en MVP se guarda **localmente**. Esto incluye:

- Nivel y atributos del héroe
- Perks desbloqueados
- Escuadras formadas y su progreso

🧩 **Componentes:**

```csharp
SaveData (clase serializable)
- HeroData: nivel, atributos, perks activos
- SquadData: lista de escuadras, nivel, habilidades
- UserSettings: configuración de audio, UI, etc.
```

```csharp
LocalSaveSystem
- Guardar en `Application.persistentDataPath`
- Serialización con JSON o BinaryFormatter
- Métodos: SaveGame(), LoadGame(), ResetProgress()
```

🔁 **Interacción:**

- Se ejecuta automáticamente al cerrar o al terminar una partida
- El `Barracón` y el `Feudo` cargan la data al iniciar escena

---

### 7.3 📁 Estructura de ScriptableObjects para Perks y Escuadras

📌 **Descripción:**

Los perks y escuadras estarán definidos como **ScriptableObjects**, facilitando su edición y expansión sin tocar código.

🧩 **Ejemplos:**

```csharp
[CreateAssetMenu(menuName = "Perks/HeroPerk")]
public class HeroPerk : ScriptableObject {
    public string perkName;
    public Sprite icon;
    public string description;
    public PerkType type; // Activo / Pasivo
    public List<string> tags; // Ofensivo, Defensa, Liderazgo, Clase
    public float cooldown;
    public EffectData effect;
}
```

```csharp
[CreateAssetMenu(menuName = "Squad/SquadData")]
public class SquadData : ScriptableObject {
    public string squadName;
    public int liderazgoCost;
    public GameObject prefab;
    public List<FormationType> availableFormations;
    public List<AbilityData> abilities;
    public SquadStats baseStats;
}
```

🔁 **Interacción:**

- Se usan para poblar la UI en el barracón y en la pantalla de selección
- `CombatSystems` los leen para aplicar sus efectos

---

### 7.4 🧠 Sistema de Perks: Carga, Activación y Visualización

📌 **Descripción:**

El sistema de perks es un **árbol modular**. El jugador puede activar hasta `5 pasivos` y `2 activos`. Se cargan desde ScriptableObjects y aplican efectos en combate o fuera de él.

🧩 **Componentes:**

```csharp
PerkComponent
- List<HeroPerk> activePerks
- List<HeroPerk> passivePerks
```

```csharp
PerkSystem
- Evalúa perks activos cada frame (si están disponibles)
- Ejecuta efecto correspondiente (buff, daño, control)
- Verifica requisitos como cooldown y stamina

PerkManager (UI)
- Muestra árbol completo de perks disponibles
- Permite asignar y quitar perks con drag & drop o click
```

📌 **Activación en Combate:**

- Perks activos se activan por condiciones o triggers definidos en su configuración.
- Consumen stamina y entran en cooldown.

> **Nota importante:** Las habilidades del héroe (Q, E, R + ultimate F) son un sistema SEPARADO de los perks. Cada clase tiene 3 habilidades de clase + 1 ultimate fijas (ver `HeroAbilityComponent` en sección 7.5). Los perks son bonificaciones de talento con 2 slots activos + 5 slots pasivos, NO son las habilidades principales del héroe.

📌 **Sinergia:**

- Perks pasivos modifican atributos del héroe o su escuadra
- Algunos perks se activan automáticamente según condiciones (ej. “+mitigación si no te mueves”)

🔁 **Interacción:**

- `CombatSystem` accede a buffs de perks en tiempo real
- `SquadSystem` consulta perks que afectan estadísticas o comportamiento
- `HUD` representa el estado de cada perk con íconos, cooldown, y tooltips

---
### 7.5 🔀 Sistema de Clases de Héroe

#### 📌 Descripción

Cada clase de héroe (Espada y Escudo, Espada a Dos Manos, Lanza, Arco) define su rol táctico, atributos base, límites de progresión, habilidades exclusivas y sinergias con escuadras. **El MVP incluye las 4 clases de héroe: Sword & Shield, Two-Handed Sword, Spear y Bow.** La implementación debe garantizar que las clases:

- Sean fácilmente instanciables desde datos externos.
- Impongan límites a la asignación de atributos.
- Asignen automáticamente habilidades compatibles.
- Permitan perks únicos según clase.

---

#### 🧩 Componentes Técnicos

#### `HeroClassDefinition` (ScriptableObject)

Define los parámetros estáticos de cada clase.

```csharp
public enum HeroClass {
    EspadaYEscudo,
    EspadaDosManos,
    Lanza,
    Arco
}

[CreateAssetMenu(menuName = "Hero/Class Definition")]
public class HeroClassDefinition : ScriptableObject {
    public HeroClass heroClass;
    public Sprite icon;
    public string description;

    public int baseFuerza;
    public int baseDestreza;
    public int baseArmadura;
    public int baseVitalidad;

    public int minFuerza, maxFuerza;
    public int minDestreza, maxDestreza;
    public int minArmadura, maxArmadura;
    public int minVitalidad, maxVitalidad;

    public GameObject weaponPrefab;
    public List<HeroAbility> abilities;
    public List<HeroPerk> validClassPerks;
}

```

#### `HeroAttributesComponent` (ECS)

```csharp
public struct HeroAttributesComponent : IComponentData {
    public int fuerza;
    public int destreza;
    public int armadura;
    public int vitalidad;
    public Entity classDefinition; // referencia a HeroClassDefinition
}

```

#### `HeroAbilityComponent` (ECS)

```csharp
public struct HeroAbilityComponent : IComponentData {
    public Entity abilityQ; // Q
    public Entity abilityE; // E
    public Entity abilityR; // R
    public Entity ultimate; // F
}

```

---

#### ⚙️ Sistemas Involucrados

#### `HeroInitializationSystem`

- Carga atributos base y habilidades desde `HeroClassDefinition`.
- Se ejecuta al crear un nuevo héroe o cargar una partida.

#### `HeroAttributeSystem`

- Valida en tiempo real que los puntos asignados no excedan los límites definidos por clase.

```csharp
if (nuevoValor > clase.maxFuerza || nuevoValor < clase.minFuerza)
    bloquearAsignación();

```

#### `PerkSystem` / `PerkTreeUI`

- Filtra perks según clase del héroe.

```csharp
if (perk.tags.Contains("Arco") && heroClass != HeroClass.Arco)
    ocultarPerk();

```

#### `LoadoutSystem`

- Verifica que el arma equipada coincida con el `HeroClassDefinition`.
- Impide uso de escuadras o perks no compatibles.

---

#### 🖥️ UI

- Panel de creación y carga de héroe debe mostrar:
    - Descripción de clase.
    - Rango permitido de atributos.
    - Habilidades disponibles (preview).
    - Arma obligatoria para esa clase.
    - Perks exclusivos habilitados.

---

#### ✅ Validaciones Críticas

| Validación | Nivel | Acción |
| --- | --- | --- |
| Arma incompatible con clase | Loadout / Combate | Bloquear |
| Atributo fuera de rango | UI de atributos | Evitar asignación |
| Perk exclusivo de otra clase | UI de perks | Ocultar o desactivar |
| Habilidad no definida por clase | Combate | Invalidar ejecución |

---

### 7.6 🧬 Progresión Avanzada de Escuadras y Sinergias

📌 **Objetivo:**

Expandir la progresión de escuadras más allá del nivel numérico, incorporando habilidades, formaciones, equipamiento y sinergias con el héroe, basadas en el diseño del GDD.

---

#### 7.6.1 🗂️ ScriptableObjects por Tipo de Escuadra

Cada escuadra estará representada por un `SquadData` específico, que contendrá:

```csharp

[CreateAssetMenu(menuName = "Squads/SquadDataExtended")]
public class SquadData : ScriptableObject {
    [Header("Identificación")]
    public string squadName;
    public SquadType tipo; // Ej. Escuderos, Arqueros
    public Sprite icon;
    public GameObject prefab;

    [Header("Formaciones y Liderazgo")]
    public List<FormationType> availableFormations;
    public int liderazgoCost;
    public BehaviorProfile behaviorProfile;

    [Header("Habilidades por Nivel")]
    public List<AbilityData> abilitiesByLevel;

    [Header("Atributos Base (Unidad)")]
    public float vidaBase;
    public float velocidadBase;
    public float masa;
    public float peso; // ligero, medio, pesado
    public float bloqueo; // solo si tiene escudo

    [Header("Defensas por Tipo")]
    public float defensaCortante;
    public float defensaPerforante;
    public float defensaContundente;

    [Header("Daño y Penetración")]
    public float dañoCortante;
    public float dañoPerforante;
    public float dañoContundente;

    public float penetracionCortante;
    public float penetracionPerforante;
    public float penetracionContundente;

    [Header("Solo para Unidades a Distancia")]
    public bool esUnidadADistancia;
    public float alcance;
    public float precision;
    public float cadenciaFuego;
    public float velocidadRecarga;
    public int municionTotal;

    [Header("Progresión por Nivel")]
    public AnimationCurve vidaCurve;
    public AnimationCurve dañoCurve;
    public AnimationCurve defensaCurve;
    public AnimationCurve velocidadCurve;
}

```

- **abilitiesByLevel:** lista ordenada de habilidades (activas/pasivas) desbloqueables por nivel.
- **baseStats:** contiene los atributos iniciales (vida, daño, defensas, etc.).
- **availableFormations:** accesibles desde el inicio o con desbloqueo progresivo.
- **behaviorProfile:** define estilo táctico (ver abajo).

---

#### 7.6.2 🧠 Sistema `SquadProgressionSystem`

Controla la experiencia y progresión de cada escuadra activa:

```csharp

SquadProgressComponent
- level: int
- currentXP: float
- xpToNextLevel: float
- unlockedAbilities: List<AbilityData>
- unlockedFormations: List<FormationType>

```

- La experiencia se gana por escuadra según su participación en combate.
- Cada `10 niveles`, se desbloquea una habilidad nueva.
- Nuevas formaciones se habilitan en niveles específicos (ej. Testudo en nivel 10 para Escuderos).

---
Las formaciones posibles para una escuadra están definidas en su SquadData.availableFormations.
El campo unlockedFormations en SquadProgressComponent refleja el subconjunto activo disponible según el nivel actual de la escuadra.
Esto evita conflictos: availableFormations actúa como límite superior, mientras que unlockedFormations representa el estado de progresión.
---

#### 7.6.3 🛡️ Sistema de `EquipamientoComponent`

Cada unidad tendrá un estado de equipamiento persistente:

```csharp

UnitEquipmentComponent
- armorPercent: float
- isDeployable: bool
- hasDebuff: bool

```

- Si `armorPercent < 50%` ➜ `hasDebuff = true`
- Si `armorPercent == 0%` ➜ `isDeployable = false`
- Este estado se actualiza al morir unidades y se guarda entre partidas.
- El HUD de preparación de batalla mostrará advertencias si una escuadra no es viable.

---

#### 7.6.4 🧠 BehaviorProfiles de Escuadras

Cada tipo de escuadra tendrá un perfil de comportamiento táctico predefinido, usado por la IA y animaciones contextuales.

```csharp

public enum BehaviorProfile {
    Defensivo,
    Hostigador,
    Anticarga,
    Versátil
}

```

| Escuadra | Perfil |
| --- | --- |
| Escuderos | Defensivo |
| Arqueros | Hostigador |
| Piqueros | Anticarga |
| Spearmen | Versátil |

> Estos perfiles afectan la toma de decisiones AI en SquadAISystem y priorización de objetivos.
> 

#### 7.6.5 📊 Sistema de Atributos de Unidad (por Escuadra)

📌 **Objetivo:**

Implementar un sistema estructurado de atributos para unidades individuales dentro de cada escuadra, alineado con el apartado **4.12 del GDD**, para soportar progresión, balance y sinergias.

---

#### 🧩 Estructura `UnitStatsComponent`

Cada unidad dentro de una escuadra portará un componente con atributos base, que escalan con nivel y pueden ser modificados por perks o habilidades.

```csharp

public struct UnitStatsComponent : IComponentData {
    public float vida;
    public float velocidad;
    public float masa;
    public float peso; // 1=ligero, 2=medio, 3=peso
    public float bloqueo; // si tiene escudo

    public float defensaCortante;
    public float defensaPerforante;
    public float defensaContundente;

    public float dañoCortante;
    public float dañoPerforante;
    public float dañoContundente;

    public float penetracionCortante;
    public float penetracionPerforante;
    public float penetracionContundente;

    public int liderazgoCosto;
}

```

> Los atributos se poblarán desde SquadData y escalarán según nivel en SquadProgressComponent.
> 

---

#### 🏹 Atributos Exclusivos para Unidades a Distancia

Se añade un componente adicional opcional para escuadras como **Arqueros**:

```csharp

public struct UnitRangedStatsComponent : IComponentData {
    public float alcance;
    public float precision;
    public float cadenciaFuego;
    public float velocidadRecarga;
    public int municionTotal;
}

```

> Se asocia solo si el SquadType lo requiere. Usado por RangedAttackSystem y HUD.
> 

---

#### 🔁 Integración con otros sistemas

- `SquadAttackSystem`: consulta tipo de daño y penetración para calcular daño efectivo.
- `SquadAIComponent`: usa `velocidad`, `alcance`, y `masa` para determinar tácticas óptimas.
- `FormationSystem`: puede modificar temporalmente atributos (ej. bonus de defensa en *Testudo*).
- `PerkSystem`: perks del héroe pueden modificar ciertos stats como `precisión` o `velocidad`.

---

#### 📈 Escalado por Nivel

Cada escuadra usa una función de progresión aplicada sobre `UnitStatsComponent`:

```csharp

public struct SquadProgressionStats {
    public AnimationCurve vidaCurve;
    public AnimationCurve dañoCurve;
    public AnimationCurve defensaCurve;
    public AnimationCurve velocidadCurve;
}

```

> Estas curvas definen el escalado base hasta nivel 30, sin intervención del jugador.
> 

---

#### 🧪 Ejemplo Visual (Escuderos, Nivel 1)

| Atributo | Valor |
| --- | --- |
| Vida | 120 |
| Defensa Contundente | 25 |
| Daño Cortante | 14 |
| Penetración Cortante | 3 |
| Velocidad | 2.5 |
| Bloqueo | 40 |
| Liderazgo | 2 |

---

#### 🔒 Notas de Validación

- Las unidades **no pueden tener atributos modificados directamente por el jugador** (según GDD).
- Los modificadores válidos provienen de:
    - Nivel de escuadra
    - Formación activa
    - Habilidades de escuadra
    - Perks del héroe
- Estos datos deben sincronizarse entre cliente y servidor (Netcode Snapshot).

# Sin título

### 7.7 🔄 Control de Estado entre Héroe y Unidades del Escuadrón

### 🎯 Descripción funcional

Este módulo define el comportamiento coordinado entre un héroe y las unidades de su escuadrón, evaluando distancia y movimiento para controlar transiciones entre los estados `Formed` y `Moving` de cada unidad. Se asegura que las unidades no reaccionen en cada frame, sino que su lógica se base en un modelo persistente de estados evaluado y transicionado de forma controlada.

### ⚙️ Estados definidos

### HeroStateComponent

```csharp
public enum HeroState { Idle, Moving }

```

### UnitFormationStateComponent

```csharp
public enum UnitFormationState { Formed, Waiting, Moving }
```

### 📐 Lógica de transición

| Estado actual unidad | Condición | Nuevo estado unidad | Descripción |
| --- | --- | --- | --- |
| Formed | Héroe sale del radio (>5m) | Waiting | Inicia delay aleatorio (0.5-1.5s) |
| Waiting | Delay expira | Moving | Comienza movimiento hacia slot |
| Waiting | Héroe regresa al radio Y unidad en slot | Formed | Cancela delay, permanece en posición |
| Moving | Llega a slot Y héroe dentro del radio | Formed | Completa movimiento exitosamente |
| Moving | Llega a slot Y héroe fuera del radio | Waiting | Nuevo delay antes de moverse nuevamente |

**Notas importantes:**
- Solo las unidades en estado `Moving` se mueven físicamente
- El estado `Waiting` introduce un delay aleatorio para crear movimientos más naturales
- Las unidades en estado `Formed` o `Waiting` permanecen estáticas

---

### 🧩 Componentes involucrados

- `HeroStateComponent`: actualizado por `HeroStateSystem` en base al input del jugador.
- `UnitFormationStateComponent`: actualizado por `UnitFormationStateSystem` evaluando distancia del héroe al centro del escuadrón y posición de la unidad en su slot.
- `LocalTransform`: posición actual de héroe y unidades.
- `SquadUnitElement`: buffer de unidades del escuadrón.
- `SquadDataComponent`: contiene las formaciones disponibles y datos del grid.

---

### 🧠 Sistemas requeridos

### 1. HeroStateSystem

Actualiza el estado del héroe (`Idle` o `Moving`) usando información de input o delta de posición.

### 2. UnitFormationStateSystem

Gestiona las transiciones de estado de cada unidad implementando la tabla de transiciones:

- **Formed → Waiting**: Cuando el héroe sale del radio de formación (>5m del centro del escuadrón)
- **Waiting → Moving**: Cuando expira el delay aleatorio (0.5-1.5 segundos)
- **Waiting → Formed**: Cuando el héroe regresa al radio y la unidad ya está en su slot
- **Moving → Formed**: Cuando la unidad llega a su slot y el héroe está dentro del radio
- **Moving → Waiting**: Cuando la unidad llega a su slot pero el héroe sigue fuera del radio

### 3. UnitFollowFormationSystem

Mueve las unidades hacia su posición asignada **solo si están en estado `Moving`**. Las unidades en estado `Formed` o `Waiting` permanecen estáticas, creando un comportamiento más natural y evitando movimientos innecesarios.

---
### 7.8 📦 Estructura de Persistencia del Jugador (MVP y Post-MVP)

📌 **Descripción general:**

Este módulo define la estructura de datos central que representa el estado persistente del jugador. Permite guardar y cargar el progreso tanto a nivel local (en disco) como en el futuro a través de un backend. Incluye el héroe, escuadras, inventario, equipamiento y perks.

---

### 🧱 Estructuras de Datos Serializables

#### `PlayerData.cs`

```csharp
[Serializable]
public class PlayerData {
    public string id;
    public string name;
    public string password; // Temporal para persistencia local
    public List<HeroData> heroList;
}
```

#### `HeroData.cs`

```csharp

[Serializable]
public class HeroData {
    public string name;
    public HeroClass heroClass; // Referencia al tipo base (ScriptableObject)
    public CalculatedAttributes cachedAttributes;
    public List<Item> inventory;
    public List<string> unlockedSquads;
    public List<SquadInstanceData> squadList;
    public AvatarParts parts;
    public int level;
    public int expPoints;
    public Equipment equipment;
}

```

---

#### `SquadInstanceData.cs`

```csharp

[Serializable]
public class SquadInstanceData {
    public string id;
    public SquadData baseSquad; // Referencia a ScriptableObject
    public int level;
    public int experience;
    public List<string> unlockedAbilities;
    public List<int> unlockedFormationsIndices;
    public int selectedFormationIndex;
    public string customName;
}

```

---

#### `AvatarParts.cs` (solo cosmético)

```csharp

[Serializable]
public class AvatarParts {
    public string headPartID;
    public string torsoPartID;
    public string glovesPartID;
    public string pantsPartID;
    public string bootsPartID;
    public string hairPartID;
}

```

---

#### `Equipment.cs`

```csharp

[Serializable]
public class Equipment {
    public Item head;
    public Item torso;
    public Item gloves;
    public Item pants;
    public Item weapon;
}

```

---

#### `Item.cs` y tipos

```csharp

[Serializable]
public class Item {
    public string itemID;
    public ItemType type;
    public Dictionary<string, float> stats;
    public List<VisualAttachment> visuals;
}

public enum ItemType {
    Headgear, Torso, Gloves, Pants, Weapon
}

[Serializable]
public class VisualAttachment {
    public string prefabID;
    public string boneTarget;
}

```

---

#### `CalculatedAttributes.cs` (atributos derivados cacheados)

```csharp

[Serializable]
public class CalculatedAttributes {
    public float maxHealth, stamina;
    public float strength, dexterity, vitality, armor;
    public float bluntDamage, slashingDamage, piercingDamage;
    public float bluntDefense, slashDefense, pierceDefense;
    public float bluntPenetration, slashPenetration, piercePenetration;
    public float blockPower, movementSpeed;
}

```

---

#### 💾 `SaveSystem` y `LoadSystem`

#### 📁 Archivos:

- `SaveSystem.cs`
- `LoadSystem.cs`
- Guardado en `Application.persistentDataPath` en formato JSON.

#### 📌 Métodos esperados:

```csharp

public static class SaveSystem {
    public static void SavePlayer(PlayerData data);
    public static PlayerData LoadPlayer();
}

```

- Guardado automático tras partida o cambios en el barracón.
- Carga automática al iniciar el juego.

---

#### ⚙️ Extensibilidad para backend

Se define la interfaz:

```csharp

public interface ISaveProvider {
    void Save(PlayerData data);
    PlayerData Load();
}

```

Implementaciones:

- `LocalSaveProvider` (JSON en disco)
- `CloudSaveProvider` (Futuro backend con API)

Esto facilita la transición al backend sin modificar lógica de negocio.

---

#### 🚀 Integración con ECS

- Los datos cargados se transforman en entidades en `GameBootstrapSystem`.
- Cada `HeroData` genera una entidad `Hero`, con sus componentes iniciales (`HeroStats`, `HeroAttributes`, `PerkComponent`, etc.).
- Cada `SquadInstanceData` genera entidades asociadas al `SquadData` referenciado, con su progreso dinámico (nivel, habilidades, formaciones desbloqueadas).

---

#### 🔄 Flujo General de Persistencia

```

Inicio del juego
  ↓
Cargar PlayerData desde JSON
  ↓
Seleccionar HeroData activo
  ↓
Generar entidades iniciales en ECS
  ↓
Actualizar progreso durante la sesión
  ↓
Guardar PlayerData modificado en disco al cerrar o tras batalla

```

---

#### 🧠 Buenas prácticas implementadas

- Separación clara entre datos estáticos (ScriptableObject) y dinámicos (progreso serializado).
- Cache de atributos derivados para evitar cálculos innecesarios (`CalculatedAttributes`).
- Referencias indirectas a `ScriptableObject` mediante nombres o IDs.
- Preparado para expansión multijugador (con `ISaveProvider`).

---

#### ✅ Checklist de criterios técnicos del backlog

| Requisito | Estado |
| --- | --- |
| Estructura de `PlayerData`, `HeroData`, `SquadInstanceData` | ✅ |
| Serialización y guardado local funcional | ✅ |
| Separación entre datos base y dinámicos | ✅ |
| Soporte para atributos cacheados del héroe | ✅ |
| Referencias limpias a `SquadData`, `HeroClass`, etc. | ✅ |
| Diseño listo para futura integración backend | ✅ |
---
### 🧠 7.9 `DataCacheService`: Cálculo y Cache de Atributos

📌 **Descripción general:**

`DataCacheService` es un servicio central encargado de calcular, almacenar y servir datos derivados del héroe como atributos, liderazgo total, y perks activos. Está diseñado para:

- Minimizar cálculos redundantes en tiempo de ejecución.
- Proveer acceso rápido a datos transformados desde `HeroData`, `Equipment`, perks y clase base.
- Ser accesible desde sistemas ECS y UI, sin modificar directamente los datos de entrada.

---

#### 🧩 Componentes clave:

#### `DataCacheService.cs`

```csharp
public static class DataCacheService {
    void CacheAttributes(HeroData heroData);
    CalculatedAttributes GetCachedAttributes(string heroId);
    List<string> GetActivePerks(string heroId);
    void Clear(); // Opcional, para limpieza de caché en escena
}

```

#### Internamente:

- Usa `Dictionary<string, CalculatedAttributes>` para cachear por ID de héroe.
- Calcula los valores combinando:
    - Atributos base por clase (`HeroClassDefinition`)
    - Nivel y puntos de atributo
    - Equipo (`Equipment`)
    - Perks activos (si están implementados)
- Utiliza las fórmulas descritas en el GDD para daño, defensa, vida, liderazgo y penetraciónGDD.

---

#### 🔁 Interacción:

- Llamado desde `GameBootstrapSystem` al cargar datos persistidos.
- Llamado desde `HeroAttributeSystem`, `PerkSystem`, `LoadoutSystem` y HUD.
- Opcionalmente se puede recalcular tras cambios en el inventario, nivel, perks o clase del héroe.

---

#### ⚙️ Ejemplo de flujo:

```
plaintext
CopiarEditar
Al cargar HeroData
    ↓
DataCacheService.CacheAttributes(HeroData)
    ↓
Genera CalculatedAttributes
    ↓
Almacena en memoria
    ↓
HeroStatsSystem accede vía GetCachedAttributes(heroId)

```

---

#### 📌 Consideraciones técnicas:

- La clase debe ser pasiva: solo lee datos y expone getters.
- No debe guardar referencias a ScriptableObjects ni a entidades ECS.
- Compatible con serialización indirecta (`HeroClassDefinition.name`, `Item.itemID`, etc.).
- Pensada para operar **antes** de la conversión a entidades (durante carga de datos).

---

#### ✅ Checklist

| Requisito | Estado |
| --- | --- |
| Cache de `CalculatedAttributes` | ✅ |
| Soporte para perks y equipo | ✅ |
| Acceso rápido por ID de héroe | ✅ |
| Preparado para integración con ECS | ✅ |
| Compatible con lógica actual de persistencia | ✅ |
---

## 🌐 8. Multijugador (MVP)

---

### 🖧 8.1 Arquitectura de Red: Servidor Dedicado

📌 **Descripción:**

El MVP funcionará sobre **servidores dedicados**, usando **Netcode for GameObjects** (Unity) combinado con **Unity DOTS (ECS)** para la lógica principal.

🧩 **Componentes de red:**

- `NetworkManager`: controla el ciclo de conexión, matchmaking (inicialmente aleatorio), sincronización y desconexiones.
- `ServerGameLoop`: mantiene la lógica central de la partida (captura, puntos, respawn).
- `ClientPredictionSystem`: gestiona predicción local para control suave del héroe.
- `Authority`: el servidor tiene autoridad sobre escuadras, muerte, respawn y puntos de control.

🔁 **Interacción:**

- `MatchmakingScene` conecta el jugador con un lobby preasignado.
- El cliente solo ejecuta predicción visual y envía inputs.
- La validación (impactos, cooldowns, cambios de escuadra) se hace en el servidor.

---

### 🔄 8.2 Sincronización de Escuadras y Héroes (Snapshots o Comandos)

📌 **Decisión Final:**

Usaremos **comandos + snapshots ligeros**:

- Comandos: para enviar inputs (ataques, habilidades, órdenes de escuadra)
- Snapshots: para sincronizar estado de unidades, formaciones, vida, posición, animaciones

🧩 **Componentes:**

```csharp
HeroCommandSender (Client)
- Envía input de movimiento, ataque y habilidades

HeroSnapshot (Server → Client)
- posición, rotación, animación, vida

SquadSnapshot
- posición promedio de escuadra
- formación activa
- número de unidades vivas
- habilidades en cooldown
```

📌 **Lógica:**

- El servidor mantiene el estado maestro y envía snapshots cada `X` ms.
- Las escuadras son **no controlables directamente** desde el cliente: sólo se envía el tipo de orden (seguir, atacar, mantener).

---

### 🚶 8.3 Interpolación de Movimiento y Predicción

📌 **Descripción:**

Para suavizar movimiento y reducir lag visual, se usará interpolación en héroes enemigos y predicción en el héroe local.

🧩 **Sistemas:**

- `HeroPredictionSystem`: aplica input local mientras llega confirmación del servidor.
- `HeroReconciliationSystem`: corrige discrepancias.
- `SquadInterpolationSystem`: mueve escuadras remotas con suavidad según snapshot.
- `NetworkTransformECS`: se usará el paquete de Unity adaptado a DOTS.

📌 **Predicción para habilidades:**

- El HUD mostrará uso inmediato.
- Si el servidor cancela el cast (por falta de stamina o interrupción), se corrige visualmente.

---

### 💬 8.4 Comunicación entre Jugadores (Chat Básico)

📌 **Descripción:**

Un sistema de chat básico tipo consola permitirá mensajes entre jugadores del mismo equipo durante la partida.

🧩 **Componentes:**

```csharp
ChatMessageComponent
- senderName: string
- message: string
- teamOnly: bool
```

```csharp
ChatSystem
- Escucha input en UI (tecla `Enter`)
- Envía mensaje al servidor
- El servidor reenvía a todos los clientes del equipo
```

📌 **UI:**

- Consola desplegable con historial (toggle con `T`)
- Campo de texto para escribir
- Color según tipo de mensaje (equipo, global en lobby, sistema)

---

### 🔄 8.5 Cambios de Escuadra desde Supply Points (Restricciones de Sincronización)

📌 **Descripción:**

El héroe puede cambiar su escuadra **únicamente en supply points seguros**. Esta acción debe ser **validada por el servidor** y sincronizada con todos los jugadores.

🧩 **Proceso de cambio:**

1. Cliente solicita cambio (con ID de escuadra deseada)
2. El servidor valida:
    - Que el héroe está en un supply point aliado
    - Que no hay enemigos en rango del punto
    - Que tiene esa escuadra en su loadout
    - Que tiene suficiente liderazgo
3. El servidor despawnea la escuadra actual y spawnea la nueva
4. Se envía `SquadChangeEvent` a todos los clientes

🧩 **Componentes:**

```csharp
SupplyPointComponent
- ownerTeam: enum
- isContested: bool
- healingEnabled: bool
SquadSwapRequest (Command)
- newSquadId: int
```

```csharp
- SquadSwapSystem (Server-side)
   - Valida condiciones
   - Ejecuta el cambio
   - Dispara eventos de sincronización
```

🔁 **Interacción:**

- El HUD activa la UI de escuadras disponibles cuando se entra en un supply point válido
- El cambio puede tardar unos segundos e interrumpirse si el punto es contestado

---

## 🖥️ 9. UI y HUD

---

### 🧱 9.1 Sistema de UI (Canvas con Unity UI)

📌 **Descripción:**

Toda la interfaz se desarrollará con **Unity UI (Canvas)** en modo **Screen Space - Overlay**. Los elementos se organizarán en prefabs reutilizables según contexto (feudo, batalla, selección de escuadra, HUD).

🧩 **Elementos clave:**

- `UIManager`: sistema principal que activa/desactiva módulos de UI según la escena o estado del juego.
- `UIScreenController`: prefabs individuales (HUD, Escuadras, Loadout, PostBattle) conectados al `UIManager`.
- `UIBinderComponent`: vincula entidades ECS con elementos UI (por ejemplo, vida de escuadra, cooldown de habilidad).
- `UIDocumentRoot` (UI Toolkit opcional en futuro MVP ampliado).

🔁 **Interacción:**

- Se comunica con `GameStateSystem` para activar interfaces por fase de partida.
- Lee datos de `HUDDataComponent`, `CooldownComponent`, `SquadStatusComponent`.

---

### 🩸 9.2 HUD de Batalla

📌 **Descripción:**

El HUD está diseñado para ser minimalista pero funcional, inspirado en *Conqueror’s Blade*. Muestra datos relevantes del héroe, su escuadra y el entorno.

🧩 **Elementos del HUD:**

- **Barra de salud del héroe** (`HeroHealthBar`)
- **Barra de estamina** (`HeroStaminaBar`)
- **Iconos de habilidades** con cooldown (`AbilityHUDSlot`)
- **Escuadra activa**:
    - Número de unidades restantes
    - Formación actual
    - Órdenes activas
    - Iconos de habilidades de escuadra
- **Minimapa**
- **Feedback de captura de bandera**
- **Feedback de supply point**

🧩 **Componentes:**

- `HUDController`: actualiza la información cada frame.
- `HeroStatusComponent`, `SquadStatusComponent`, `CooldownComponent`, `CaptureProgressComponent`.

🔁 **Sincronización:**

- Datos del héroe y escuadra se extraen del ECS world del jugador local.
- Elementos con animación (CD circular, daño recibido) actualizados por eventos visuales.

---

### 🗺️ 9.3 Minimapa Dinámico (Feudo y Combate)

📌 **Descripción:**

Minimapa en tiempo real que muestra aliados, enemigos, puntos de captura y supply points.

🧩 **Sistemas involucrados:**

- `MinimapCamera`: cámara ortográfica en altura sobre el mapa.
- `MinimapRenderer`: proyecta íconos en UI según la posición de entidades con `MinimapIconComponent`.

🧩 **Iconos Renderizados:**

- Héroes (propios y enemigos)
- Escuadras activas
- Puntos de captura (A, Base)
- Supply points (con colores según estado: azul/rojo/gris)
- Objetivos activos (marcadores)

🔁 **Interacción:**

- En feudo muestra NPCs y zonas del hub.
- En batalla sincroniza con `GameStateSystem` para marcar objetivos activos.

---

### 🧰 9.4 Interfaz de Preparación y Loadouts

📌 **Descripción:**

Pantalla accesible **antes de entrar a batalla**, permite seleccionar:

- Clase (arma)
- Escuadras (según liderazgo disponible)
- Perks activos/pasivos
- Formaciones predefinidas

🧩 **Componentes:**

- `LoadoutBuilderUI`: permite arrastrar y soltar escuadras en slots según liderazgo restante.
- `HeroPreviewWidget`: muestra el héroe en 3D con equipamiento activo.
- `PerkTreeUI`: árbol de perks simple con activación y reset.

🔁 **Lógica interna:**

- Valida que el total de liderazgo no se exceda.
- Guarda preferencias en `LoadoutSaveData`.
- Se comunica con `GameBootstrapSystem` al iniciar la batalla.

---

### ⚙️ 9.5 Menús de Interacción con Supply y Puntos de Captura

📌 **Supply Point UI:**

- Se activa al **entrar en rango de un supply point aliado y no contestado**.
- Muestra:
    - Iconos de escuadras disponibles (dentro del loadout)
    - Botón para cambiar escuadra activa
    - Mensaje de curación pasiva si aplica
- Usa `SupplyPointUIController`, que escucha `SupplyInteractionComponent`.

📌 **Puntos de Captura UI:**

- Aparece cuando el héroe entra al radio de una bandera.
- Muestra:
    - Barra de progreso de captura
    - Indicador de bloqueo si hay enemigos presentes
    - Nombre del punto (A, Base)
- Usa `CapturePointUIController` + `CaptureProgressComponent`.

🔁 **Actualización:**

- Ambas interfaces están sincronizadas con datos del servidor.
- La visibilidad de estas UIs depende de `ZoneDetectionSystem` que activa/desactiva componentes de UI según el rango.

---

### 🎯 9.6 Sistema de Marcadores de Destino (Hold Position)

📌 **Descripción:**

Sistema visual que muestra marcadores en el mundo 3D para indicar las posiciones exactas donde se moverán las unidades cuando se da una orden de "Hold Position". Los marcadores aparecen únicamente durante órdenes de Hold Position y proporcionan feedback visual inmediato de dónde se formará el escuadrón.

🧩 **Componentes principales:**

**`UnitDestinationMarkerComponent`** (IComponentData):
- Se añade dinámicamente a unidades que requieren marcadores
- Almacena: `markerEntity` (referencia al prefab instanciado), `targetPosition` (posición objetivo), `isActive` (estado), `ownerUnit` (unidad propietaria)

**`DestinationMarkerPrefabComponent`** (IComponentData - Singleton):
- Componente global que almacena la referencia al prefab del marcador
- Configurado mediante `DestinationMarkerAuthoring` desde el Inspector

**`DestinationMarkerSystem`** (SystemBase):
- Sistema principal que gestiona el ciclo de vida completo de los marcadores
- Se ejecuta después de `UnitFollowFormationSystem` para usar posiciones actualizadas
- Funciona exclusivamente en estado `SquadFSMState.HoldingPosition`

🔁 **Flujo de interacción:**

1. **Detección de orden Hold Position:**
   - `SquadControlSystem` captura posición del mouse en terreno
   - `SquadOrderSystem` crea/actualiza `SquadHoldPositionComponent` con posición del mouse
   - `SquadFSMSystem` transiciona escuadrón a `HoldingPosition`

2. **Creación de marcadores:**
   - `DestinationMarkerSystem` detecta unidades en estado `Moving` dentro de escuadrón en `HoldingPosition`
   - Usa `FormationPositionCalculator.CalculateDesiredPosition()` para obtener posición exacta de cada unidad
   - Instancia prefabs de marcadores en posiciones calculadas

3. **Actualización y limpieza:**
   - Marcadores se actualizan cuando cambia la formación en Hold Position
   - Se destruyen automáticamente cuando unidades alcanzan estado `Formed`
   - Se limpian completamente al cambiar a `FollowingHero`

🧩 **Integración con otros sistemas:**

**Con sistemas de formación:**
- Usa `FormationPositionCalculator` para consistencia en cálculos de posición
- Lee `gridPositions` de la formación actual desde `SquadDataComponent`
- Respeta thresholds de distancia: `slotThresholdSq = 0.04f` (0.2m precision)

**Con sistemas de estado:**
- Monitorea `UnitFormationStateComponent.State` para detectar transiciones `Moving` ↔ `Formed`
- Responde a `SquadStateComponent.currentState` para limitar funcionamiento a Hold Position

**Con entrada del usuario:**
- Recibe posiciones de mouse desde `SquadInputComponent.holdPosition`
- Usa posición del mouse como `squadCenter` en lugar de posición del héroe

🧩 **Características técnicas:**

**Precisión de posicionamiento:**
- Marcadores aparecen en posiciones exactas calculadas por el sistema de formación
- Mismos algoritmos que `UnitFollowFormationSystem` para garantizar coherencia
- Thresholds reducidos para mayor precisión visual

**Gestión de memoria:**
- Usa `EntityCommandBuffer` para operaciones thread-safe
- Limpieza automática de componentes al cambiar de estado
- Previene memory leaks mediante destrucción explícita de entidades de marcadores

**Restricciones de uso:**
- **SOLO** activo durante `SquadFSMState.HoldingPosition`
- **NO** se muestran en `FollowingHero` ni otros estados
- **NO** aparecen durante cambios de formación en modo seguimiento

🔁 **Configuración:**

- Requiere un GameObject en escena con `DestinationMarkerAuthoring`
- Prefab del marcador debe tener `LocalTransform` para posicionamiento
- Sistema completamente automático, sin configuración adicional requerida

---
### 📊 9.6 Scoreboard de Batalla (Panel de Estado Activado con `Tab`)

#### 🧾 Descripción General

Durante el combate, el jugador puede activar temporalmente un panel de estado presionando la tecla `Tab`. Este panel proporciona una visión táctica en tiempo real del desarrollo de la batalla, incluyendo:

- ✅ Rendimiento individual de jugadores de ambos bandos.
- 🧭 Control territorial actual (supply points y puntos de captura).
- 🧍 Posicionamiento en vivo de aliados en el mapa.

Este sistema actúa como un HUD expandido y cumple funciones de *scoreboard*, mapa táctico y herramienta de análisis en medio del combate.

#### 🎯 Objetivos Funcionales

- Brindar información condensada sin romper la inmersión.
- Permitir rápida evaluación del estado de aliados y control del terreno.
- Visualización pasiva y no interactiva (sin inputs durante visualización).

#### 🧩 Componentes UI

- **`BattleStatusPanel`**: Contenedor principal visible solo durante `Input.Tab held`.
  - 🎛️ Oculta el HUD principal mientras está activo.
  - ✨ Animación de entrada y salida con transición fade-in/fade-out rápida.

- **`PlayerScoreColumn` (x2)**: Muestra jugadores por equipo (aliados y enemigos).
  - 🧍 Nombre del jugador.
  - ⚔️ Kills de héroes.
  - 🪖 Kills de unidades.
  - 💀 Muertes totales.

- **`BattleStatusMinimap`**: Minimap central con representación expandida.
  - 🧍‍♂️ Posición en tiempo real de héroes aliados (íconos tipo ping).
  - ⛽ Supply points: iconos con estado (gris = neutral, 🔵 azul = aliado, 🔴 rojo = enemigo).
  - 🎯 Puntos de captura: icono + porcentaje + color de dominancia (barra radial o slider).

#### ⚙️ Comportamiento del Sistema

- ⌨️ Se activa mientras se mantiene presionada la tecla `Tab`.
- 👁️ Oculta el HUD principal para evitar superposición.
- 🧼 Al soltar `Tab`, el panel desaparece y el HUD normal se reactiva.

#### 🧠 Lógica Técnica

- 🔄 Sistema central: `BattleStatusUIController`
- Se suscribe a eventos de:
  - `MultiplayerScoreSystem` → 🔢 kills/muertes por jugador
  - `CaptureZoneStatusSystem` → 🎯 porcentaje de captura por zona
  - `SupplyPointStatusSystem` → ⛽ estado de control de supply
  - `AllyPositionBroadcastSystem` → 🧍‍♂️ ubicación en tiempo real de aliados

#### 🔗 Dependencias

- `InputSystem` (⌨️ tecla `Tab`)
- `CanvasLayeredHUDSystem` (🎛️ switching de HUD)
- `BattleHUDDataStream` (📡 ECS -> UI)

#### 🎨 Requisitos Visuales

- 🧭 Minimapa con mayor zoom que el minimapa de HUD estándar.
- 🖼️ Íconos diferenciados por función: 🧍 jugadores, ⛽ supply, 🎯 captura.
- 🔍 Legibilidad asegurada en resoluciones desde 1280x720.

## 🔐 10. Seguridad y Backend (Para expansión futura)

---

### 🟡 10.1 Estado actual (Solo Local – MVP)

📌 **Descripción:**

Durante el MVP, todo el progreso del jugador (atributos, perks, escuadras, loadouts, etc.) se almacena **en archivos locales en disco**. No hay necesidad de conexión a servidores ni validación en red.

🧩 **Componentes clave:**

- `SaveManager`: sistema central encargado de leer/escribir archivos de progreso.
- `PlayerProgressData` (Scriptable/Serializable):
    - nivel
    - puntos de atributo asignados
    - perks desbloqueados
    - escuadras desbloqueadas
    - configuraciones de loadout
- `SaveSystem` (C#):
    - Guarda y carga archivos `.json` en `Application.persistentDataPath`.
    - Funciones: `SaveProgress()`, `LoadProgress()`, `ResetProgress()`.

🔁 **Interacción:**

- `SaveManager` se activa en:
    - Inicio del juego (carga datos)
    - Fin de partida (guarda experiencia, nivel, perks)
    - Menús de loadout/perks/escuadras (guarda al confirmar cambios)
- Integrado con `GameBootstrapSystem` y `BarrackSystem`.

🔒 **Consideraciones:**

- **No hay validación de integridad ni anti-trampa.**
- Datos pueden ser modificados por el usuario (fácilmente).
- La lógica del juego confía completamente en los datos locales durante el MVP.

---

### 📦 10.2 Recomendaciones para transición a Backend (Post-MVP)

📌 **Transición sugerida:**

Para una futura versión multijugador completa, el backend deberá gestionar:

- Login y autenticación (OAuth, JWT, etc.)
- Almacenamiento de progresión del jugador (niveles, perks, squads)
- Emparejamiento (matchmaking) en partidas PVP
- Validación de partidas, resultados y economía

🧩 **Servicios recomendados:**

| Necesidad | Recomendación |
| --- | --- |
| Backend general | [PlayFab](https://playfab.com/), [Firebase](https://firebase.google.com/), [GameLift] (AWS) |
| Login | Email + password / OAuth (Google/Steam) |
| Matchmaking | PlayFab Matchmaking, Photon Fusion, Unity Lobby |
| Progresión remota | Cloud Save con sincronización en login |
| Anti-cheat | Unity Client Authority con validación parcial en servidor |

🧩 **Migración futura del sistema local:**

- `SaveManager` debe tener una **interfaz abstracta (`ISaveProvider`)** con implementaciones:
    - `LocalSaveProvider`
    - `CloudSaveProvider`

Esto permite migrar el sistema sin alterar el resto del código.

---

### 🔐 10.3 Gestión Segura de Progresión (futuro)

📌 **Progresión segura implica:**

- Evitar que usuarios alteren su progreso fuera del juego.
- Validar toda modificación de datos desde el servidor.
- Detectar comportamientos anómalos (ej. subir 5 niveles de golpe).

🧩 **Recomendaciones:**

- Uso de tokens por sesión.
- Validación del progreso contra límites razonables (anticheat básico).
- Logs de acciones del jugador para revisión en caso de errores o abuso.
- Evitar usar PlayerPrefs para datos críticos, incluso en MVP.

---

✅ **Resumen para el MVP:**

- Solo datos locales (JSON).
- No se usa backend real.
- No hay autenticación.
- Se deja abierta la arquitectura para expansión con `ISaveProvider`.

---

## ⚙️ 11. Extras Técnicos

---

### 🎖️ 11.1 Sistema de Liderazgo (Restricciones en Loadouts)

📌 **Descripción:**

Cada escuadra tiene un coste de liderazgo (1–3 puntos). El héroe tiene un valor base que limita cuántas escuadras puede llevar activas en su loadout. Este sistema restringe combinaciones y promueve decisiones tácticas.

🧩 **Componentes clave:**

- `HeroLeadershipComponent`:
    - `currentLeadership: int`
    - `maxLeadership: int`
- `SquadMetadata` (ScriptableObject):
    - `leadershipCost: int`
- `LoadoutSystem`:
    - Valida el total de liderazgo al seleccionar escuadras.
    - Previene guardar loadouts que excedan el límite.

🔁 **Interacción:**

- La UI de preparación de batalla muestra el total usado vs máximo.
- Se comunica con el sistema de selección de escuadras y perks (algunos perks aumentan `maxLeadership`).

---

### 💨 11.2 Sistema de Estamina y Gasto por Acción

📌 **Descripción:**

El héroe utiliza estamina al ejecutar ataques, sprints y habilidades. La estamina se regenera fuera de combate. Cada acción tiene un coste definido.

🧩 **Componentes clave:**

- `StaminaComponent`:
    - `currentStamina: float`
    - `maxStamina: float`
    - `regenRate: float`
- `HeroStaminaSystem`:
    - Reduce `currentStamina` según acción (sprint, ataque, habilidad).
    - Impide acciones si no hay suficiente estamina.
    - Regenera estamina si el jugador no actúa ofensivamente por cierto tiempo.
- `StaminaUsageProfile` (ScriptableObject):
    - Define cuánto cuesta cada tipo de acción por clase.

🔁 **Interacción:**

- Integrado con `HeroInputSystem`, `CombatSystem`, `AbilitySystem`.
- El HUD muestra barra de estamina y grises cuando está agotada.
- `CooldownSystem` y `StaminaSystem` deben estar sincronizados.

---

### 🧱 11.3 Visualización de Formaciones y Selección de Unidades

📌 **Descripción:**

Formaciones afectan el comportamiento y disposición espacial de las unidades. Deben poder activarse por hotkeys (`F1`, `F2`, etc.) o UI, y verse claramente en el terreno.

🧩 **Componentes clave:**

- `FormationComponent`:
    - `currentFormation: enum {Line, Dispersed, Testudo, Schiltron}`
    - `formationPattern: Vector3[]`
- `FormationSystem`:
    - Calcula las posiciones relativas de cada unidad según la formación activa.
    - Reorganiza el `LocalToWorld` de cada unidad cuando se cambia de formación.
- `FormationVisualizer` (MonoBehaviour):
    - Renderiza íconos, siluetas o líneas guía sobre el terreno.
    - Usado en modo táctico ligero (ALT) o al apuntar orden.

🔁 **Interacción:**

- Coordina con `SquadCommandSystem` (para aplicar la orden).
- La UI muestra formaciones disponibles para la escuadra activa.
- Respeta obstáculos del terreno (usando NavMesh o RaycastDown).

---

### 🧠 11.4 Optimización de Escena y Assets (Nivel MVP)

📌 **Descripción:**

Para asegurar buen rendimiento durante el MVP, se aplican prácticas básicas de optimización de escena y contenido.

🧩 **Prácticas aplicadas:**

- `GPU Instancing` en materiales de escuadras.
- `LOD Groups` para modelos 3D complejos (murallas, torres).
- `Occlusion Culling` en el mapa de combate.
- `Texture Atlas` para unidades que comparten materiales.
- `NavMesh` bakeado por zonas (NavMesh Surface segmentado).

🧩 **Componentes recomendados:**

- `PerformanceTrackerSystem`: muestra FPS, draw calls y memoria GC en tiempo real.
- `ObjectPoolSystem`: para proyectiles, habilidades y unidades temporales.
- `AsyncSceneLoader`: para evitar stutter al cambiar de escena.

🔁 **Interacción:**

- Directa con el sistema de renderizado y entidades de combate.
- Las formaciones, visualizadores y AI deben usar `EntityCommandBuffer` para optimizar instanciación y destrucción.

---

## 📘 12. Glosario Técnico (TDD)

> Este glosario resume los principales conceptos técnicos y arquitectónicos usados en la implementación del juego.
> 

---

### 🔧 Sistemas / Componentes

| Término | Descripción |
| --- | --- |
| **ECS (Entity Component System)** | Paradigma de programación basado en datos. Separa datos (`ComponentData`) de lógica (`SystemBase`). Optimiza rendimiento y escalabilidad en Unity DOTS. |
| **Netcode for GameObjects (Unity)** | Framework oficial para sincronización en red. Soporta sincronización de transform, RPCs y predicción. Utilizado para el MVP. |
| **ScriptableObject** | Objeto serializable de Unity usado para definir data externa editable por diseñador (perks, escuadras, atributos, etc.). |
| **FSM (Finite State Machine)** | Máquina de estados para controlar la lógica de flujo del héroe o escuadras (ej: `Idle` → `Combate` → `Retirada`). |
| **DynamicBuffer** | Buffer dinámico de datos dentro de una entidad ECS. Útil para almacenar múltiples objetivos, comandos o historial de órdenes. |

---

### ⚔️ Combate y Movimiento

| Término | Descripción |
| --- | --- |
| **DamageType** | Enum que representa el tipo de daño: `Contundente`, `Cortante`, `Perforante`. Usado en cálculos de daño. |
| **Penetration** | Valor que reduce la defensa del enemigo antes de aplicar daño. Definida por tipo de daño. |
| **FormationSystem** | Sistema que reordena posiciones de unidades dentro de una escuadra según una formación seleccionada. Usa `NavMesh` + `LocalToWorld`. |
| **StaminaSystem** | Controla el gasto y recuperación de estamina en el héroe. Interactúa con input, habilidades y UI. |
| **AbilityComponent** | Define datos de una habilidad (daño, tipo, coste de stamina, cooldown) y su ejecución. |
| **tacticalIntent** | Enum dentro de `SquadAIComponent`. Representa la intención táctica del escuadrón: atacar, reagruparse, defender, etc. No debe confundirse con `currentState`, que refleja el estado real actual del escuadrón. |

---

### 🧠 Escuadras e IA

| Término | Descripción |
| --- | --- |
| **SquadComponent** | Identifica una entidad como escuadra. Almacena formación activa, estado y referencia a unidades. |
| **UnitGroupAI** | Lógica que coordina el comportamiento grupal de unidades: mantenerse juntas, atacar en sincronía, evitar colisiones. |
| **RetreatLogicSystem** | Sistema que activa la retirada automática de una escuadra cuando el héroe está muerto. |
| **ZoneTriggerComponent** | Collider de tipo `trigger` para detectar si un héroe o escuadra entra en una zona especial (ej: supply point, punto de captura). |

---

### 🧱 UI y Escenarios

| Término | Descripción |
| --- | --- |
| **HUD (Heads-Up Display)** | Superposición visual durante la partida. Muestra vida, habilidades, estamina, minimapa, escuadra activa. |
| **Loadout** | Conjunto predefinido de arma, perks y escuadras que puede equipar el jugador antes de entrar a combate. Validado por sistema de liderazgo. |
| **Minimapa** | Mapa en tiempo real en UI que muestra puntos de captura, supply points y aliados. Actualizado por sistema de radar/posición. |
| **SceneLoaderSystem** | Encargado de cargar o descargar escenas Unity (feudo, combate, etc.) de forma asíncrona y sin bloqueos. |
| **FormationVisualizer** | Renderiza líneas o íconos en el terreno para indicar la posición deseada de las unidades. Usa datos de `FormationComponent`. |

---

### 💾 Guardado y Expansión

| Término | Descripción |
| --- | --- |
| **SaveManager** | Módulo que guarda y carga el progreso del jugador desde disco (JSON local para MVP). |
| **ISaveProvider** | Interfaz que permite intercambiar entre guardado local y en nube (ej: para post-MVP). |
| **PlayerProgressData** | Estructura que almacena nivel, perks, escuadras desbloqueadas, loadouts, puntos de atributo. |
| **CloudSave (futuro)** | Alternativa a almacenamiento local, donde los datos son sincronizados con un servidor seguro. |