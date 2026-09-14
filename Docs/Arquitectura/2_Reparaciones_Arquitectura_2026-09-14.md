# Reparaciones de arquitectura y validación

Fecha: 2026-09-14. Cambios locales sobre la base 5f52f8ca; sin commit.

## Cambios aplicados

1. Compilación: herramientas de Assets/Scripts/Editor y Synty separadas en ensamblados exclusivos del editor. Imports de UnityEditor eliminados del runtime; confirmación de borrado disponible mediante un diálogo runtime con cancelar/confirmar.
2. Bake: SquadDefinitionBaker<T> unifica ambos puntos de entrada, conserva regeneración de bloqueo y stun, declara dependencias y registra blobs. Las curvas se muestrean para niveles 1–30; configuración ausente produce multiplicador neutro y los lectores validan límites.
3. XP: SquadProgressionSystem consume SquadXPEvent explícitos y los destruye. No concede 10/50 XP por frame de postpartida. No se ha implementado una fórmula de recompensas ni un servidor: la propuesta CQ-001 sigue pendiente.
4. Guardado: actualizaciones de héroe sobre el documento más reciente; equipo relee el estado antes de guardar y solo se guarda una vez por postpartida. Escrituras con archivo temporal y copia .bak. ProgressFileStore permite probar transacciones fuera de las partidas reales.
5. Combate: escalado sincroniza HealthComponent, DefenseComponent y el perfil de daño; conserva proporción de salud y munición/recarga. Un evento de nivel ya no recalcula todas las escuadras.
6. Penetración: el valor base vive en el perfil de daño; PenetrationComponent de una unidad recién creada representa bonus adicional, inicialmente cero.
7. Eventos: impactos de proyectil marcan que poseen su entidad temporal; DamageCalculationSystem la destruye en todas sus salidas. Los eventos melee solo pierden el componente.
8. Bootstrap: la UI envía una petición; BattleBootstrapSystem escribe el estado local y los mapas remotos. SquadIdMapElement conserva GUID, nivel, XP, formación y efectivos. El spawn respeta supervivientes y límites de formación; las escuadras remotas no heredan el mapa local.
9. Identidad: SquadInstanceComponent conserva persistentId e initialUnitCount. Este último impide calcular siempre 100% de supervivencia después de que UnitDeathSystem retire muertos del buffer.
10. Definiciones: el baker de héroe produce HeroClassDefinitionComponent y referencias a habilidades/perks. Las habilidades de escuadra se hornean y se restauran según el nivel. Esto conecta datos; no implementa nuevos efectos de habilidades.
11. Caché: el cálculo de atributos usa el equipamiento del héroe recibido, no el seleccionado globalmente. El fallback remoto no incorpora equipamiento del jugador local.
12. Limpieza: conversión de offsets de formación compartida, guards de carga sin jugador/héroes, eliminación de una consulta y método sin uso. Métodos públicos de compatibilidad y archivos vacíos se conservan para no romper referencias sin una auditoría de assets.
13. Detección: se excluyen entidades marcadas muertas y se vacían buffers antes de salidas tempranas. Añadida marca de Profiler Conquest.EnemyDetection.

## Validación reproducible

Motor utilizado: Unity 6000.5.8f1, Entities 6.5.0.

```powershell
unity test . --mode EditMode --filter ArchitectureRegressionTests --output Logs/architecture-regression-results.xml --timeout 300 -- -nographics -logFile Logs/architecture-regression-tests.log
unity run . --timeout 300 -- -nographics -executeMethod ArchitectureBuildValidation.CompilePlayerScripts -logFile Logs/architecture-player-compile.log
```

Suite: 14 casos sobre curvas, estado de combate, XP consumida una sola vez, alcance del escalado, limpieza de eventos, identidad, transacciones de guardado, offsets, spawn con supervivientes, snapshot remoto y desbloqueos.

Resultado final: **14/14 pruebas aprobadas**; **64 ensamblados de Player compilados correctamente**.
Comprobaciones ejecutadas el 2026-09-14. El grafo local no se considera actualizado: el intento de graphify update no terminó durante esta validación.

La comprobación Player compila los scripts para Windows x64; no produce un ejecutable completo ni valida escenas, assets, shaders o una partida completa.
Los XML y logs se guardan en Logs/ (ignorado por Git); las pruebas y el validador quedan en Assets/Tests/EditMode/.

## Límites y pendientes

- Siguen coexistiendo player_save.json y player_progress.json. No se ha migrado la persistencia al modelo/contrato de BronzeAge.
- El snapshot de batalla conserva la semántica de experiencia que usa la UI actual: XP hacia el siguiente nivel. Un contrato externo acumulativo necesita una conversión explícita.
- SquadXPEvent es un evento local, no un protocolo autenticado ni un resultado idempotente entre reintentos/red. La fórmula del servidor y BattleResult quedan pendientes.
- Hay más escrituras históricas GameObject → ECS, especialmente el spawn remoto y selección legacy. Solo se ha aislado el bootstrap intervenido.
- La detección aún hace recorridos anidados y mantiene buffers por unidad. Se instrumentó para medir; no se afirma una mejora de rendimiento ni se implementó particionado espacial sin perfilado.
- Falta una partida de integración y validación visual del diálogo. Las pruebas EditMode no demuestran que todas las escenas y prefabs estén correctamente configurados.
- No se han borrado scripts públicos potencialmente referenciados por assets. La limpieza destructiva queda separada de estas correcciones.
- Warnings de APIs obsoletas y avisos de memoria/licencia del proceso de Unity no se consideran resueltos por esta suite.
