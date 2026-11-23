# Visuales ligeros de batalla: lo que queda fuera

Fecha: 2026-11-23. Sale del plan de visuales ligeros de héroes y unidades en batalla (2026-10-29), que se cerró a medias el
2026-10-30 a petición del autor. El plan se retiró de `tasks/` en la limpieza del 2026-11-23 (sigue en git). Lo que funciona
está en `Docs/ConfiguracionPrefabs_ECS_Visual.md` y `Docs/ModeloHybrido.md`.

## Lo que ya está
- Cuatro prefabs de tropa ligeros generados (`Assets/Prefabs/BattleVisuals/`) y registrados en
  `Assets/ScriptableObjects/VisualPrefabConfig.asset` como `battleVisualPrefab`; los originales se conservan para editar.
- Rigs ligeros local y remoto con catálogo de mallas compartidas; se monta solo la apariencia elegida. Arma e hitboxes siguen en el rig.
- La apariencia se copia del ticket al contenedor de batalla y al héroe, y el respawn la conserva. Cambiar el equipo en combate está bloqueado.
- Regeneración manual desde el menú del editor, con comprobación de las cuatro tropas y 68 apariencias de héroe.

## Reglas confirmadas por el autor
- La apariencia corporal del héroe es fija toda la batalla (respawn y puntos de suministro incluidos).
- Cambiar el arma podría llegar más adelante: el arma va aparte y no está habilitado.
- Las futuras excepciones de unidades cambiarán entre unas pocas configuraciones prearmadas (unas tres), como lista de datos.
- Lo que cambie fuera de batalla se ve en la siguiente entrada a batalla.

## Resumen
| # | Tema | Prioridad | Estado |
|---|---|---|---|
| V1 | Perfiles repetidos con cargas masivas en un Player (A/B, percentiles de frame y de spawn, memoria, GC, draw calls) | Media | Pendiente |
| V2 | Medir memoria y coste de spawn en frío y en caliente | Media | Pendiente |
| V3 | Cobertura visual de animación más extensa y comparación de casos de casco, pelo, barba, piezas compuestas, armas y escudos | Baja | Pendiente |
| V4 | Validación en build | Media | Pendiente |
| V5 | Detectar salidas obsoletas y prueba de regeneración idempotente (conservar GUID al regenerar) | Media | Pendiente |
| V6 | Detectar configuraciones incompletas antes de entrar en batalla, sin caer en silencio al prefab enorme | Media | Pendiente |
| V7 | Variantes prearmadas de unidades y cambio de arma | Baja | Pendiente (extensión futura, no habilitada) |
| V8 | Documentar cómo añadir una configuración fija o una lista de variantes | Baja | Pendiente |

## Cómo abordarlo
Primero V1 y V2: sin medidas en Player no se puede decir cuánto se gana. Las evidencias de partida están en
`Logs/BattleVisuals/` (`validation.txt`, `baseline-rendered-profile.json`, `lightweight-rendered-profile.json`). No prometer
FPS sin una línea base comparable.
