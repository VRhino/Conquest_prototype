# Guía de Animaciones para Unidades de Escuadrón

Esta documentación explica cómo implementar y usar el sistema de animaciones híbrido ECS para las unidades de escuadrón en Conquest Tactics.

## Componentes del Sistema

El sistema de animaciones para unidades se compone de:

1. **UnitAnimationMovementComponent** (ECS Component)
   - Almacena datos de movimiento: velocidad, dirección, estados
   - Actualizado por UnitAnimationSystem
   - Requiere **UnitAnimationMovementAuthoring** para añadirse a entidades

2. **UnitAnimationSystem** (ECS System)
   - Actualiza UnitAnimationMovementComponent para cada unidad
   - Se ejecuta después de UnitFollowFormationSystem

3. **UnitAnimationAdapter** (MonoBehaviour)
   - Puente entre ECS y GameObject
   - Conecta con EntityVisualSync para asociar entidad-visual

4. **UnitAnimatorController** (MonoBehaviour)
   - Actualiza los parámetros del Animator
   - Aplica rotación basada en movimiento

## Configuración de Prefab

Para cada prefab de unidad de escuadrón:

```
UnitPrefab
├── Animator (con AC_Polygon_Masculine.controller)
├── UnitAnimationAdapter
├── UnitAnimatorController
├── UnitAnimationMovementAuthoring (para baking del componente ECS)
├── EntityVisualSync (configurado igual que para el héroe)
└── CharacterController (opcional)
```

### Configuración en el Inspector

#### UnitAnimationMovementAuthoring
- **Initial Speed**: 0 (velocidad inicial, normalmente 0)
- **Max Speed**: 5 (velocidad máxima para normalización)
- **Initial Direction**: Vector3.forward (dirección inicial)

#### UnitAnimationAdapter
- **Auto Find Entity**: True (buscará la entidad asociada automáticamente)
- **Running Threshold**: 0.6 (umbral para considerar que está corriendo)
- **Stopped Threshold**: 0.05 (umbral para considerar que está detenido)
- **Enable Debug Logs**: False (activar solo para debugging)

#### UnitAnimatorController
- **Animation Damp Time**: 0.1 (suavizado de transición entre animaciones)
- **Rotation Speed**: 10 (velocidad de rotación hacia dirección de movimiento)
- **Rotate To Movement Direction**: True (activar rotación automática)
- **Enable Debug Logs**: False (activar solo para debugging)

#### EntityVisualSync
- Configurar igual que para el héroe
- **Auto Find Hero Entity**: True
- **Sync Position**: True
- **Sync Rotation**: False (la rotación es manejada por UnitAnimatorController)

## Parámetros del Animator

El controlador AC_Polygon_Masculine.controller utiliza estos parámetros principales:

- **MoveSpeed**: Velocidad normalizada (0-1)
- **CurrentGait**: Estado de marcha (0=Idle, 1=Walk, 2=Run, 3=Sprint)
- **IsStopped**: Si la unidad está detenida
- **IsGrounded**: Siempre true para evitar problemas con "Fall" state
- **IsWalking**: Si está caminando (no corriendo y no detenido)

## Flujo de Datos

```
UnitFollowFormationSystem (movimiento físico)
    ↓
UnitAnimationSystem (actualiza UnitAnimationMovementComponent)
    ↓
UnitAnimationAdapter (lee datos de ECS)
    ↓
UnitAnimatorController (actualiza Animator)
    ↓
AC_Polygon_Masculine.controller (anima el modelo)
```

## Debugging

Para depurar el sistema:

1. Agregar el script `UnitAnimationTester` (solo para testing)
2. Activar `Show Debug Info` para ver información en tiempo real
3. Activar logs en los componentes individuales para ver mensajes detallados

## Solución de Problemas Comunes

### Las unidades se quedan en estado idle_standing

Si las unidades se quedan atascadas en el estado `idle_standing` a pesar de estar en movimiento:

1. **Verificar triggers del Animator**:
   - Asegúrate de que el Animator Controller (AC_Polygon_Masculine.controller) tenga definidos los triggers `ForceLocomotion` y `ForceIdle` para forzar transiciones directas.
   - En el Animator, crea transiciones desde cualquier estado a `Locomotion` usando el trigger `ForceLocomotion`.
   - En el Animator, crea transiciones desde cualquier estado a `Idle_Standing` usando el trigger `ForceIdle`.

2. **Activar logs de debug**:
   - Establece `Enable Debug Logs` en `true` en el UnitAnimatorController.
   - Usa el componente UnitAnimationTester con `Show Debug Info` activado.
   - Verifica los valores de los parámetros `MoveSpeed`, `IsStopped` y `CurrentGait`.

3. **Verificar UnitAnimationAdapter**:
   - Asegúrate de que está encontrando correctamente la entidad ECS asociada.
   - Usa `DebugAnimationState()` desde el menú contextual para verificar los valores.

4. **Corregir transiciones del Animator**:
   - Reduce o elimina las condiciones de `Exit Time` en las transiciones.
   - Aumenta el valor de `Transition Duration` para transiciones más suaves.
   - Disminuye el valor de `Animation Damp Time` en UnitAnimatorController.

5. **Usar control directo en modo debug**:
   - Agrega UnitAnimationTester a la unidad.
   - Activa `Enable Direct Animator Controls`.
   - Usa las teclas numéricas para forzar estados (1=Walk, 2=Run, 0=Idle).
   - Pulsa I para inspeccionar el Animator y ver sus parámetros actuales.

### La animación no corresponde a la velocidad real

Si la unidad se mueve pero la animación no coincide con la velocidad:

1. Aumenta el valor de `Running Threshold` en UnitAnimationAdapter.
2. Reduce el valor de `Stopped Threshold` para detectar movimientos más pequeños.
3. Ajusta el valor de `Max Speed` en UnitAnimationMovementAuthoring.

### La rotación no sigue la dirección del movimiento

Si la unidad se mueve pero no rota hacia la dirección del movimiento:

1. Verifica que `Rotate To Movement Direction` esté activado en UnitAnimatorController.
2. Aumenta el valor de `Rotation Speed` para rotaciones más rápidas.
3. Asegúrate de que `Sync Rotation` esté desactivado en EntityVisualSync.

## Solución de Problemas Específicos

### Unidades Atascadas en Idle a Pesar de Moverse

Si las unidades permanecen en estado `idle_standing` mientras se mueven físicamente:

1. **Uso de UnitAnimationDebugger**:
   - Agrega el script `UnitAnimationDebugger` a cualquier GameObject en la escena.
   - Presiona F7 para forzar animación de movimiento en todas las unidades.
   - Presiona F8 para forzar animación de idle en todas las unidades.
   - Presiona F9 para reiniciar todos los animadores.

2. **Verifica la sincronización de posición**:
   - Asegúrate de que el componente `EntityVisualSync` esté correctamente configurado y enlazado con la entidad ECS.
   - Verifica que `Sync Position` esté habilitado y que el ID de entidad sea correcto.

3. **Modo de depuración forzada**:
   - Habilita `Force Movement Debug Mode` en el `UnitAnimationAdapter`.
   - Establece un valor de `Forced Debug Speed` mayor que el umbral de detección de movimiento (0.05 por defecto).
   - Esto forzará los parámetros de movimiento independientemente del movimiento real.

4. **Verifica el sistema ECS**:
   - Revisa los datos del componente `UnitAnimationMovementComponent` usando el EntityDebugger.
   - Confirma que `CurrentSpeed` y `IsMoving` se actualicen correctamente.

5. **Ajusta los umbrales de detección**:
   - Reduce el `Stopped Threshold` en `UnitAnimationAdapter` (intenta con 0.01).
   - Esto hará que las unidades sean más sensibles a pequeños movimientos.

6. **Solución extrema para casos atascados**:
   - Modifica el prefab de la unidad para añadir un componente `Animator` nuevo y configúralo correctamente.
   - Elimina y vuelve a añadir los componentes `UnitAnimationAdapter` y `UnitAnimatorController`.
   - Esto restablecerá el estado del Animator y forzará una reinicialización limpia.

## Notas Importantes

- Las unidades no tienen input del jugador ni cámara que los siga
- El sistema detecta automáticamente si están caminando o corriendo basado en velocidad
- La rotación es manejada automáticamente hacia la dirección del movimiento
- Siempre se establece IsGrounded=true para evitar que queden en "Fall" state
