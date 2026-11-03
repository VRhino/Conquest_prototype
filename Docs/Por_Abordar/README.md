# Por abordar

Trabajo que se ha dejado fuera a propósito de un plan, con el porqué y lo necesario para retomarlo. Cada punto dice qué es,
por qué no entra ahora, qué hay que tener en cuenta y por dónde empezar.

Cuando un plan cierre un punto, se marca aquí como **Hecho** con la referencia del commit o del documento, y no se borra.

| Documento | Ámbito | Plan del que sale |
|---|---|---|
| [Formaciones_y_Posturas.md](./Formaciones_y_Posturas.md) | Datos de formación, plantillas, reparto, posturas, animación y defensa | `tasks/todo.md` (formaciones por datos), 2026-10-20 |
| [Movimiento.md](./Movimiento.md) | Objetivos abiertos del lote 2, lote 3, escala y coste | `tasks/movement-batch2-plan.md` y `Docs/Design/Movimiento_Formaciones.md` §10 |
| [Habilidades_Perks.md](./Habilidades_Perks.md) | Lo que deja abierto el lanzador de habilidades y los perks | `tasks/todo.md` (Fase 2 de `Aprendizajes_Referencia.md`), 2026-10-22 |
| [Pila_Modificadores.md](./Pila_Modificadores.md) | Stats de objeto con `StatId` (L7-6) y lo que dejó abierto la pila de modificadores | `tasks/todo.md` (Fase 1 de `Aprendizajes_Referencia.md`), 2026-10-21 |
| [Muerte_Heroe.md](./Muerte_Heroe.md) | Animación de muerte, retirar el cuerpo y reaparecer | `tasks/todo.md` (presentación de la muerte del héroe), 2026-10-27 |
| [Combate.md](./Combate.md) | Datos de los rasgos del golpe, terreno y condicionados; reacciones al golpe (L1-09) | `tasks/todo.md` (combate pendiente del catálogo), 2026-11-03 |

## Estado general (2026-11-03, tras el combate del catálogo)
| Documento | Abiertos | Bloqueados | Lo más prioritario |
|---|---|---|---|
| Formaciones_y_Posturas.md | F4, F6, F8, F9, F11-F13, F16, F18 (F3 parcial) | F3 (clips de ShieldWall y Testudo) | F9 cuando haya caso de uso (falta quien apague la colisión y la evitación por escuadra) |
| Movimiento.md | M4, M10, M12, M15, M16 (M2 hecho el 2026-10-28) | M3 (paquete de VFX) | P6 de `game-re/notes/battle-movement-combat.md` solo si vuelven los picos |
| Habilidades_Perks.md | H3, H4, H12, H14 (H5: falta el árbol) | H2 (animaciones) | H14 (perks con las condiciones nuevas; son datos) |
| Pila_Modificadores.md | P1, P3, P5 | P6 (prefab de VFX) | P1 (stats de objeto con `StatId`, cuando toque) |
| Muerte_Heroe.md | D4, D5 | D1 (clip de muerte) | D1 en cuanto haya clip; D5 (medir el coste de recrear el visual) |
| Combate.md | C3, C5-C8 (C1 y C2 hechos) | C4 (L1-09, animaciones de reacción) | C3 (datos de condicionados; el caso de libro espera a la caballería) |

Lo que el catálogo de la referencia aún no ha planificado (incluidos los 20 puntos añadidos el 2026-10-26, marcados
"(amp.)") está en `Docs/Design/Aprendizajes_Referencia.md` §4, con su estado al final de §5. Cuando un plan los tome, lo
que deje fuera vendrá a esta carpeta.

## Convenciones
- **Estado:** `Pendiente`, `Bloqueado` (depende de algo externo, como contenido o un paquete) o `Hecho`.
- **Prioridad:** `Alta` (afecta a la sensación de movimiento y formación, la prioridad del proyecto), `Media` o `Baja`.
- **Referencia:** lo que se tomó de Conqueror's Blade sale del análisis estático en `game-re/notes/`. Solo se transfieren
  patrones; ningún código ni dato de la referencia.
- Las marcas **[dec]**, **[obs]** y **[hip]** indican si algo se leyó en el código, se dedujo de nombres o es una hipótesis.
