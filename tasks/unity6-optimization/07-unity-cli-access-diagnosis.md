# Diagnóstico: Unity CLI en Luna frente a Sol

Fecha: 2026-10-16. Investigación solicitada por el usuario. Pruebas de solo lectura: orquestador con configuración de modelo `gpt-6.1-sol` y subagente existente `cpu_detection`, creado explícitamente con `gpt-6-luna`.

## Resultado reproducido

No se reprodujo una restricción del CLI por modelo. Se ejecutaron los mismos comandos en el mismo checkout:

| Comando / contexto | Orquestador | Subagente Luna |
|---|---|---|
| `unity --version` | Exit 0, `1.0.0-beta.13` | Exit 0, `1.0.0-beta.13` |
| `unity status --format json`, `use_default` | Exit 1, `STATUS_NO_INSTANCES` | Exit 1, `STATUS_NO_INSTANCES` |
| Advertencia dentro del sandbox | `Could not check which editors are running, so a running editor may be missing from this report.` | La misma advertencia |
| El mismo status con `require_escalated`, aprobado | Exit 1, `STATUS_PIPELINE_LOAD_PENDING`, sin warnings | Exit 1, `STATUS_PIPELINE_LOAD_PENDING`, sin warnings |

La ejecución fuera del sandbox detecta Unity con el proyecto abierto y Pipeline en el manifest, pero no encuentra un servidor Pipeline en funcionamiento. La consulta normal pierde además visibilidad de los procesos. El subagente Luna pudo ejecutar el binario y solicitar la misma ejecución fuera del sandbox. Ninguno consiguió una conexión operativa al Editor en esta prueba.

La ejecución fuera del sandbox no implica pedir privilegios de administrador de Windows ni cambiar globalmente los permisos. La configuración local `windows.sandbox = "elevated"` sigue describiendo una modalidad de sandbox; no equivale a que todos los comandos sean `require_escalated`.

## Qué ocurrió en la ronda anterior

El orquestador asignó a los Luna tareas de investigación y les indicó explícitamente que no controlaran el Editor, reservando esa interacción para un único coordinador. Por ejemplo: «no controlar Editor (lo coordina root)» y «No controlar ni reiniciar Editor». No se trató de que Luna rechazase una herramienta por incapacidad del modelo.

La comprobación anterior del propio orquestador se limitó al contexto normal: reportó servidor no alcanzable sin contrastarlo fuera del sandbox. Esta investigación añade esa comparación, con idéntico resultado para el padre y Luna. No hay evidencia suficiente para atribuir éxitos de otras sesiones Sol al modelo: tendrían que compararse sandbox, instrucciones, disponibilidad de Pipeline, proyecto y hora de ejecución.

## Estado de Pipeline observado

`unity pipeline list --format json` fuera del sandbox reconoce el proyecto con `isRunning: true`, package `0.8.0-exp.1`, `pipelineServer.isReachable: false`, `apiUrl: null`, `safeMode: null`; sin warnings de detección de procesos. `safeMode: null` no demuestra Safe Mode ni permite descartarlo definitivamente.

El paquete contiene el arranque `[InitializeOnLoad]` y el menú `Window/Pipeline/Start Server` en `Library/PackageCache/com.unity.pipeline@62c08c808737/Editor/EditorPipelineStartup.cs`. El diagnóstico no accionó ese menú, no reinició Unity, no cambió settings ni ejecutó tests. La causa interna exacta por la que el servidor no se publica sigue sin verificarse desde una conexión al Editor.

## Regla operativa para las siguientes delegaciones

1. Autorizar a los subagentes las consultas CLI de lectura cuando la tarea las necesita; reservar cambios de escena, Play Mode y tests que compitan por el Editor a un único coordinador.
2. Ante `STATUS_NO_INSTANCES` acompañado por advertencia de procesos, no concluir que Unity está cerrado ni que el modelo no tiene CLI. Comparar la misma consulta fuera del sandbox mediante el mecanismo de aprobación disponible.
3. Si fuera del sandbox se obtiene `STATUS_PIPELINE_LOAD_PENDING`, diagnosticar el servidor del Editor. Cambiar de modelo no demuestra una solución a ese estado.
4. Si se deniega ejecución fuera del sandbox, informar la denegación y su causa; no atribuirla a la capacidad del modelo sin evidencia.

## Documentación oficial contrastada

- [Subagents: approvals and sandbox controls](https://learn.chatgpt.com/docs/agent-configuration/subagents): los subagentes heredan sandbox/permisos del turno padre; instrucciones de una tarea pueden limitar las acciones, pero no crean un sandbox diferente.
- [Catálogo de modelos](https://developers.openai.com/api/docs/models): Luna y Sol admiten herramientas. Esto no garantiza acceso a un servidor local: ese acceso se comprobó directamente con las herramientas de esta sesión.

Conclusión limitada a esta comparación: la diferencia observada es entre contextos de ejecución y el estado de Pipeline; no entre Luna y Sol.
