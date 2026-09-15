
# Conquest Tactics Documentation

Bienvenido a la documentación oficial de **Conquest Tactics**, desarrollado actualmente en Unity 6000.5.8f1 con ECS (Entities 6.5.0), según la configuración del repositorio.

Este repositorio documenta todos los aspectos técnicos y de diseño del juego, incluyendo la arquitectura de sistemas, persistencia de datos, guías para desarrolladores, detalles de implementación y procesos de refactorización. Aquí encontrarás recursos para entender cómo funciona el juego internamente, cómo extenderlo y cómo contribuir de forma efectiva.

La documentación está organizada por temas clave, cubriendo desde la estructura de datos y sistemas principales, hasta guías prácticas para agregar nuevas funcionalidades, realizar pruebas y mantener la calidad del código.

## Índice de Documentos

### Arquitectura

- [Limpieza de estado muerto en movimiento](./Arquitectura/13_Limpieza_Estado_Movimiento_2026-09-15.md):
  Componentes sin lectores retirados y referencia del pipeline actual corregida.

- [Detección y targeting a escala](./Arquitectura/12_Deteccion_Targeting_Escala_2026-09-15.md):
  Candidatos compartidos por squad, eliminación de copias por unidad y medición con 900 unidades.

- [Escala de bodyblock con 900 agentes](./Arquitectura/11_Escala_Bodyblock_900_Agentes_2026-09-14.md):
  Pool de buckets, frame caliente sin GC y medición reproducible.

- [Movimiento del héroe y autoridad física](./Arquitectura/10_Movimiento_Heroe_Autoridad_Fisica_2026-09-14.md):
  Autoridad local/remota, destinos IA y contrato de colisiones por equipo.

- [Destinos NavMesh y fallos de path](./Arquitectura/9_Destinos_NavMesh_Fallos_Path_2026-09-14.md):
  Proyección navegable, destino efectivo, fallback y reintento controlado.

- [Bodyblock y orden físico](./Arquitectura/8_Bodyblock_Orden_Fisico_2026-09-14.md):
  Fuerza independiente de FPS, solapamiento exacto y sincronización posterior a la corrección.

- [Formación, geometría y estabilidad temporal](./Arquitectura/7_Formacion_Geometria_FPS_2026-09-14.md):
  Centro fraccional, slots persistentes, estado por unidad y comportamiento independiente de FPS.

- [Retirada por muerte del dueño](./Arquitectura/6_Retirada_Muerte_Dueno_2026-09-14.md):
  Decisión persistente, espera de destino, supervivientes y redespliegue sin duplicar instancias.

- [Intercambio y retirada por reemplazo](./Arquitectura/5_Intercambio_Retirada_2026-09-14.md):
  Validación previa, reservas, marcador activo y llegada de todos los supervivientes.

- [Ciclo de vida del héroe local](./Arquitectura/4_Ciclo_Vida_Heroe_2026-09-14.md):
  Salud, intención de movimiento inhibida, respawn y revisión de posicionamiento.

- [Control de escuadras: reparación por fases](./Arquitectura/3_Control_Escuadras_2026-09-14.md):
  Órdenes de IA, formación, mantener posición, validación y pendientes del movimiento.

- [Reparaciones y validación — 2026-09-14](./Arquitectura/2_Reparaciones_Arquitectura_2026-09-14.md):
  Correcciones aplicadas, pruebas reproducibles y trabajo pendiente.

- [Arquitectura/1_Arquitectura_Actual.md](./Arquitectura/1_Arquitectura_Actual.md):
  Referencia versionada y verificada de la arquitectura de datos actual: persistencia, catálogos,
  `BattleData`, puente entre escenas, entidades ECS, relaciones e inexistencia del contrato de red.
- [Coordinacion/README.md](./Coordinacion/README.md):
  Punto de intercambio con BronzeAge y acceso a la arquitectura objetivo y al modelo compartido propuesto.

### Referencias técnicas

- [ConfiguracionPrefabs_ECS_Visual.md](./ConfiguracionPrefabs_ECS_Visual.md):
  Guía para configurar prefabs visuales y su integración con ECS.
- [Existing_Databases.md](./Existing_Databases.md):
  Documentación sobre las bases de datos existentes en el proyecto.
- [Funcionalidades.md](./Funcionalidades.md):
  Resumen de las funcionalidades principales implementadas en el juego.
- [GDD.md](./GDD.md):
  Documento de diseño general del juego (Game Design Document).
- [Hero_detail_prefab_structure.md](./Hero_detail_prefab_structure.md):
  Estructura interna de cómo están configurados los prefabs de detalle del héroe.
- [ModeloHybrido.md](./ModeloHybrido.md):
  Explicación del modelo híbrido visual/ECS utilizado en el proyecto.
- [ScriptableObjects_Architecture.md](./ScriptableObjects_Architecture.md):
  Arquitectura y manejo de los datos del juego utilizando Scriptable Objects.
- [squad_prefab_relationships.md](./squad_prefab_relationships.md):
  Documentación de las relaciones entre prefabs de escuadrones y su configuración.
- [TDD.md](./TDD.md):
  Guía para el desarrollo orientado a pruebas (Test Driven Development).
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

> Documentación de Conquest Tactics, actualizada al 2026.
