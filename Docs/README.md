
# Conquest Tactics Documentation

Bienvenido a la documentación oficial de **Conquest Tactics**, desarrollado actualmente en Unity 6000.5.8f1 con ECS (Entities 6.5.0), según la configuración del repositorio.

Este repositorio documenta todos los aspectos técnicos y de diseño del juego, incluyendo la arquitectura de sistemas, persistencia de datos, guías para desarrolladores, detalles de implementación y procesos de refactorización. Aquí encontrarás recursos para entender cómo funciona el juego internamente, cómo extenderlo y cómo contribuir de forma efectiva.

La fuente canónica del estado implementado es `Arquitectura/1_Arquitectura_Actual.md`, complementada por las fases numeradas y `TroopMovementPipeline.md`. `GDD.md` expresa diseño de producto y `TDD.md` conserva también diseño técnico histórico; una afirmación de esos documentos no implica por sí sola que esté implementada.

La documentación está organizada por temas clave, cubriendo desde la estructura de datos y sistemas principales, hasta guías prácticas para agregar nuevas funcionalidades, realizar pruebas y mantener la calidad del código.

## Índice de Documentos

### Arquitectura

> Los registros fechados del 2 al 21 (septiembre y octubre) se retiraron el 2026-11-23 y siguen en git. El estado actual está en `1_Arquitectura_Actual.md`; los documentos del 22 al 36 cuentan la red y la migración de BronzeAgeClient.

- [Arquitectura/1_Arquitectura_Actual.md](./Arquitectura/1_Arquitectura_Actual.md):
  Referencia versionada y verificada de la arquitectura de datos actual: persistencia, catálogos,
  `BattleData`, puente entre escenas, entidades ECS, relaciones e inexistencia del contrato de red.
- [Hoja_De_Ruta.md](./Hoja_De_Ruta.md):
  Qué falta para que Conquest sea el juego, en qué orden y qué hay decidido. Documento vivo.

### Mecánicas

- [Mechanics/SkillsAndPerks.md](./Mechanics/SkillsAndPerks.md): habilidades por fases, enfriamientos y perks.
- [Mechanics/DamageMechanics.md](./Mechanics/DamageMechanics.md): daño, mitigación y pila de modificadores de stats.
- [Mechanics/Formations.md](./Mechanics/Formations.md), [FormationPostures.md](./Mechanics/FormationPostures.md), [FormationMilestoneSystem.md](./Mechanics/FormationMilestoneSystem.md): formaciones por datos, posturas e hitos.
- [Mechanics/BodyblockSystem.md](./Mechanics/BodyblockSystem.md), [UnitEngageSystem.md](./Mechanics/UnitEngageSystem.md): choque entre enemigos y navegación de combate.
- [Mechanics/SquadSwap.md](./Mechanics/SquadSwap.md), [HeroDeath.md](./Mechanics/HeroDeath.md): cambio de escuadra y muerte del héroe.
- [Mechanics/CapturePoints.md](./Mechanics/CapturePoints.md), [MatchTimer.md](./Mechanics/MatchTimer.md): captura y reloj de partida.
- [TroopMovementPipeline.md](./TroopMovementPipeline.md): pipeline de movimiento de tropas.
- [RemoteHeroAI_Architecture.md](./RemoteHeroAI_Architecture.md): IA de héroes remotos.

### Diseño

- [Design/Aprendizajes_Referencia.md](./Design/Aprendizajes_Referencia.md): patrones tomados de la referencia, por fases.
- [Design/Movimiento_Formaciones.md](./Design/Movimiento_Formaciones.md): modelo de movimiento y formaciones, decisiones y medidas.
- [Design/ia-tactica/README.md](./Design/ia-tactica/README.md): diseño de la IA táctica.

### Por abordar

- [Por abordar](./Por_Abordar/README.md):
  Lo que los planes dejan fuera a propósito, con el porqué y cómo retomarlo (formaciones, posturas y movimiento).

### Referencias técnicas

- [ConfiguracionPrefabs_ECS_Visual.md](./ConfiguracionPrefabs_ECS_Visual.md):
  Guía para configurar prefabs visuales y su integración con ECS.
- [Existing_Databases.md](./Existing_Databases.md):
  Documentación sobre las bases de datos existentes en el proyecto.
- [GDD.md](./GDD.md):
  Diseño de producto y comportamiento objetivo; no es inventario de implementación.
- [Hero_detail_prefab_structure.md](./Hero_detail_prefab_structure.md):
  Estructura interna de cómo están configurados los prefabs de detalle del héroe.
- [ModeloHybrido.md](./ModeloHybrido.md):
  Contrato vigente del modelo híbrido visual/ECS y sus autoridades físicas.
- [ScriptableObjects_Architecture.md](./ScriptableObjects_Architecture.md):
  Arquitectura y manejo de los datos del juego utilizando Scriptable Objects.
- [squad_prefab_relationships.md](./squad_prefab_relationships.md):
  Documentación de las relaciones entre prefabs de escuadrones y su configuración.
- [Mechanics/HeroDeath.md](./Mechanics/HeroDeath.md):
  Comportamiento implementado de muerte, retirada inmediata, persistencia de supervivientes y respawn.
- [TDD.md](./TDD.md):
  Diseño técnico histórico y objetivo; consultar arquitectura actual para implementación vigente.
- [tooltip_prefab_structure.md](./tooltip_prefab_structure.md):
  Guía técnica enfocada en la estructura base de los prefabs de tooltip de la interfaz.

### Guías de Creación

- [AgregarNuevaUnidad_Guia.md](./Guides/AgregarNuevaUnidad_Guia.md):
  Cómo crear y agregar una nueva unidad al juego paso a paso.
- [Crear Consumible.md](./Guides/Crear%20Consumible.md):
  Pasos para crear e integrar un nuevo ítem de tipo consumible.
- [Crear_Armor_Equipamiento.md](./Guides/Crear_Armor_Equipamiento.md):
  Guía con los pasos para crear piezas de armadura equipables en los personajes.
- [Crear_NPC_Interactivo.md](./Guides/Crear_NPC_Interactivo.md):
  Documentación detallada para diseñar e incluir NPCs interactivos en los mapas.
- [Crear_Weapon_Equipamiento.md](./Guides/Crear_Weapon_Equipamiento.md):
  Tutorial técnico para integrar nuevas armas en el sistema de juego.
- [Spawn_Heroe_Guia.md](./Guides/Spawn_Heroe_Guia.md):
  Guía para la creación y spawn de héroes en el sistema de batalla.

## Uso

Consulta cada documento según el área específica que desees repasar, investigar o mejorar dentro del proyecto.

---

> Documentación de Conquest Tactics, revisada contra el código el 2026-10-22.
