# Bodyblock y orden físico

Fecha: 2026-09-14. Quinta fase del pipeline de movimiento. Cambios locales sin commit.

## Cambios aplicados

- `UnitBodyblockSystem` se ejecuta después de decidir destinos y antes de `NavMeshPositionSyncSystem`. La corrección de `agent.Move` llega a `LocalTransform` en el mismo ciclo.
- El clamp deja de ser metros por frame. `bodyblockMaxPushSpeed` está en m/s y el desplazamiento máximo es `velocidad * deltaTime`, consistente a 30/60/144 FPS.
- Dos enemigos exactamente solapados ya no se ignoran: reciben una dirección horizontal determinista, finita y normalizada.
- Radio, fuerza normal, fuerza de muro, velocidad máxima, radio engaging y fuerza engaging viven en `SquadSpawnConfigAuthoring`/`Component`.
- Se conserva la política existente: solo equipos enemigos; aliados no se repelen; dos paredes formadas no reciben push; engaging usa valores reducidos; el héroe local continúa con CharacterController/colliders.

Valores por defecto: radio 0,8 m, fuerza normal 8, fuerza muro 60, máximo 18 m/s, radio engaging 0,35 m y fuerza engaging 3. El máximo preserva aproximadamente 0,3 m a 60 FPS sin variar la velocidad límite por frecuencia.

## Validación

```powershell
unity test . --mode EditMode --filter 'BodyblockRegressionTests|FormationGeometryRegressionTests|ArchitectureRegressionTests|SquadControlRegressionTests|HeroLifecycleRegressionTests|SquadSwapRegressionTests|SquadOwnerDeathRegressionTests' --output Logs/bodyblock-results.xml --timeout 300 -- -nographics -logFile Logs/bodyblock-tests.log
unity test . --mode PlayMode --filter 'SquadNavigationRegressionTests|HeroVisualRespawnRegressionTests' --output Logs/bodyblock-playmode-results.xml --timeout 300 -- -nographics -logFile Logs/bodyblock-playmode-tests.log
unity run . --timeout 300 -- -nographics -executeMethod ArchitectureBuildValidation.CompilePlayerScripts -logFile Logs/bodyblock-player-compile.log
```

EditMode cubre clamp equivalente a 30/60/144 FPS y dirección estable para solapamiento exacto. PlayMode crea un NavMesh temporal, solapa dos agentes enemigos y verifica separación física real, además de las regresiones anteriores.

Resultado final: **58/58 EditMode**, **5/5 PlayMode** y **64 assemblies de Player Windows** compiladas correctamente.

## Límites

No cambia la política de aliados ni la ausencia de resolución entre dos muros formados enemigos. Necesitan validación de diseño en batalla masiva.

Actualización posterior: la cuadrícula conserva `Dictionary`/`List` administrados en main thread, pero reutiliza sus buckets mediante pool. El stress test de 900 agentes midió 0 bytes administrados en frame caliente; véase la fase 11. No se migra a Native containers/Burst sin evidencia. El contrato físico del héroe local quedó certificado en la fase 10.
