# Contraste del handoff de IA táctica con Conquest Tactics

> **Registro histórico (2026-10-14).** Describe el estado y las decisiones de esa fecha; no se reescribe. Para el estado vigente, ver [Arquitectura actual](1_Arquitectura_Actual.md) y, para movimiento, [TroopMovementPipeline](../TroopMovementPipeline.md). Los nombres de sistemas o valores citados aquí pueden haber cambiado.

Fecha: 2026-10-14. Análisis estático del árbol de trabajo actual, incluyendo cambios locales. No se modificó código ni se ejecutaron nuevas pruebas de Unity.

Fuente de diseño: `C:/Users/VRINO/Documents/Codex/2026-10-14/referenced-chatgpt-conversation-this-is-an/outputs/bronze-age-conquest-ia-handoff.html`.

Se consultaron el informe Graphify, ModeloHybrido, SystemResponsibilities, las secciones pertinentes del GDD/TDD, scripts de IA, órdenes, formación, navegación, combate, bootstrap y matchmaking, assets de tropas, tests y resultados guardados. HEAD: `22c6a785`, coincidente con el commit del grafo. Hay numerosos cambios locales: esa coincidencia no acredita que cada relación del grafo refleje el árbol de trabajo; las conclusiones siguientes se apoyan en los archivos actuales.

## 1. Dictamen

El handoff es una buena propuesta de comportamiento y coordinación, pero necesita adaptación antes de servir como especificación de implementación. El proyecto ya implementa buena parte de la ejecución héroe → escuadra → unidad. La carencia principal es la planificación cooperativa sobre esa base, junto con contratos que permitan expresar y comprobar una maniobra táctica.

Conviene conservar percepción/decisión/ejecución, el arbitraje único de órdenes y los propietarios actuales de anclas, slots, navegación, ataques y visuales. Añadir un comandante y grupos tiene sentido; crear nuevos HeroBrain, SquadBrain y UnitAI que repliquen esas responsabilidades introduciría autoridades competidoras.

El primer corte recomendado es un grupo de dos bots con infantería con escudo y arqueros, posiciones anotadas, preparación verificable, timeout y cancelación. Caballería, carga, habilidades activas y contramedidas de picas necesitan primero capacidades de combate que no están acreditadas como operativas en el código examinado.

## 2. Correcciones de contexto

| Tema | Evidencia del repo | Consecuencia para el handoff |
|---|---|---|
| Editor | `ProjectSettings/ProjectVersion.txt:1`: 6000.5.8f1 | Unity 6 sí coincide; AGENTS.md conserva Unity 2022.3.x y debe actualizarse en una tarea posterior. |
| Paquetes | `Packages/manifest.json` y `packages-lock.json`: Entities/Collections/Graphics 6.5.0, Burst 1.8.30, AI Navigation 2.0.14 | No diseñar contra Entities 1.3.14 de AGENTS ni asumir que los enlaces 1.4 del HTML representan las APIs instaladas. |
| ECS | `Docs/ModeloHybrido.md`, sistemas actuales | ECS ya es autoridad de gameplay. La propuesta de introducir ECS después no corresponde al punto de partida real. |
| Escala | `Logs/system-optimization-full-battle-profile.json`: 20 héroes y 240 unidades | 15 vs. 15 es objetivo del diseño, no escala ya validada. |
| Partida por defecto | `BattleDebugCreator.cs:19` crea cuatro atacantes remotos y cinco defensores y añade al héroe local | El camino normal de debug produce 5 vs. 5 cuando el jugador es atacante. No confundir ese fixture con un límite del motor. |
| Red | Netcode está instalado; `MatchmakingService.MatchmakingProcess` usa `BattleDebugCreator` y simula backend | No se encontró autoridad de servidor implementada en los scripts examinados. La presencia del paquete no resuelve esta decisión. |
| Modos/ciclo de vida | Captura, puntos de suministro, timer, spawn/respawn, retirada y cambio de escuadra ya tienen sistemas | Estas mecánicas dejan de ser incógnitas generales del handoff; falta acordar cómo debe utilizarlas la IA táctica. |

## 3. Mapa de reutilización

| Propuesta del HTML | Pieza encontrada / ubicación | Estado y recomendación |
|---|---|---|
| Blackboard de equipo | `Hero/AI/Components/TeamWorldState.Component.cs`, `Hero/AI/Systems/BattleWorldState.System.cs` | **EXTENDER parcialmente.** Ya ofrece vistas por equipo y snapshots. Mantenerlo como datos, sin decisiones. Memoria y asignaciones tienen responsabilidades distintas; no convertir el singleton en un gestor universal. |
| HeroBrain | `HeroAIPerception`, `HeroAIBlackboard`, `HeroAIRusher`, `HeroAIBalanced`, `HeroAIDecision` | **REUTILIZAR / EXTENDER.** Existe la frontera estable decisión → ejecución. Utility AI puede incorporarse como comportamiento compatible, garantizando un único decisor activo por héroe. |
| Ejecutor del héroe | `HeroAIExecution.System.cs`, `HeroAttack.System.cs` | **REUTILIZAR.** Navega, limita reintentos y publica órdenes; el ataque básico lee `shouldAttack`. Falta feedback táctico explícito. |
| SquadBrain | `SquadAI`, `CombatReaction`, `OrderResolution`, `SquadOrder`, `SquadFSM` | **EXTENDER.** Existe conducta reactiva y arbitraje; falta maniobra por rol, formación/orientación solicitadas y compromiso. La nueva planificación debe publicar peticiones, no escribir el estado resuelto. |
| UnitAI | `UnitTargeting`, `UnitFormationState`, `UnitNavMesh`, `UnitFollowFormation`, `UnitAttack`, `RangedAttack` | **REUTILIZAR.** Ya cubren selección local, slots, movimiento y ataques. Cada sistema conserva su responsabilidad. |
| Formación/readiness | `SquadAnchor`, `Formation`, `GridFormationUpdate`, `SquadNavigation`, `FormationStance` | **REUTILIZAR / EXTENDER.** Llegada y estados individuales son señales útiles; no hay readiness completo de grupo por cohesión, orientación, capacidad y ruta. |
| TeamCommander | Sin implementación encontrada en búsquedas de código y documentación | **CREAR cuando se implemente.** Responsabilidad nueva: prioridades y asignación del equipo. No colocarla en BattleWorldState. |
| TacticalGroups / EngagementManager | Sin implementación encontrada | **CREAR cuando se implemente.** No hay membresía/roles persistentes ni protocolo Assemble → Prepare → Commit → Resolve → Regroup. |
| TacticalMap / ThreatMap / reservas | Zonas de captura, NavMesh y `EnvironmentAwareness` son coincidencias parciales | **CREAR datos específicos reutilizando geometría y zonas.** No existe grafo táctico, mapa de amenaza ni reserva de huellas/corredores encontrada. |
| Datos de capacidades | `SquadDefinitionComponent`, `SquadDataComponent`, stats de armas/distancia/escudos, perfiles y librería de formaciones | **EXTENDER / REUTILIZAR.** Derivar capacidades reales; un nombre de tropa o enum no garantiza que tenga una habilidad operativa. |
| Servicios y utilidades | `HeroDataService`, `SquadDataService`, `DataCacheService`, `MapService`; `HeroPositionUtility`, `FormationPositionCalculator`, `UnitStatsUtility`, `VisualSyncUtility`, `VisualPrefabRegistry` | **REUTILIZAR en su ámbito.** Persistencia/definiciones, posición, formación, stats y visuales; ninguno equivale a coordinación táctica. MapService consulta la base de mapas, no un mapa de posiciones tácticas. |
| Inventario y eventos | Servicios de `Inventory/Services`, `BattlePreparationEvents`, eventos de captura y daño existentes | **REUTILIZAR los eventos pertinentes.** Inventario no es sede de decisiones tácticas; evitar duplicar daño, captura o ciclo de vida para notificar al comandante. |

Rutas de scripts relativas a `Assets/Scripts/` salvo indicación contraria. La ausencia se refiere a la implementación examinada, no a nombres que necesariamente habría que adoptar.

## 4. Brechas que condicionan la implementación

### 4.1. Percepción compartida no equivale todavía a conocimiento permitido completo

`EnemyDetection.System.cs` detecta por distancia desde el centroide de una escuadra a unidades y héroes enemigos. No aplica oclusión, campo visual ni memoria. `BattleWorldState` agrega contactos de todas las escuadras aliadas; los comparte por equipo sin latencia o alcance de comunicaciones.

Al perder contacto, la entidad deja de aparecer en la vista visible. No existen `observedAt`, confianza, última posición conocida o procedencia. Los snapshots de héroes enemigos visibles contienen salud/stamina exactas, orden/formación de escuadra y `currentDecision`. Además `TeamWorldState` expone ambas vistas y `EnemyOf`, por lo que el consumidor puede acceder a datos completos del otro equipo si el contrato no lo impide.

No se encontró un comportamiento actual usando `EnemyOf` para hacer trampas. El riesgo es de frontera de datos para futuros decisores. Balanced sí compara salud exacta del contacto visible. Hay que decidir qué atributos y estados son información legítima y publicar una representación enemiga acorde. Las zonas, su progreso y sus flags de captura son globales actualmente: definir si esa información es pública por regla del juego.

### 4.2. Las decisiones son prioridades fijas, no Utility AI cooperativa

Rusher y Balanced evalúan ramas `if` cada actualización. Balanced distingue atacante/defensor y ventaja de HP; ninguno realiza selección de roles, reservas o puntuación de planes de equipo. Ambos priorizan atacar al héroe más cercano cuando `isUnderAttack` y hay un héroe visible. Ese flag también se activa por proximidad a 20 m, sin requerir daño recibido.

Consecuencia: un enemigo cercano puede desviar bots de su misión aunque no haya atacado. Incorporar un comandante sin adaptar esa prioridad puede hacer que sus órdenes se abandonen enseguida. Deben definirse qué amenazas justifican una interrupción, cuánto compromiso conserva la misión y qué feedback se envía al grupo.

`SquadAI` tiene perfiles, pero Defensive y Harassing comparten la misma lógica de estado. AntiCharge publica Defending cuando está cohesionado; eso no constituye una contramedida física de carga.

### 4.3. El contrato de órdenes no expresa la maniobra propuesta

`SquadAIOrderIntentComponent` permite orden, target y posición de hold. No incluye formación deseada, orientación independiente, revisión de misión, caducidad, rol ni feedback. `OrderResolution.System.cs:130` conserva siempre la formación actual para escuadras remotas: la IA no solicita cambios de formación por este pipeline.

`SquadOrderSystem` toma la orientación del héroe al aplicar HoldPosition. Una posición de picas orientada hacia una aproximación no puede expresarse directamente con la interfaz actual. `HeroAIExecution.System.cs:199` publica `targetEntity = Entity.Null`; `UnitTargeting` selecciona y distribuye candidatos locales por su cuenta. Coordinar fuego o fijar un enemigo concreto exige adaptar ambos contratos respetando el propietario de targeting.

Las peticiones sobreviven a reacciones de combate y el resolvedor evita reemitir payloads sin cambios. Esa estabilidad ya existe; no equivale a órdenes versionadas con lease ni a acknowledgements de ejecución.

La reacción de combate gana a órdenes remotas que no sean HoldPosition. Un repliegue o reposicionamiento táctico debe expresar cuándo puede superar esa reacción, conservando las retiradas comprometidas y las restricciones de gameplay. No cambiar prioridades globalmente sin una regla explícita y regresiones del jugador local.

### 4.4. Formaciones existentes; adaptación de pasillos incompleta

Hay librería de formaciones, anclas, slots estables y tolerancias configurables. `SquadNavigation.HasArrived` usa los destinos efectivos proyectados al NavMesh, no solo el destino ideal. Eso sirve de base para medir preparación real.

`FormationAdaptationSystem` publica detección de obstáculos y `requiresAdaptation`, pero no cambia formaciones por diseño. `UnitFollowFormation` mantiene el consumidor como TODO. No se encontró un flujo operativo de columna automática en un estrechamiento y despliegue al salir. Tampoco reservas de superficie entre aliados. Bodyblock resuelve contacto entre equipos; no sustituye asignación de posiciones ni prioridad de paso entre escuadras aliadas.

### 4.5. Tropas y habilidades: separar catálogo de ejecución

El enum SquadType contiene Squires, Archers, Pikemen y Spearmen; UnitType incluye Cavalry. En `Assets/Resources/Squads` se encontraron definiciones Squires (12 unidades), Levy Archers (12) y Spearmen (20). No se encontró una definición de caballería en ese catálogo ni una mecánica específica de carga en los scripts buscados. Esto no acredita todos los prefabs o assets externos al catálogo.

Sí hay combate cuerpo a cuerpo, escudos con bloqueo/rotura y proyectiles con munición/recarga. `HeroAbility`, `HeroAbilityComponent` y su baking aportan datos; `SquadSkillComponent` y AbilityData también. No se encontró un sistema que active/ejecute esas habilidades desde dichos contratos. El ataque básico de héroe sí está conectado a la IA.

`BraceWeaponActivation.System.cs:20` fija `shouldBrace = false` y devuelve Brace a Normal. `BraceWeaponSystem` contiene overrides por fila, pero ese código no demuestra picas preparadas operativas contra una carga. Por tanto, cargar/flanquear con caballería, readiness de habilidades y picas anti-carga quedan fuera del primer corte hasta completar y validar sus reglas.

### 4.6. Cadencia y parámetros

Percepción usa `frameCount % 5`: equivale a 12 Hz a 60 FPS o 6 Hz a 30 FPS y sincroniza a todos los bots en el mismo frame. No es una cadencia temporal constante. Rusher/Balanced, snapshot y varios sistemas de escuadra siguen actualizándose cada frame.

Hay radios y umbrales hardcodeados en percepción y comportamientos. `SquadSpawnConfig` ya configura navegación, llegada, leash, targeting y bodyblock, pero no perfiles de decisión, memoria o compromiso. Al implementar, separar tunables de IA en configuración apropiada, reutilizar los parámetros de ejecución y escalonar reevaluaciones por tiempo de simulación. Las frecuencias del HTML son hipótesis, no motivo para desacelerar física/daño/animación indiscriminadamente.

### 4.7. Arquitectura híbrida: respetar propietarios y reconocer deuda existente

La matriz de ModeloHybrido es más específica que la idea genérica de servicios C# del HTML. Las decisiones de gameplay deben vivir en sistemas/contratos ECS; helpers C# puros pueden puntuar candidatos sin adquirir autoridad propia. NavMeshAgent sigue siendo un ejecutor gestionado y la pose confirmada vuelve por NavMeshPositionSync. EntityVisualSync y VisualPrefabRegistry ya resuelven el flujo visual.

Existe una excepción de hecho a la regla local de no mutar ECS desde GameObjects: `BattleSceneController.SpawnRemoteHero` instancia y configura entidades, tags de IA y blackboard mediante EntityManager. BattleBootstrap ya procesa otras peticiones en ECS. La nueva IA no debería replicar esa vía; trasladar el bootstrap remoto completo a su propietario ECS sería una corrección acotada previa a ampliarlo. Se ha identificado la deuda, no se ha corregido en este análisis.

## 5. Rendimiento y pruebas: evidencia previa, no resultados nuevos

El HTML declara que no había benchmarks al redactarse. El repo sí conserva mediciones y suites posteriores/disponibles para el contraste:

| Evidencia guardada | Resultado | Límite |
|---|---|---|
| `Logs/system-optimization-full-battle-result.json` | Una prueba completa Passed; flujo preparación → batalla | No demuestra calidad cooperativa ni 15 vs. 15. |
| `Logs/system-optimization-full-battle-profile.json` | 20 héroes, 240 soldados; 600 frames renderizados; contacto en 275 | Editor y test runner, 1920×1080; sin perfil Player/GPU comparable. |
| Mismo perfil, p95 | Hilo principal 22,7345 ms; detección 1,0366 ms; bodyblock 0,3244 ms | La muestra no acredita el objetivo 60 FPS ni un presupuesto total de IA de 2 ms. No atribuir todo el coste a IA. |
| Informe de optimización, prueba aislada | 900 agentes densos: bodyblock 4,18 ms paralelo | Muestra aislada de un frame; no extrapolar a batalla completa. |
| Tests existentes | Órdenes/hold/leash, formación y supervivientes, navegación fallida, daño/respawn, snapshots al terminar partida, targeting compartido | Se inspeccionó código/resultados guardados; no se ejecutaron de nuevo. |

La detección todavía recorre escuadras enemigas y sus unidades para cada escuadra. Tiene buffer compartido por squad y marcador de profiler, pero no se encontró índice espacial en esa detección. Conservar la instrumentación y medir antes de extender consultas tácticas. No hace falta migrar todo a ECS: ya lo está; el coste gestionado/NavMesh y las consultas concretas son los candidatos a estudiar.

## 6. Contratos y pipeline adaptados

Mantener el camino existente y añadir planificación sobre sus fronteras:

```text
Daño / muerte / detección / captura / estado de partida
  → BattleWorldState: vista del ciclo, estado táctico anterior
  → conocimiento permitido y memoria, sin decidir objetivos
  → comandante: misión y asignación por equipo
  → grupo/engagement: roles, reservas y estado de preparación
  → comportamiento del héroe: HeroAIDecision
  → HeroAIExecution: intención de escuadra y movimiento del héroe
  → OrderResolution → SquadOrder / FSM / targeting / ancla / formación
  → UnitNavMesh y combate → pose física confirmada y feedback
  → revisión del plan en el siguiente ciclo
```

Es un pipeline conceptual propuesto, no nuevos tipos ni atributos ya implementados. Debe conservar el orden real documentado en SystemResponsibilities: daño resuelto antes de decisiones, captura antes del snapshot, ataques sobre pose confirmada. El HTML coloca combate después de decisión de forma genérica; no usarlo para reordenar daño previo y romper ese contrato. Evitar un ciclo de dependencias al exigir readiness del mismo frame antes de ejecutar la formación que lo produce.

Extensiones mínimas a diseñar:

1. Intención de misión con identidad/revisión, equipo, objetivo, rol, área y expiración. Diferenciarla del enum existente `TacticalIntent`, que solo representa Idle/Attacking/Regrouping/Defending.
2. Petición operativa de escuadra con formación/orientación opcionales y políticas de reacción. Un único productor o resolvedor explícito evita que héroe y squad sobrescriban la misma intención.
3. Feedback leído desde ejecución: aceptada, preparando, lista, bloqueada, completada o cancelada, con revisión y motivo. `isNavigating = false` solo no demuestra readiness.
4. Contactos observados con atributos permitidos, última observación y expiración; reservas de posiciones liberadas al morir, cambiar de misión, cambiar escuadra o terminar partida.
5. Perfiles de capacidades comprobadas y configuración de evaluación/compromiso. No convertir un enum Cavalry o un asset AbilityData en una capacidad ejecutable ficticia.

## 7. MVP revisado y puertas de aceptación

| Fase | Trabajo concreto | Verificación necesaria |
|---|---|---|
| A: contratos | Configurar cadencias/tunables; formación/orientación AI; revisión y feedback; resolver autoridad del bootstrap remoto al extenderlo | Órdenes AI pasan por el mismo resolvedor; hold conserva orientación solicitada; jugador local conserva su arbitraje; rutas fallidas producen feedback. |
| B: dos bots | Squires como ancla y arqueros como apoyo; dos posiciones anotadas, reserva de huella, un objetivo y timeout | Los dos ocupan posiciones distintas; el héroe puede reposicionarse sin mover el hold; el grupo no declara listo a quien no ha llegado; muerte/bloqueo libera reserva y cancela o degrada el plan. |
| C: información y Utility | Definir reglas visibles, memoria y atributos permitidos; scoring de mantener/apoyar/reposicionar/retirarse con histéresis | Contacto perdido no sigue coordenadas exactas; el log explica candidato, motivo y cambio; proximidad sola no rompe una misión de manera indiscriminada. |
| D: objetivos y equipos mixtos | Distribuir bots entre capturas/defensa/supply; respetar presencia humana sin controlarla; grupos mayores | No mandar todos al objetivo más cercano; supply/respawn/cambio de squad invalidan asignaciones; todas las esperas tienen salida. |
| E: escala | Reutilizar fixture 10 vs. 10 y llevarlo a 15 vs. 15; variar efectivos y densidad; ejecutar en Player | Medir p95/p99 por capa, rutas, físicas, animación y memoria; comparar el mismo escenario con y sin coordinación. 360 soldados solo es un caso de 30×12, no el máximo del juego. |
| F: ampliar combate | Habilidades operativas, brace/picas y caballería/carga, cuando existan y estén probadas | Precondiciones y contrajuego reales, ventanas de commit, cancelación compatible con animación/combate; después incorporar sinergias avanzadas. |

No reconstruir UnitAI ni el motor de formaciones en A/B. Corregir fallos de ejecución demostrados mediante los escenarios, y mantener la lógica nueva acotada a peticiones, coordinación y feedback.

## 8. Decisiones que siguen abiertas

- Modelo de red y autoridad final; diferenciar bot de verdadero jugador remoto antes de integrar sesiones en red. Hoy todos los héroes creados por el camino remoto reciben HeroAITag.
- Tamaño máximo de escuadra/efectivos simultáneos y hardware objetivo. Las definiciones y el fixture no fijan el máximo del producto.
- Reglas de percepción: visión/oclusiones, conocimiento de salud/stamina/órdenes, información pública de captura y comunicaciones entre aliados.
- Política para interrumpir misiones, retirarse sin quedar atrapado por la reacción de combate y apoyar a humanos.
- Capacidad real que tendrá cada tropa/habilidad y cuándo caballería/picas entran en el alcance.
- Configuración del mapa táctico y validación de ancho, orientación, cobertura, líneas de tiro y huella de la formación.

Las fases y contratos de este informe son recomendaciones de integración. No acreditan implementación, validación dinámica ni autorización para modificar el juego.
