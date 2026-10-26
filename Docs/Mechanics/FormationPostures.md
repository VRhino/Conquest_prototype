# Posturas por ranura

Fecha: 2026-10-20. Patrón tomado de Conqueror's Blade (análisis estático, `game-re/notes/formation-postures.md`): la ranura
de la plantilla da el **papel** y el **rumbo** del soldado; la **postura** es un dato de cada soldado.

## Datos
- `FormationDefinition.templates[].slots[]`: posición (en unidades de espaciado), `row` (0 = frente), `role`
  (`Front`, `Flank`, `Interior`, `Rear`) y `headingDegrees` (rumbo local, 0 = hacia delante, 90 = derecha).
- `FormationDefinition.postures[]`: por papel, postura **formado** y **en marcha** (`UnitStance`: `Normal`,
  `BracedShields`, `ShieldFront`, `ShieldSide`, `ShieldUp`, `SpearLineFront`, `SpearLineRear`). Un papel sin entrada usa `Normal`.
- Valores por defecto (`Tools/Formations/Apply Default Postures`): Testudo con el frente escudo al frente, los costados
  escudo al costado (y mirando hacia fuera por su rumbo) y el interior y la retaguardia escudo arriba; Shield Wall
  afianzada al formar y escudo al frente en marcha.

## Sistemas
| Sistema | Qué hace |
|---|---|
| `FormationSystem` | Escribe papel, fila y rumbo de cada ranura en `UnitGridSlotComponent` al rehacer la disposición. |
| `UnitFollowFormationSystem` | Orienta a la unidad formada con el rumbo de la formación × el rumbo de su ranura. |
| `UnitPostureSystem` | Único escritor de `UnitFormationStanceComponent`: postura de la tabla según papel y estado; en melé conserva la de formado. Propaga postura, fila y papel a la animación. |
| `UnitAnimatorController` | Escribe los parámetros `Stance` y `SlotRole` si el controller los declara. |

## Defensa
El bloqueo es físico: un golpe o una flecha es de escudo si choca primero con el collider `ShieldHitbox`, que cuelga de
la mano (`Hand_L/Off_Hand_Weapon_Carrier`). Por eso la defensa por postura sale de la animación: con un clip de escudo
arriba, el collider sube y cubre lo que llega desde arriba, como el cuerpo de impactos por postura de la referencia.

## Estado
- Hecho: datos, reparto de papeles, rumbo por ranura, sistema de posturas y parámetros del Animator. Tests en
  `UnitPostureTests` y `FormationLayoutTests`.
- Integrado (2026-10-24): Line de lanceros usa clips Humanoid propios para frente arrodillado y retaguardia con
  escudo elevado. La segunda fila incluye sus extremos Flank. `AC_Spearmen` añade una capa al controlador de
  locomoción existente; sale al moverse, atacar o cambiar de postura, restaurando los anclajes del equipo.
  Desde el ajuste del 2026-10-26, Line usa 0,67 m entre columnas y 0,65 m entre filas, sin ruido,
  para coincidir con el estudio de Blender. Proceso, aprendizajes y regeneración en
  [Crear animaciones de formación](../Guides/Crear_Animaciones_De_Formacion.md).
- Pendiente: clips específicos para las posturas genéricas de ShieldWall/Testudo (`ShieldFront`, `ShieldSide`,
  `ShieldUp`, `BracedShields`). Las nuevas poses exclusivas de Line no se asignan a esas formaciones.
  También faltan estocadas y transiciones coreografiadas para Line; actualmente usa mezcla entre poses.
- `UnitShieldComponent.orientation` se rellena desde el prefab pero ningún sistema lo lee (dato muerto).
