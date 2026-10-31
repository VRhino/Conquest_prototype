# Pikemen: modelo, agarre y formación

Autoría visual para `Assets/Resources/Squads/Pikemen/Pikemen_GO_Skeleton.prefab`.
Conserva las piezas elegidas del modular y la escala de `SM_Wep_Spear_07` que había en la escena.

## Ajustes

Editar `settings.json` y ejecutar **Conquest > Pikemen > 2 - Rebuild Poses And Controller**.
Después ejecutar **3 - Show Formation Review** y guardar la escena.
El paso **1 - Prepare From Scene** solo se necesita al cambiar el modelo o el arma fuente;
reconstruye el equipamiento a partir del actor y la lanza independiente de la escena.

| Ajuste actual | Delante | Detrás |
|---|---:|---:|
| Inclinación ascendente de la pica | 14° | 2° |
| Bajada de la cadera | 0,40 m | 0,07 m |
| Giro de cadera | 25° | 20° |
| Distancia entre agarres | 0,60 m | 0,54 m |

Primera fila con rodilla derecha cerca del suelo y pie izquierdo adelantado. Segunda fila de pie,
desplazada media columna (0,525 m). Columnas y filas a 1,05 m para respetar el diámetro
de 1 m de los cuerpos del sistema de movimiento.
Son ajustes visuales a partir de las referencias, no mediciones exactas del juego de referencia.

`Pikemen_Line.asset` usa el mismo cálculo que la revisión. Asigna `PikeLineFront` y `PikeLineRear`
a las dos filas; en marcha vuelve a `Normal`. `AC_Pikemen` conserva la locomoción Synty y añade
porte a dos manos, ambas posturas y estocadas. La estocada avanza 0,15 m para que ambas manos alcancen.
El arma y su hitbox se mueven juntos bajo `PikeCarrier`.

La revisión `Pikemen_Formation_Review` es `EditorOnly`: ocho modelos inmóviles para comparar
las poses; no representa una escuadra ECS activa. Sus poses y componentes desactivados se guardan
como overrides del prefab. `render-review.cs` genera tres imágenes aisladas desde Unity.

## Validación y alcance

- Nueve pruebas EditMode de Pikemen: catálogo y reclutamiento en perfiles antiguos, persistencia
  de la instancia y loadout, registro visual, filas escalonadas, rodilla, inclinación, agarre
  durante la estocada, salida a locomoción y combate libre de pie desde ambas filas.
  Otras nueve pruebas de `UnitPostureTests` verifican las órdenes, la vuelta a HoldPosition y
  las posturas anteriores de Spearmen/Testudo. Las nueve pruebas anteriores del Animator de Spearmen también pasaron.
- `pose_report.txt`: alcance de las muñecas en las poses y los fotogramas de ataque.
- `tests-pikemen.json`: resultados guardados de las pruebas nuevas.
- `tests-gameplay-flow.json`: prueba PlayMode superada de reclutamiento mediante el controlador
  del barracón, selección en BattlePrep, transición a BattleScene de producción, 20 unidades
  con modelo y hitbox vinculados, perfil melee perforante, seguimiento, F1/F2 y V liberando
  la postura de fila en todas las unidades. Verifica el ajuste horneado en la subescena. Usa guardado
  en memoria y restaura los proveedores de la partida al terminar.
- El alta jugable usa `pkm01`, `Pikemen.asset`, `Pikemen_Melee.asset`,
  `UnitPrefab_ECS_Pikemen.prefab`, `SquadDatabase` y la entrada `UnitVisual_Pikemen` del registro visual.
- No se añade un sistema ECS ni un controlador de movimiento alternativo; se reutilizan
  `FormationDefinition`, `UnitPostureSystem`, `UnitAnimatorController` y la captura Humanoid
  compartida de `SpearmenLineAnimationSetup`.

## Reclutamiento y placeholders

El paso **4 - Register Gameplay Placeholders** crea los assets iniciales y conecta el catálogo.
Repetirlo conserva los valores de combate ya editados, pero vuelve a conectar las formaciones y el registro.
Los valores de combate se editan en `Pikemen_Melee.asset`; el coste, tier, vida y demás datos
en `Pikemen.asset`. Si se cambian los tiempos de animación en `settings.json`, sincronizar también
los campos de tiempo del módulo melee.

`availableForRecruitmentByDefault` permite reclutar Pikemen en el barracón a héroes nuevos y
existentes. El servicio combina esa disponibilidad del catálogo con los desbloqueos del héroe,
sin reescribir su guardado, regalar tropas ni equiparlas automáticamente. La instancia reclutada
entra en `squadProgress` y puede seleccionarse en BattlePrep con el límite normal de liderazgo.

Mismo tier de Spearmen (`seasoned_tier`), 120 de liderazgo, 20 unidades, 1.600 HP y velocidad 5.
Defensas, masa, progresión y sprites proceden de Spearmen. Los sprites son placeholders.
Spearmen no tiene módulo melee asignado: el placeholder de daño usa los 150 puntos totales
de Squires como daño perforante, con penetración 20, alcance 3,1 m y sin escudo.
Estocada de 1 s, ventana de impacto 0,32–0,52 s y enfriamiento de 1,5 s tras terminar.
F1 usa la línea de picas; F2 reutiliza la formación dispersa de Spearmen.
El perfil táctico es `Versatile`, igual que Spearmen. No se implementa una habilidad anticarga.
La prueba del flujo no sustituye una prueba de balance ni de combate prolongado entre escuadras.

## Formación frente a combate libre

`Pikemen_Line.asset` activa `useNormalPostureInFreeCombat`. `UnitPostureSystem` usa la orden
real del escuadrón: Attack (V) selecciona `Normal` durante aproximación, pausas y ataques,
independientemente de la fila que ocupaba el soldado. La persecución durante FollowHero también
libera la postura. Mantener posición conserva `PikeLineFront` o `PikeLineRear` para atacar desde
la fila; el movimiento continúa controlado por los sistemas de navegación existentes.
El Animator ya usa la estocada de pie cuando `Stance` es Normal, sin requerir otra animación.
