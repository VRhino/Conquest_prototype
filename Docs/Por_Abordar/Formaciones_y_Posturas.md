# Formaciones y posturas: lo que queda fuera

Fecha: 2026-10-20. Sale del plan de formaciones por datos (`tasks/todo.md`), que sustituye la rejilla fija de 1 m por
`FormationDefinition` (espaciado propio, plantillas, papel y rumbo por ranura) y añade posturas por ranura (paso 6).

Referencia: `game-re/notes/formations-orders.md` (plantillas, reparto, publicación), `game-re/notes/formation-postures.md`
(clase y rumbo de ranura, postura del cuerpo, StateGraph) y `game-re/notes/mounts-movement.md` §6 (evitación).

## Resumen
| # | Tema | Prioridad | Estado |
|---|---|---|---|
| F1 | Plantillas reducidas por número de soldados | Alta | Pendiente (contenido) |
| F2 | Elección de plantilla por encaje en la malla y reubicación (C13) | Alta | Pendiente |
| F3 | Clips de animación de las posturas | Alta | Bloqueado (contenido; inventario hecho, no hay clips) |
| F4 | Cuerpo de impactos por postura | Media | Depende de F3 |
| F5 | Valores reales de la referencia (posturas, ángulos, espaciados) | Media | Fuera de alcance del análisis |
| F6 | Reparto por clase de soldado (escuadras mixtas) | Media | Pendiente |
| F7 | Orden de llenado de ranuras (grupos de orden) | Media | Pendiente |
| F8 | Cambiar el espaciado desde el juego (orden o habilidad) | Media | Parcial: API hecha, sin orden |
| F9 | Relaciones de evitación y colisión por escuadra (C16) | Media | Pendiente |
| F10 | Ruido aleatorio por publicación frente a determinista | Baja | Decidido: determinista |
| F11 | Disposición dinámica y "seguir al líder con retardo" | Baja | Pendiente |
| F12 | Cohesión como consulta: quórum, aislados y terreno no apto | Media | Pendiente |
| F13 | Postura al ir subido a escaleras, torres o máquinas | Baja | Pendiente (sin asedio) |
| F14 | Media vuelta (invertir filas en giros grandes) | Baja | Descartado por D5 |
| F15 | Radios de llegada por formación | Media | Pendiente |
| F16 | `UnitShieldComponent.orientation` sin lectores | Baja | Pendiente |

---

## F1. Plantillas reducidas por número de soldados
**Qué es.** En la referencia, cada formación tiene varias plantillas y se usa la primera cuyo mínimo de soldados cabe en los
vivos; al caer soldados, la formación cambia de forma (por ejemplo, una línea de 4 filas pasa a 3). **[dec]**

**Por qué queda fuera.** El modelo nuevo ya admite varias plantillas y re-elige al cambiar los vivos, pero los assets
actuales se migran con una sola plantilla para que el juego se vea igual que hoy. Diseñar las plantillas de 15, 10 y 5
soldados es trabajo de contenido y de gusto, no de código.

**A considerar.**
- Las plantillas deben conservar la forma reconocible (una cuña sigue siendo cuña) y los papeles (frente, costado, interior).
- Al cambiar de plantilla, los soldados no se reordenan (ranura k = soldado k, como la referencia): si la plantilla nueva
  mueve mucho a alguien, habrá cruces. Conviene que la ranura k de la plantilla menor quede cerca de la ranura k de la mayor.
- Afecta a C2 (escalonado por fila) y a las posturas: la fila y el papel vienen de la plantilla nueva.

**Cómo abordarlo.** Una plantilla por umbral (20, 15, 10, 5) para Spearmen y Squires; medir S4 con bajas forzadas (matar
soldados por script) y contar cruces y recorrido total al cambiar de plantilla. El editor de plantillas del paso 1 debe
mostrar todas las plantillas de una formación superpuestas.

## F2. Elección de plantilla por encaje en la malla y reubicación (C13)
**Qué es.** La referencia prueba si la plantilla cabe en la malla de navegación antes de usarla (se puede desactivar por
plantilla) y, si una ranura cae fuera o choca con otra, la reubica en la celda libre más cercana de la misma isla con un
BFS de 4 vecinos. **[dec]** Decisión del usuario D9: mantener la plantilla y reubicar localmente, sin rechazar la orden.

**Por qué queda fuera.** Es un sistema propio (`FormationSlotResolveSystem`, consultas a NavMesh con `NavMeshQuery`) y no
hace falta para que las formaciones por datos funcionen en terreno abierto. Ya estaba previsto para el lote 3.

**A considerar.**
- El radio de consulta pasa a `min(espaciado, 0,65)` en el paso 3 del plan (L3-04); C13 se apoya en eso.
- Hay que guardar el slot resuelto aparte (`UnitResolvedSlotComponent`) para no tocar la plantilla.
- Coste: una consulta por cambio de ancla, no por frame. Validar con 900 agentes (S8).

**Cómo abordarlo.** Escena S7 de `Docs/Design/Movimiento_Formaciones.md` §5.2 (pasillo y borde de acantilado): dos unidades
nunca comparten celda y ninguna queda fuera de la malla.

## F3. Clips de animación de las posturas
**Qué es.** El paso 6 del plan conecta la postura con el Animator (parámetros `Stance` y `SlotRole`). Hacen falta clips de
escudo al frente, al costado y por encima de la cabeza, parado y andando.

**Por qué queda fuera.** Es contenido. No se sabe todavía si el paquete de animaciones actual los incluye; el paso 6 empieza
haciendo ese inventario.

**A considerar.**
- Sin clips, la postura funciona en lógica (defensa y bloqueo) pero no se ve. El plan lo permite a propósito.
- Las capas del Animator: la postura del escudo es una capa de brazo/torso sobre la locomoción, para no multiplicar estados.
- Las transiciones deben usar la histéresis de llegada (C1) para no parpadear.

**Cómo abordarlo.** Inventario de clips → lista de los que faltan → decidir si se compran, se hacen con retargeting o se
crean poses estáticas con IK de brazo como solución temporal.

**Inventario (2026-10-20).** En `Assets/` solo hay `Animations/Sword And Shield Slash.fbx`; ningún clip de escudo al frente,
al costado ni arriba. La lógica ya está (`UnitPostureSystem`, parámetros `Stance` y `SlotRole`): falta el contenido y los
estados del Animator que lo usen. Ver `Docs/Mechanics/FormationPostures.md`.

## F4. Cuerpo de impactos por postura
**Qué es.** En la referencia, cambiar la postura reconstruye el cuerpo de impactos del actor ("dummy body"): levantar el
escudo cambia qué zonas reciben golpes. **[dec]** flujo, **[obs]** efecto.

**Por qué queda fuera.** El paso 6 cubre lo esencial con la dirección de bloqueo (`ShieldOrientation.Up`). Cambiar también
los colliders del cuerpo es más caro y solo merece la pena si el combate distingue zonas (cabeza, torso, piernas).

**A considerar.**
- El escudo es un `BoxCollider` en la malla del escudo, que cuelga de la mano (`Hand_L/Off_Hand_Weapon_Carrier`); el bloqueo es
  físico (el golpe o la flecha choca antes con ese collider). Por tanto, **con clips de postura el cuerpo de impactos cambia
  solo**, como en la referencia. Mover el collider por código pelearía con la animación.
- Los golpes a la cabeza (la referencia tiene clase de golpe "cabeza") dependen de esto.

**Cómo abordarlo.** Primero medir con el paso 6 si el bloqueo por dirección basta (flechas en parábola contra el interior de
la tortuga). Si no, perfiles de collider por postura en el prefab, activados por el mismo sistema de posturas.

## F5. Valores reales de la referencia
**Qué es.** Qué clase de ranura lleva qué postura, los ángulos de defensa, los espaciados y el ruido de cada formación.

**Por qué queda fuera.** Están en datos cifrados o en Lua. El usuario decidió no tocar la protección del cliente, así que
quedan fuera del análisis estático.

**A considerar.** Los valores de Conquest son decisiones de diseño (plan, decisiones 1-7) y se ajustan mirando el juego, no
copiando cifras. Lo que sí se conoce: espaciado por defecto 1,0, suelo de 0,3, radio de encaje 0,65, radio combinado de
ORCA 1,0, vecinos a 4 m, horizontes de 5 s y 1 s. **[dec]**

## F6. Reparto por clase de soldado (escuadras mixtas)
**Qué es.** En la referencia, cada ranura tiene una clase y el reparto pone en ella a un soldado con ese atributo (`basic`).
Así una escuadra puede mezclar tipos (oficial, abanderado, escuderos y lanceros) y cada uno va a su sitio. **[dec]**

**Por qué queda fuera.** En Conquest todas las escuadras son de un solo tipo de unidad. El papel por ranura del plan
(`Front`, `Flank`, `Interior`, `Rear`) cubre las posturas sin necesitar clases de soldado.

**A considerar.**
- Si se añaden oficiales o abanderados, la ranura necesita además una clase de soldado y el reparto deja de ser "ranura k =
  soldado k": se reparte por clase y, dentro de la clase, por orden.
- Las bajas deberían rellenarse con soldados de la misma clase o dejar la ranura vacía.

**Cómo abordarlo.** Añadir `unitClass` a la ranura y al `SquadUnitSpawnBuilder`; reparto por clase en `FormationSystem`
solo en el evento (cambio de formación o de plantilla), nunca por frame.

## F7. Orden de llenado de ranuras
**Qué es.** La referencia guarda en cada plantilla hasta 256 grupos de "orden de posición": listas de ranuras en el orden en
que se llenan. Con menos soldados que ranuras, se ocupan primero las ranuras importantes (el frente). **[dec]**

**Por qué queda fuera.** Con plantillas por número de soldados (F1) apenas sobran ranuras. Hoy se llenan por índice.

**Cómo abordarlo.** Un campo `fillOrder` por ranura en la plantilla; el editor lo muestra como números. Solo tiene sentido
junto con F1 o F6.

## F8. Cambiar el espaciado desde el juego
**Qué es.** La referencia permite cambiar el intervalo entre soldados en partida (`setCurrentFormationSoilderInterval`, con
suelo de 0,3) y escalar la legión. **[dec]**

**Por qué queda fuera.** El plan deja el componente y la API (`SquadFormationSpacingRequest`), pero sin tecla ni orden: hace
falta decidir si es una orden del jugador ("apretar/abrir"), una habilidad o un efecto de estado.

**A considerar.** Abrir la formación cambia el radio entre compañeros de ORCA (sale del espaciado vigente) y la huella en
pasillos. Debe respetar el enfriamiento de cambio de formación.

## F9. Relaciones de evitación y colisión por escuadra (C16)
**Qué es.** En la referencia, quién esquiva a quién se configura por escuadra (mismo grupo, enemigo, aliado de otro grupo) y
hay interruptores de colisión por escuadra y por soldado. Por defecto, los enemigos se atraviesan. **[dec]** Decisión del
usuario D2: interruptor `EnemyCollisionEnabled` por escuadra (por ejemplo, para Muro de escudos).

**Por qué queda fuera.** Previsto para el lote 3. Ahora ORCA tiene reglas fijas: aliados sí, enemigos no, compañeros quietos
no son obstáculo.

**A considerar.** Un Muro de escudos que bloquea a los enemigos necesita colisión real (bodyblock o ORCA frente a enemigos),
no solo la distancia de parada. Hay que decidir cuál de los dos sistemas lo aplica para no tener dos escritores.

## F10. Ruido aleatorio por publicación frente a determinista
**Qué es.** La referencia sortea el ruido de posición y de rumbo cada vez que publica las posiciones. **[dec]**

**Decisión.** Conquest lo hace determinista por (escuadra, ranura): mismo aspecto orgánico, sin temblor al republicar. Se
reconsidera solo si la formación se ve demasiado "fija" al moverse; entonces se cambiaría la semilla en cada orden nueva, no
en cada frame.

## F11. Disposición dinámica y "seguir al líder con retardo"
**Qué es.** La referencia tiene una variante de Scatter que vuelve a consultar la malla cada ciclo y unos coeficientes de
"seguir al líder" que retrasan el ancla respecto al centro de la escuadra. **[dec]** estructura, **[obs]** significado.

**Por qué queda fuera.** La Dispersa del plan es estática; el ancla con retardo encaja con C10 (llegada del ancla, lote 3).

## F12. Cohesión como consulta
**Qué es.** La referencia pregunta si la formación está "lista" (como mucho `ceil((1−q)·N)` soldados sin llegar), qué
soldados están aislados (sin camino a su ranura) y teletransporta a los que caen en terreno no apto. **[dec]**

**Por qué queda fuera.** No es parte de la disposición. Encaja con la IA de escuadra (esperar a los rezagados antes de cargar)
y con C13.

**Cómo abordarlo.** Una consulta pura en `FormationLayout` (`IsFormationReady(q)`) leída por la IA; el teletransporte solo
como último recurso tras C13.

## F13. Postura al ir subido a escaleras, torres o máquinas
**Qué es.** La referencia elige la postura según el objeto al que va subido el soldado (lista valor → postura). **[dec]**

**Por qué queda fuera.** Conquest no tiene asedio todavía. Cuando lo tenga, la tabla de posturas del paso 6 se amplía con
"montado en objeto".

## F15. Radios de llegada por formación
**Qué es.** `MovementTuningComponent.Validate` comprueba los radios de llegada contra un espaciado de referencia de 1 m. La
Testudo (0,8 m) queda fuera de esa regla a propósito: `arrivalRadiusOut` = 0,5 m es más de la mitad de su espaciado.

**Cómo abordarlo.** Radios de llegada proporcionales al espaciado de la formación (por ejemplo, `r_out = 0,45·s`) o un perfil
de llegada por formación (D3). Medir en S4 que la Testudo no oscile al formar.

## F16. `UnitShieldComponent.orientation` sin lectores
**Qué es.** Se rellena desde `ShieldHitboxBehaviour.orientation` del prefab, pero ningún sistema lo lee (el bloqueo es el collider).

**Cómo abordarlo.** Eliminarlo, o usarlo si se añade un bloqueo por ángulo para golpes sin física (habilidades de área).

## F14. Media vuelta
**Qué es.** Invertir el orden de filas en giros grandes en lugar de rotar la rejilla.

**Decisión.** Descartado por D5 (rotar la rejilla entera). Con rumbo por ranura, una media vuelta podría resolverse girando
solo el rumbo de cada soldado; queda anotado por si D5 se revisa.
