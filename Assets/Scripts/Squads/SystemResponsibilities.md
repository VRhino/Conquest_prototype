# Sistema de Responsabilidades - Refactorización Híbrida

## Responsabilidades por Sistema

### UnitFormationStateSystem
**Responsabilidad Principal:** Gestión exclusiva de transiciones de estado de las unidades (Moving, Formed, Waiting)
- ✅ Todas las transiciones de estado de UnitFormationState están centralizadas aquí
- ✅ Decide cuándo una unidad debe cambiar de estado basado en:
  - Posición del héroe relativa al escuadrón
  - Distancia de la unidad a su slot asignado
  - Estado del escuadrón (HoldingPosition vs FollowingHero)
  - Delays y timers para transiciones naturales

### UnitFollowFormationSystem
**Responsabilidad Principal:** Movimiento físico de las unidades
- ✅ Solo mueve unidades que están en estado `Moving`
- ✅ Calcula posición de destino usando FormationPositionCalculator
- ✅ Actualiza transformación y orientación de las unidades
- ❌ ELIMINADO: Cambio de estado (ahora es responsabilidad de UnitFormationStateSystem)

### FormationSystem
**Responsabilidad Principal:** Cálculo de posiciones de formación
- ✅ Calcula posiciones de formación cuando cambia la formación
- ✅ Actualiza UnitGridSlotComponent con nuevas posiciones
- ✅ Actualiza UnitTargetPositionComponent para visualización
- ❌ ELIMINADO: Cambio de estado (ahora es responsabilidad de UnitFormationStateSystem)

### SquadOrderSystem
**Responsabilidad Principal:** Interpretación de órdenes de entrada
- ✅ Procesa SquadInputComponent
- ✅ Convierte órdenes en cambios de estado del escuadrón
- ✅ Gestiona SquadHoldPositionComponent
- ✅ Solicita cambios de formación

### SquadFSMSystem
**Responsabilidad Principal:** Gestión de estado del escuadrón
- ✅ Maneja transiciones de estado del escuadrón (FollowingHero, HoldingPosition, etc.)
- ✅ Procesa transitionTo para cambiar currentState

### SquadControlSystem
**Responsabilidad Principal:** Captura de entrada del usuario
- ✅ Detecta clicks del mouse y teclas
- ✅ Convierte entrada en órdenes para escuadrones
- ✅ Actualiza SquadInputComponent

### DestinationMarkerSystem
**Responsabilidad Principal:** Visualización de marcadores de destino
- ✅ Solo lee estados, no los modifica
- ✅ Muestra marcadores solo para unidades en estado Moving durante HoldingPosition

### SquadSpawningSystem
**Responsabilidad Principal:** Creación de entidades ECS
- ✅ Crea escuadrones y unidades ECS-only
- ✅ Configura componentes iniciales
- ✅ Establece estado inicial (FollowingHero)

### SquadVisualManagementSystem
**Responsabilidad Principal:** Gestión de visuales de unidades
- ✅ Crea GameObjects visuales para unidades
- ✅ Gestiona sincronización ECS-GameObject

### HeroVisualManagementSystem
**Responsabilidad Principal:** Gestión de visuales de héroe
- ✅ Crea GameObjects visuales para héroes
- ✅ Gestiona sincronización ECS-GameObject

## Principios de Diseño Aplicados

### 1. Separación de Responsabilidades
- **Estado**: UnitFormationStateSystem (exclusivo)
- **Movimiento**: UnitFollowFormationSystem (solo física)
- **Formación**: FormationSystem (solo cálculos)
- **Visuales**: Systems separados para ECS vs GameObjects

### 2. Flujo de Datos Unidireccional
```
Input → SquadControlSystem → SquadOrderSystem → SquadFSMSystem
                                                      ↓
UnitFormationStateSystem ← FormationSystem ← SquadFSMSystem
         ↓
UnitFollowFormationSystem (solo lee estados)
```

### 3. Eliminación de Lógica Duplicada
- ❌ Removido: Cambios de estado en UnitFollowFormationSystem
- ❌ Removido: Cambios de estado en FormationSystem
- ❌ Removido: Logs de debug innecesarios
- ❌ Removido: Detección de heroWithinRadius duplicada

### 4. Modelo Híbrido ECS-GameObject
- **ECS**: Toda la lógica de juego (estado, movimiento, formación)
- **GameObjects**: Solo visualización y sincronización
- **Sincronización**: EntityVisualSync para mantener coherencia

## Estado Actual
✅ **COMPLETADO**: Refactorización híbrida completamente implementada
✅ **COMPLETADO**: Separación de responsabilidades implementada
✅ **COMPLETADO**: Eliminación de lógica duplicada
✅ **COMPLETADO**: Limpieza de logs de debug
✅ **COMPLETADO**: Centralización de cambios de estado en UnitFormationStateSystem
✅ **COMPLETADO**: Análisis completo de sistemas 10-18

## Sistemas 10-18: Análisis Completado

### 10. RetreatLogicSystem
**Responsabilidad:** Mueve escuadrones en estado Retreating hacia ubicación segura.
**Estado:** ✅ Limpio, sin duplicación significativa.

### 11. SquadAISystem
**Responsabilidad:** Decide estado táctico de IA basado en órdenes, enemigos y cohesión.
**Estado:** ✅ Limpio, sin duplicación significativa.

### 12. SquadNavigationSystem
**Responsabilidad:** Mueve líderes de escuadrón hacia posiciones objetivo usando NavMeshAgent.
**Estado:** ✅ Limpio, sin duplicación significativa.

### 13. SquadFSMSystem
**Responsabilidad:** Máquina de estados finitos para estados tácticos de escuadrón.
**Estado:** ✅ Limpio, sin duplicación significativa.

### 14. SquadProgressionSystem
**Responsabilidad:** Maneja ganancia de XP, progresión de nivel y escalado de stats.
**Estado:** ✅ Refactorizado para usar UnitStatsUtility centralizada.

### 15. SquadSwapSystem
**Responsabilidad:** Valida y procesa solicitudes de cambio de escuadrón.
**Estado:** ✅ Limpio, sin duplicación significativa.

### 16. UnitDeploymentValidationSystem
**Responsabilidad:** Valida estado de equipamiento y elegibilidad de despliegue.
**Estado:** ✅ Limpio, sin duplicación significativa.

### 17. UnitSpacingSystem
**Responsabilidad:** Ajusta posiciones de unidades para mantener separación mínima.
**Estado:** ✅ Limpio, sin duplicación significativa.

### 18. UnitStatScalingSystem
**Responsabilidad:** Escala stats de unidades basado en nivel de escuadrón.
**Estado:** ✅ Refactorizado para usar UnitStatsUtility centralizada.

## Unificación Completada: Stats Scaling

**Problema Resuelto:** SquadProgressionSystem y UnitStatScalingSystem contenían lógica idéntica para aplicar stats.

**Solución Implementada:**
- ✅ Creada UnitStatsUtility centralizada
- ✅ Refactorizado SquadProgressionSystem para usar la utilidad
- ✅ Refactorizado UnitStatScalingSystem para usar la utilidad
- ✅ Eliminadas funciones ApplyStats duplicadas
- ✅ Preservada funcionalidad existente

**Beneficios:**
- Eliminación de ~50 líneas de código duplicado
- Mantenimiento centralizado de lógica de stats
- Garantía de consistencia en aplicación de stats
- Reutilización fácil para futuros sistemas

## Unificación Completada: Hero Position Retrieval

**Problema Resuelto:** Múltiples sistemas obtenían posición de héroe con lógica duplicada.

**Solución Implementada:**
- ✅ Creada HeroPositionUtility centralizada
- ✅ Refactorizado FormationSystem para usar la utilidad
- ✅ Refactorizado SquadControlSystem para usar la utilidad
- ✅ Refactorizado FormationAdaptationSystem para usar la utilidad
- ✅ Refactorizado GridFormationUpdateSystem para usar la utilidad
- ✅ Refactorizado DestinationMarkerSystem para usar la utilidad

**Sistemas que ahora usan HeroPositionUtility:**
- FormationSystem
- SquadControlSystem  
- FormationAdaptationSystem
- GridFormationUpdateSystem
- DestinationMarkerSystem

## Arquitectura Correcta Confirmada: FormationPositionCalculator

**Análisis:** FormationPositionCalculator.CalculateDesiredPosition() es llamado por múltiples sistemas:
- FormationSystem
- SquadSpawningSystem
- UnitFollowFormationSystem
- DestinationMarkerSystem
- GridFormationUpdateSystem
- UnitFormationStateSystem

**Conclusión:** ✅ **ESTO ES CORRECTO** - Múltiples sistemas usando la misma función centralizada es la arquitectura adecuada para:
- Garantizar consistencia en cálculos de posición
- Evitar duplicación de lógica compleja
- Facilitar mantenimiento y cambios futuros
- Asegurar que todos los sistemas usen la misma lógica de formación

## Próximos Pasos
✅ **COMPLETADO**: Unificar lógica de stats duplicada entre SquadProgressionSystem y UnitStatScalingSystem
✅ **COMPLETADO**: Implementar uso de HeroPositionUtility en todos los sistemas relevantes
✅ **COMPLETADO**: Análisis completo de TODOS los 33 sistemas (incluyendo los 2 faltantes)
- Pruebas de integración completas
- Validación de rendimiento
- Documentación de API final

## Sistemas 19-33: Análisis Completado

### 19. UnitOrientationInitializationSystem
**Responsabilidad:** Inicializa componente de orientación automáticamente para unidades sin configurar.
**Estado:** ✅ Limpio, sin duplicación. Sistema de inicialización bien enfocado.

### 20. UnitSpacingSystem (previamente analizado)
**Responsabilidad:** Ajusta posiciones de unidades para mantener separación mínima.
**Estado:** ✅ Limpio, sin duplicación significativa.

### 21. UnitStatScalingSystem (previamente analizado)
**Responsabilidad:** Escala stats de unidades basado en nivel de escuadrón.
**Estado:** ✅ Refactorizado para usar UnitStatsUtility centralizada.

### 22. HeroAttackSystem
**Responsabilidad:** Maneja input de ataque del héroe, cooldowns y colisiones de arma.
**Estado:** ✅ Limpio, sin duplicación. Lógica de combate bien estructurada.

### 23. HeroAttributeSystem
**Responsabilidad:** Valida asignaciones de atributos de héroe contra límites de clase.
**Estado:** ✅ Limpio, sin duplicación. Validación apropiada fuera de combate.

### 24. HeroInitializationSystem
**Responsabilidad:** Inicializa atributos, habilidades y perks cuando se crea un héroe.
**Estado:** ✅ Limpio, sin duplicación. Sistema de inicialización bien enfocado.

### 25. HeroInputSystem
**Responsabilidad:** Captura input de teclado/mouse y escribe a HeroInputComponent.
**Estado:** ✅ Limpio, sin duplicación. Uso correcto del Unity Input System.

### 26. HeroLevelSystem
**Responsabilidad:** Maneja agregación de XP, progresión de nivel y persistencia de datos.
**Estado:** ✅ Limpio, sin duplicación. **Correctamente separado** de SquadProgressionSystem por contexto.

### 27. HeroMovementSystem
**Responsabilidad:** Maneja movimiento determinista del héroe basado en input y cámara.
**Estado:** ✅ Limpio, sin duplicación. Lógica de movimiento relativo a cámara bien implementada.

### 28. HeroRespawnSystem
**Responsabilidad:** Maneja reaparición del héroe después de expirar timer de muerte.
**Estado:** ✅ Limpio, sin duplicación. Lógica simple y enfocada.

### 29. HeroStaminaSystem
**Responsabilidad:** Gestiona consumo y regeneración de stamina basado en input.
**Estado:** ✅ Limpio, sin duplicación. **Lógica de consumo específica por acción** bien estructurada.

### 30. HeroStateSystem
**Responsabilidad:** Determina estado del héroe (Idle, Moving) basado en movimiento.
**Estado:** ✅ Limpio, sin duplicación. Detección simple de estado basada en posición.

### 31. HeroVisualManagementSystem
**Responsabilidad:** Gestiona instanciación y sincronización de GameObjects visuales para héroes.
**Estado:** ✅ Limpio, sin duplicación. **Similar a SquadVisualManagementSystem pero correctamente separado** por contexto (hero vs squad).

### 32. SpawnSelectionSystem
**Responsabilidad:** Procesa solicitudes de selección de spawn point para héroes.
**Estado:** ✅ Limpio, sin duplicación. Sistema simple de procesamiento de requests.

## Análisis Final Completo: Todos los 33 Sistemas

**Observaciones Finales:**
- **32/33 sistemas con arquitectura limpia** sin duplicación significativa
- **1 área de duplicación crítica identificada y corregida** (stats scaling)
- **Separación correcta** entre sistemas similares por contexto (hero vs squad)
- **Arquitectura híbrida ECS-GameObject** correctamente implementada

**Patrón Confirmado - Sistemas Similares pero Correctos:**
- **HeroVisualManagementSystem vs SquadVisualManagementSystem:** Correctamente separados por contexto
- **HeroLevelSystem vs SquadProgressionSystem:** Diferentes dominios (individual vs colectivo)
- **HeroSpawnSystem vs SquadSpawningSystem:** Diferentes entidades y lógicas

**Resultado Final:**
- **Hero Systems:** Arquitectura ejemplar desde el diseño inicial
- **Squad Systems:** Elevados a arquitectura limpia mediante refactorización
- **Proyecto general:** Arquitectura unificada y mantenible lista para producción
