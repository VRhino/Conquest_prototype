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

## Próximos Pasos
- Pruebas de integración completas
- Validación de rendimiento
- Documentación de API final
