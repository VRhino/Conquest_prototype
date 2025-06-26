# TDD

## Índice Técnico (TDD)

### 1. Arquitectura General del Proyecto

- 1.1 Versión y configuración de Unity
- 1.2 Render Pipeline: elección y justificación
- 1.3 Estructura general de escenas
- 1.4 Modularidad y separación por sistemas
- 1.5 Arquitectura ECS con Unity DOTS
- 1.6 Integración con Netcode for GameObjects

### 2. Control del Jugador y Cámara

- 2.1 Movimiento y control del héroe (TPS)
- 2.2 Control de cámara según estado del héroe
- 2.3 Comandos a escuadras (hotkeys, UI, radial)
- 2.4 Feedback visual y navegación(x)
- 2.5 Modo espectador tras muerte(x)

### 3. IA de Escuadras y Unidades

- 3.1 Sistema de navegación (NavMesh)
- 3.2 Comportamiento en formación reactivo
- 3.3 IA de escuadra grupal vs individual
- 3.4 Coordinación de habilidades de escuadra
- 3.5 FSM para estados de escuadras y transición a retirada

### 4. Construcción de Mapas y Escenarios

- 4.1 Herramientas para creación de mapas (Unity Terrain / externos)
- 4.2 Implementación de elementos destructibles (puertas, obstáculos)
- 4.3 Sistema de zonas y triggers físicos (Supply, captura, visibilidad)
- 4.4 Configuración del mapa MVP y puntos claves

### 5. Sistema de Combate y Daño

- 5.1 Combate del héroe (colliders y animaciones)
- 5.2 Combate de escuadras (detección y ataques sincronizados)
- 5.3 Tipos de daño y resistencias (blunt, slashing, piercing)
- 5.4 Cálculo de daño y penetración en C#
- 5.5 Gestión de cooldowns y tiempos de habilidad

### 6. Flujo de Partida

- 6.1 Transiciones entre escenas (Feudo → Preparación → Combate → Post)
- 6.2 Ciclo de vida del héroe (muerte, respawn, cooldown)
- 6.3 Estado y retirada de escuadra al morir el héroe
- 6.4 Reglas del sistema de captura y uso de supply points
- 6.5 Asignación de spawn inicial

### 7. Progresión y Guardado de Datos

- 7.1 Progresión del héroe (nivel, atributos, perks)
- 7.2 Guardado local en MVP
- 7.3 Estructura de ScriptableObjects para perks y escuadras
- 7.4 Sistema de perks: carga, activación y visualización

### 8. Multijugador (MVP)

- 8.1 Arquitectura de red: servidor dedicado
- 8.2 Sincronización de escuadras y héroes (Snapshots o comandos, decisión final)
- 8.3 Interpolación de movimiento y predicción
- 8.4 Comunicación entre jugadores (chat básico)
- 8.5 Cambios de escuadra desde supply points (restricciones de sincronización)

### 9. UI y HUD

- 9.1 Sistema de UI (Canvas con Unity UI)
- 9.2 HUD de batalla: salud, habilidades, escuadra, órdenes
- 9.3 Minimapa dinámico (feudo y combate)
- 9.4 Interfaz de preparación y loadouts
- 9.5 Menús de interacción con supply y puntos de captura

### 10. Seguridad y Backend (Para expansión futura)

- 10.1 Estado actual (solo local)
- 10.2 Recomendaciones para transición a backend (login, matchmaking, almacenamiento)
- 10.3 Gestión segura de progresión futura

### 11. Extras Técnicos

- 11.1 Sistema de liderazgo (restricciones en loadouts)
- 11.2 Sistema de estamina y gasto por acción
- 11.3 Visualización de formaciones y selección de unidades
- 11.4 Optimización de escena y assets (nivel MVP)

### 12. Glosario tecnico

---

## 1. 🧱 Arquitectura General del Proyecto

### 1.1 Motor y Versión

- **Motor:** Unity
- **Versión:** Unity 2022.3.6f1 (LTS)

### 1.2 Render Pipeline

- **Pipeline:** URP (Universal Render Pipeline)
- **Justificación:**
    - Buen balance entre rendimiento y calidad visual.
    - Ideal para entornos con escuadras numerosas.
    - Compatible con dispositivos de gama media.

### 1.3 Arquitectura Técnica

- **Paradigma Base:** ECS (Entity Component System)
- **Implementación:** Unity Entities 1.0 (DOTS)
- **Justificación:**
    - Escalabilidad con múltiples unidades en pantalla.
    - Separación clara entre lógica y datos.
    - Rendimiento optimizado para combate en masa.

### 1.4 Organización Modular por Escenas

- El proyecto se divide en **múltiples escenas funcionales**:
    - **Login / Selección de personaje**
    - **Feudo (hub social)**
    - **Barracón (gestión de escuadras)**
    - **Preparación de batalla**
    - **Mapa de batalla**
    - **Post-partida**

> Cada escena tiene su propio sistema de UI, lógica de flujo y referencia a sistemas compartidos.
> 

### 1.5 Networking

- **Solución de red:** Unity Netcode for GameObjects (con ECS wrapper donde sea necesario).
- **Topología:** Cliente-servidor con servidor dedicado.
- **Estado sincronizado:**
    - Posición y estado de héroes.
    - Posición, formaciones y acciones de escuadras.
    - Eventos de combate y habilidades.
- **Autoridad:** Cliente predice, servidor valida.
- **Interpolación:** Movimiento interpolado con buffers de posición para héroes y escuadras.

---

## 🎮 Sección 2: Control del Jugador y Cámara

---

### 🎮 2.1 Control del Héroe

### 🎯 Descripción General:

El jugador controla directamente al **héroe** en tercera persona durante la batalla. El movimiento, ataques y uso de habilidades son de estilo **action-RPG táctico**, similar a *Conqueror’s Blade*.

### 🧩 Componentes Principales:

- `HeroControllerSystem` (SystemBase):
    - Sistema de movimiento basado en **EntityCommandBuffer** y **Input System**.
    - Controla desplazamiento (`WASD`), salto (`Space`), sprint (`LeftShift`), y bloqueo de movimiento si está aturdido o muerto.
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

### 🎥 2.2 Cámara

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

### 🛡️ 2.3 Control de Escuadras

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

🤖 3. IA de Escuadras y Unidades

---

🧭 3.1 Sistema de Navegación (NavMesh)

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

### 🧱 3.2 Comportamiento Reactivo en Formación

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

### 👥 3.3 IA de Escuadra Grupal vs Individual

### 📌 Descripción:

El comportamiento es **grupal**, pero con **unidad mínima de decisión** en cada soldado (solo para microacciones: evasión, rotación, targeting).

### 🧩 Componentes:

- `SquadAIComponent`:
    - `state`: enum general (Idle, Atacando, Reagrupando, Defendiendo, Retirada)
    - `targetEntity`: enemigo actual
    - `isInCombat`: bool
- `UnitCombatComponent`:
    - Posición relativa
    - `attackCooldown`
    - Estado local (cubierto, flanqueado, suprimido)

### 🧩 Sistemas:

- `SquadAISystem`:
    - Lógica de toma de decisiones grupal
    - Inicia combate si enemigo dentro de rango
    - Cambia formación si está siendo flanqueado
- `UnitTargetingSystem`:
    - Asigna enemigo cercano a cada unidad
    - Maneja “sobretargeting” (más de 3 soldados contra 1 objetivo = redistribución)
- `UnitAttackSystem`:
    - Verifica cooldowns
    - Ejecuta animaciones de ataque si tiene target
    - Usa `criticalChance` del arma para aplicar golpes críticos de 1.5x

---

### 🧠 3.4 Coordinación de Habilidades de Escuadra

### 📌 Descripción:

Habilidades de escuadra se ejecutan de forma **coordinada y sincronizada**, basadas en señales del jugador (hotkey) y condiciones tácticas (posición, formación, enemigos).

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

---

### 🔁 3.5 FSM para Estados de Escuadras y Transición a Retirada

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

### 🛠️ 4.1 Herramientas para Creación de Mapas

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

### 🚪 4.2 Implementación de Elementos Destructibles

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

### 📦 4.3 Sistema de Zonas y Triggers Físicos

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

### 🗺️ 4.4 Configuración del Mapa MVP y Puntos Claves

### 📌 Descripción:

El MVP incluye **un único mapa simétrico asimétrico**, con elementos específicos:

- 2 puntos de spawn (por bando)
- 2 supply points por lado
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
    
    ### 🏳️ 4.4.1 Puntos de Captura
    
    ### 📌 Descripción:
    
    Los puntos de captura son zonas estratégicas que deben ser conquistadas por el bando atacante para avanzar y ganar la partida. Su funcionamiento es diferente al de los supply points:
    
    - **Propiedad inicial:** Todos los puntos de captura pertenecen al bando defensor al inicio de la partida.
    - **Captura irreversible:** Una vez que un punto de captura es conquistado por el bando atacante, no puede ser recuperado por el bando defensor durante esa partida.
    - **Desbloqueo secuencial:** Algunos puntos de captura están bloqueados al inicio y solo se pueden capturar si se ha conquistado previamente el punto anterior (precondición). Un punto bloqueado no puede ser capturado hasta que se desbloquee.
    - **Punto de base:** Si el atacante conquista el punto de base, la partida termina inmediatamente con la victoria del bando atacante.
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
        - Actualiza captura si cumple condiciones (nadie del bando propietario presente y el punto está desbloqueado)
        - Al completarse la captura, si el punto desbloquea otro, lo activa
        - Si es un punto de base y es capturado, termina la partida
    - `CaptureProgressUISystem`:
        - Sincroniza HUD de progreso
        - Envía eventos de captura completada
    
    ### 🧩 Interacción:
    
    - El HUD recibe cambios de color, íconos o tiempo.
    - El resultado de la captura puede desbloquear zonas (ej.: Base se desbloquea tras capturar A/B).
    - Los puntos de captura no pueden ser recuperados por el bando defensor una vez perdidos.
    - Los supply points pueden cambiar de dueño varias veces durante la partida, pero los puntos de captura no.
    
    ---
    
    ### 🩺 4.4.2 Supply Points
    
    ### 📌 Descripción:
    
    Zonas pasivas que permiten curar al héroe/squad y cambiar de escuadra si no están en disputa.
    
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
    
    ### 🧭 4.4.3 Spawn Points
    
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

### 🧍 5.1 Combate del Héroe (Colliders y Animaciones)

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

### 🪖 5.2 Combate de Escuadras (Detección y Ataques Sincronizados)

📌 **Descripción:**

Las escuadras atacan como **entidad colectiva**. Las unidades detectan enemigos en su rango de ataque, y ejecutan ataques por intervalos. El daño se calcula por **unidad**, pero la ejecución es **coordinada desde el squad**.

🧩 **Componentes clave:**

```csharp
SquadCombatComponent (IComponentData)
- attackRange: float
- attackInterval: float
- attackTimer: float
- targetEntities: DynamicBuffer<Entity>
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

### ⚔️ 5.3 Tipos de Daño y Resistencias

📌 **Descripción:**

Todo daño en el juego es de tipo:

- `Blunt` (Contundente)
- `Slashing` (Cortante)
- `Piercing` (Perforante)

Cada unidad tiene defensas diferenciadas por tipo y los ataques tienen **penetraciones específicas** que ignoran parte de esa defensa.

🧩 **Componentes:**

```csharp
csharp
CopiarEditar
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
csharp
CopiarEditar
DefenseComponent (IComponentData)
- bluntDefense: float
- slashDefense: float
- pierceDefense: float

```

```csharp
csharp
CopiarEditar
PenetrationComponent (IComponentData)
- bluntPenetration: float
- slashPenetration: float
- piercePenetration: float

```

🔁 **Interacción:**

- Leídos por `DamageCalculationSystem` cuando un ataque impacta.
- El tipo de daño determina qué defensa y qué penetración se aplican.

---

### 🧮 5.4 Cálculo de Daño y Penetración (Lógica en C#)

📌 **Fórmula básica de cálculo:**

```csharp
float CalculateEffectiveDamage(float baseDamage, float defense, float penetration)
{
    float mitigatedDefense = Mathf.Max(0, defense - penetration);
    return Mathf.Max(0, baseDamage - mitigatedDefense);
}
```

🧩 **Sistemas involucrados:**

```csharp
DamageCalculationSystem
- Lee DamageProfile, DefenseComponent y PenetrationComponent
- Aplica daño resultante a HealthComponent
 - Aplica multiplicador 1.5f si el `DamageCategory` es `Critical`

HealthComponent (IComponentData)
- maxHealth: float
- currentHealth: float
```

- Si `currentHealth <= 0`, se notifica a `DeathSystem`
- Puede desencadenar animación de muerte, retirada de unidad, etc.

---

### ⏱️ 5.5 Gestión de Cooldowns y Tiempos de Habilidad

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

## 🔄 6. Flujo de Partida

---

### 🧭 6.1 Transiciones entre Escenas

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

### ☠️ 6.2 Ciclo de Vida del Héroe (Muerte, Respawn, Cooldown)

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

### 🪖 6.3 Estado y Retiro de Escuadra al Morir el Héroe

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

### 🏳️ 6.4 Reglas del Sistema de Captura y Uso de Supply Points

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
SupplyCaptureSystem
- Detecta héroes enemigos en zona sin defensores
- Inicia barra de captura
- Interrumpe captura si entra defensor
- Al capturar, cambia `ownerTeam`

SupplyInteractionSystem
- Si supply está en estado aliado y sin disputa:
    - Permite cambiar escuadra activa
    - Cura pasivamente a héroe y escuadra en área
```

🔁 **Interacción:**

- Se conecta con `HeroInputSystem` (para menú de cambio)
- `HUD` muestra progreso de captura si el jugador está en rango

---

### 📍 6.5 Asignación de Spawn Inicial

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

## 🧬 7. Progresión y Guardado de Datos

---

### 🧠 7.1 Progresión del Héroe (Nivel, Atributos, Perks)

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

### 💾 7.2 Guardado Local en MVP

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

### 📁 7.3 Estructura de ScriptableObjects para Perks y Escuadras

📌 **Descripción:**

Los perks y escuadras estarán definidos como **ScriptableObjects**, facilitando su edición y expansión sin tocar código.

🧩 **Ejemplos:**

```csharp
[CreateAssetMenu(menuName = "Perks/PerkData")]
public class PerkData : ScriptableObject {
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

### 🧠 7.4 Sistema de Perks: Carga, Activación y Visualización

📌 **Descripción:**

El sistema de perks es un **árbol modular**. El jugador puede activar hasta `5 pasivos` y `2 activos`. Se cargan desde ScriptableObjects y aplican efectos en combate o fuera de él.

🧩 **Componentes:**

```csharp
PerkComponent
- List<PerkData> activePerks
- List<PerkData> passivePerks
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

- Perks activos están ligados a teclas (`Q`, `E`)
- Consumen stamina y entran en cooldown

📌 **Sinergia:**

- Perks pasivos modifican atributos del héroe o su escuadra
- Algunos perks se activan automáticamente según condiciones (ej. “+mitigación si no te mueves”)

🔁 **Interacción:**

- `CombatSystem` accede a buffs de perks en tiempo real
- `SquadSystem` consulta perks que afectan estadísticas o comportamiento
- `HUD` representa el estado de cada perk con íconos, cooldown, y tooltips

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

- `PerformanceTrackerSystem` (opcional): muestra FPS, draw calls y GC.
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