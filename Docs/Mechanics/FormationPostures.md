# Posturas por ranura

Fecha: 2026-10-20. Patrón tomado de Conqueror's Blade (análisis estático, `game-re/notes/formation-postures.md`): la ranura
de la plantilla da el **papel** y el **rumbo** del soldado; la **postura** es un dato de cada soldado.

## Datos
- `FormationDefinition.templates[].slots[]`: posición (en unidades de espaciado), `row` (0 = frente), `role`
  (`Front`, `Flank`, `Interior`, `Rear`) y `headingDegrees` (rumbo local, 0 = hacia delante, 90 = derecha).
- `FormationDefinition.postures[]`: por papel, postura **formado** y **en marcha** (`UnitStance`: `Normal`,
  `BracedShields`, `ShieldFront`, `ShieldSide`, `ShieldUp`). Un papel sin entrada usa `Normal`.
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
- Pendiente: los clips de escudo al frente, al costado y arriba (no hay en el proyecto) y los estados del Animator que
  los usan; sin ellos la postura existe en la lógica pero no se ve ni cambia el bloqueo. Ver `Docs/Por_Abordar/` F3-F4.
- `UnitShieldComponent.orientation` se rellena desde el prefab pero ningún sistema lo lee (dato muerto).
