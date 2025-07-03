# Lista Completa de Sistemas - Squad y Hero

## SISTEMAS DE SQUAD

### 1. **DestinationMarkerSystem**
**Descripción**: System that creates and manages destination markers for units. Shows a visual marker where each unit is supposed to move to.
**Responsabilidad**: Visualización de marcadores de destino para unidades en movimiento

### 2. **FormationAdaptationSystem**
**Descripción**: System that handles formation adaptation based on squad composition and situation.
**Responsabilidad**: Adaptación automática de formaciones según composición y situación

### 3. **FormationGridSystem**
**Descripción**: System that manages formation grids for visual representation.
**Responsabilidad**: Gestión de grillas de formación para representación visual

### 4. **FormationSystem**
**Descripción**: Assigns target positions to squad units based on the selected formation.
**Responsabilidad**: Asignación de posiciones objetivo a unidades según formación seleccionada

### 5. **GridFormationUpdateSystem**
**Descripción**: System that updates grid formations when conditions change.
**Responsabilidad**: Actualización de formaciones en grilla cuando cambian las condiciones

### 6. **RetreatLogicSystem**
**Descripción**: System that handles retreat logic for squads under certain conditions.
**Responsabilidad**: Lógica de retirada para escuadrones bajo ciertas condiciones

### 7. **SquadAISystem**
**Descripción**: System that handles AI behavior for squads not controlled by players.
**Responsabilidad**: Comportamiento IA para escuadrones no controlados por jugadores

### 8. **SquadControlSystem**
**Descripción**: Reads player hotkeys and writes the corresponding commands to the SquadInputComponent of the active squad.
**Responsabilidad**: Captura de entrada del jugador y conversión a comandos de escuadrón

### 9. **SquadFSMSystem**
**Descripción**: System that manages squad finite state machine transitions.
**Responsabilidad**: Gestión de transiciones de máquina de estados finitos del escuadrón

### 10. **SquadNavigationSystem**
**Descripción**: System that handles squad navigation and pathfinding.
**Responsabilidad**: Navegación y pathfinding de escuadrones

### 11. **SquadOrderSystem**
**Descripción**: System that interprets the SquadInputComponent and updates SquadStateComponent accordingly.
**Responsabilidad**: Interpretación de órdenes de entrada y actualización de estado del escuadrón

### 12. **SquadProgressionSystem**
**Descripción**: System that handles squad progression and stat improvements.
**Responsabilidad**: Progresión del escuadrón y mejoras de estadísticas

### 13. **SquadSpawningSystem**
**Descripción**: Spawns squad entities and their units when a hero enters the scene.
**Responsabilidad**: Creación de entidades de escuadrón y sus unidades

### 14. **SquadSwapSystem**
**Descripción**: System that handles swapping between different squads.
**Responsabilidad**: Intercambio entre diferentes escuadrones

### 15. **SquadVisualManagementSystem**
**Descripción**: System that manages the visual representation of squads.
**Responsabilidad**: Gestión de representación visual de escuadrones

### 16. **UnitDeploymentValidationSystem**
**Descripción**: System that validates unit deployment positions and formations.
**Responsabilidad**: Validación de posiciones de despliegue y formaciones de unidades

### 17. **UnitFollowFormationSystem**
**Descripción**: Moves each unit of a squad towards its assigned formation slot relative to the squad leader.
**Responsabilidad**: Movimiento físico de unidades hacia sus posiciones de formación

### 18. **UnitFormationStateSystem**
**Descripción**: Sistema que gestiona exclusivamente los estados de formación de las unidades (Moving, Formed, Waiting).
**Responsabilidad**: Gestión de transiciones de estado de formación de unidades

### 19. **UnitOrientationInitializationSystem**
**Descripción**: System that initializes unit orientation settings.
**Responsabilidad**: Inicialización de configuración de orientación de unidades

### 20. **UnitSpacingSystem**
**Descripción**: System that manages spacing between units in formations.
**Responsabilidad**: Gestión del espaciado entre unidades en formaciones

### 21. **UnitStatScalingSystem**
**Descripción**: System that scales unit stats based on level and other factors.
**Responsabilidad**: Escalado de estadísticas de unidades según nivel y factores

## SISTEMAS DE HERO

### 1. **HeroAttackSystem**
**Descripción**: System that handles hero attack mechanics and combat interactions.
**Responsabilidad**: Mecánicas de ataque del héroe e interacciones de combate

### 2. **HeroAttributeSystem**
**Descripción**: System that manages hero attributes and stat calculations.
**Responsabilidad**: Gestión de atributos del héroe y cálculos de estadísticas

### 3. **HeroInitializationSystem**
**Descripción**: System that initializes hero components and settings.
**Responsabilidad**: Inicialización de componentes y configuración del héroe

### 4. **HeroInputSystem**
**Descripción**: System that handles hero input and movement commands.
**Responsabilidad**: Manejo de entrada y comandos de movimiento del héroe

### 5. **HeroLevelSystem**
**Descripción**: System that manages hero leveling and experience progression.
**Responsabilidad**: Gestión de nivel del héroe y progresión de experiencia

### 6. **HeroMovementSystem**
**Descripción**: System that handles hero movement mechanics and physics.
**Responsabilidad**: Mecánicas de movimiento del héroe y física

### 7. **HeroRespawnSystem**
**Descripción**: System that handles hero respawning after death.
**Responsabilidad**: Manejo de reaparición del héroe después de la muerte

### 8. **HeroSpawnSystem**
**Descripción**: Places the local hero at the selected spawn point at the start of the match and after a respawn.
**Responsabilidad**: Colocación del héroe en el punto de spawn inicial y después de reaparición

### 9. **HeroStaminaSystem**
**Descripción**: System that manages hero stamina and energy mechanics.
**Responsabilidad**: Gestión de resistencia y mecánicas de energía del héroe

### 10. **HeroStateSystem**
**Descripción**: Sistema que gestiona los estados del héroe (Idle, Moving, Attacking, etc.).
**Responsabilidad**: Gestión de estados del héroe

### 11. **HeroVisualManagementSystem**
**Descripción**: System that manages the visual representation of heroes.
**Responsabilidad**: Gestión de representación visual de héroes

### 12. **SpawnSelectionSystem**
**Descripción**: System that handles spawn point selection for heroes.
**Responsabilidad**: Selección de puntos de spawn para héroes

## RESUMEN DE CATEGORÍAS

### **Sistemas de Entrada y Control**
- SquadControlSystem
- HeroInputSystem
- SquadOrderSystem

### **Sistemas de Estado**
- SquadFSMSystem
- UnitFormationStateSystem  
- HeroStateSystem

### **Sistemas de Movimiento**
- UnitFollowFormationSystem
- HeroMovementSystem
- SquadNavigationSystem

### **Sistemas de Formación**
- FormationSystem
- FormationAdaptationSystem
- FormationGridSystem
- GridFormationUpdateSystem

### **Sistemas de Spawn/Creación**
- SquadSpawningSystem
- HeroSpawnSystem
- SpawnSelectionSystem

### **Sistemas de Visualización**
- SquadVisualManagementSystem
- HeroVisualManagementSystem
- DestinationMarkerSystem

### **Sistemas de Progresión**
- SquadProgressionSystem
- HeroLevelSystem
- UnitStatScalingSystem
- HeroAttributeSystem

### **Sistemas de Combate**
- HeroAttackSystem
- RetreatLogicSystem

### **Sistemas de Inicialización**
- HeroInitializationSystem
- UnitOrientationInitializationSystem

### **Sistemas de Validación**
- UnitDeploymentValidationSystem

### **Sistemas de Gestión**
- SquadSwapSystem
- HeroStaminaSystem
- UnitSpacingSystem
- HeroRespawnSystem

### **Sistemas de IA**
- SquadAISystem

## TOTAL: 33 SISTEMAS
- **Squad Systems**: 21 sistemas
- **Hero Systems**: 12 sistemas
