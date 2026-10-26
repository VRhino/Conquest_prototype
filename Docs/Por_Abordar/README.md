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

## Estado general (2026-10-26)
| Documento | Abiertos | Bloqueados | Lo más prioritario |
|---|---|---|---|
| Formaciones_y_Posturas.md | F1, F2, F4, F6-F9, F11-F13, F15, F16 (F3 parcial) | F3 (clips de ShieldWall y Testudo) | F2 (C13: posiciones sobre la malla), F1 |
| Movimiento.md | M2, M4, M10, M12 | M3 (paquete de VFX) | M2 (solapes en la melé) |
| Habilidades_Perks.md | H1, H3-H9 | H2 (animaciones) | H1 (IA con habilidades), H5 (elegir perks) |
| Pila_Modificadores.md | P1-P5 | P6 (prefab de VFX) | P2 (referencia rota del prefab de clase) |

Lo que el catálogo de la referencia aún no ha planificado (incluidos los 20 puntos añadidos el 2026-10-26, marcados
"(amp.)") está en `Docs/Design/Aprendizajes_Referencia.md` §4, con su estado al final de §5. Cuando un plan los tome, lo
que deje fuera vendrá a esta carpeta.

## Convenciones
- **Estado:** `Pendiente`, `Bloqueado` (depende de algo externo, como contenido o un paquete) o `Hecho`.
- **Prioridad:** `Alta` (afecta a la sensación de movimiento y formación, la prioridad del proyecto), `Media` o `Baja`.
- **Referencia:** lo que se tomó de Conqueror's Blade sale del análisis estático en `game-re/notes/`. Solo se transfieren
  patrones; ningún código ni dato de la referencia.
- Las marcas **[dec]**, **[obs]** y **[hip]** indican si algo se leyó en el código, se dedujo de nombres o es una hipótesis.
