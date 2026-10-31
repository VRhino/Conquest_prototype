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
| `UnitPostureSystem` | Único escritor de `UnitFormationStanceComponent`: postura de la tabla según papel y estado. Si la formación activa `useNormalPostureInFreeCombat`, una orden Attack o una persecución fuera de HoldPosition usa Normal; en HoldPosition conserva la postura por fila. Propaga postura, fila y papel a la animación. |
| `UnitAnimatorController` | Escribe los parámetros `Stance` y `SlotRole` si el controller los declara. |

## Defensa
El bloqueo es físico: un golpe o una flecha es de escudo si choca primero con el collider `ShieldHitbox`, que cuelga de
la mano (`Hand_L/Off_Hand_Weapon_Carrier`). Por eso la defensa por postura sale de la animación: con un clip de escudo
arriba, el collider sube y cubre lo que llega desde arriba, como el cuerpo de impactos por postura de la referencia.

## Estado
- Pikemen (2026-10-28): Line activa `useNormalPostureInFreeCombat`. Mantener posición permite
  estocadas desde la postura de cada fila; V (Attack) libera inmediatamente la postura incluso
  antes de adquirir un objetivo. En combate libre el Animator usa la estocada de pie para ambas
  filas. Volver a mantener posición y formar restaura las posturas de primera y segunda fila.
  El ajuste es optativo: las formaciones anteriores conservan su comportamiento.
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

## Brace por formación (2026-10-31)

- `FormationDefinition.supportsBrace` habilita brace al mantener posición (X) con esa formación. `braceHalfAngleDegrees` limita el giro a cada lado del frente fijado al dar la orden; 30 significa un arco total de 60 grados. Cada escuadra referencia sus propios assets de formación: compartir un asset comparte también su política.
- Configuración inicial: Spearmen Line habilitado con 30 grados por lado. El sistema admite cualquier tipo de unidad y formación; las demás conservan brace deshabilitado hasta configurarlo.
- `FormationSystem` y el spawn copian la política a `FormationComponent` junto al layout. `BraceWeaponActivationSystem` lee esa copia después de resolver órdenes y antes de targeting, sin consultar blobs durante la descarga de escenas. Un cambio de layout se refleja en brace en el siguiente ciclo de decisiones.
- Brace mantiene los destinos de HoldPosition: los soldados ocupan sus ranuras sin perseguir. Targeting y ataques rechazan enemigos fuera del alcance del arma o del arco frontal; el resolver de contactos repite la comprobación para no aplicar impactos tras un cambio de posición. `UnitRotationResolutionSystem` limita la rotación final del GameObject al arco.
- C cancela brace con una única orden, solicita Dispersed si está disponible en la escuadra y da prioridad temporal a seguir frente a la reacción de combate. La transición a dispersa no espera el cooldown de cambio de formación. V abandona brace para combatir libremente.
- Los perfiles opcionales de arma por fila vuelven a los valores melee de la escuadra al salir de brace.
- Spearmen tenía `meleeData` sin asignar. Se ha creado `Spearmen_Melee.asset` con daño perforante 100, penetración 20 y alcance 2 m como valores iniciales de balance, editables en ese asset.
