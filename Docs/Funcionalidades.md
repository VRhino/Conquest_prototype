# 🎮 Funcionalidades del Juego

## 📋 Índice

### 1. 🎯 Funcionalidades Core Implementadas
### 2. 🔄 Funcionalidades En Desarrollo  
### 3. 📅 Funcionalidades Planificadas
### 4. 💡 Funcionalidades Adicionales (Ideas)

---

## 1. 🎯 Funcionalidades Core Implementadas

### 🧙‍♂️ **Control del Héroe**
- ✅ Movimiento en tercera persona (WASD)
- ✅ Sistema de input con Unity Input System
- ✅ Input para habilidades de héroe (Q, E, R)

### 🛡️ **Control de Escuadras**
- ✅ Órdenes básicas (C: Seguir, X: Hold Position, V: Atacar)
- ✅ Cambio de formaciones con teclas F1-F4
- ✅ **NUEVO:** Cambio cíclico de formaciones con doble clic X
- ✅ Detección de posición del mouse para Hold Position

### 🧠 **Sistema de Formaciones**
- ✅ Formaciones básicas: Line, Dispersed, Testudo, Wedge
- ✅ Posicionamiento dinámico de unidades
- ✅ Marcadores visuales para Hold Position
- ✅ Rotación cíclica automática de formaciones

---

## 2. 🔄 Funcionalidades En Desarrollo

### 🧙‍♂️ **Sistema del Héroe**
- 🔄 Animaciones básicas (Idle, Run, Attack)
- 🔄 Sistema de stamina y cooldowns
- ✅ Sistema de salto eliminado
- 🔄 Implementación real de habilidades (Q, E, R)

### 🛡️ **Sistema de Escuadras**
- 🔄 Sistema de liderazgo para restricción de escuadras
- 🔄 IA básica de unidades en formación
- 🔄 Navegación con NavMesh

### ⚔️ **Sistema de Combate**
- 🔄 Combate del héroe con colliders animados
- 🔄 Sistema de tipos de daño (Blunt, Slashing, Piercing)
- 🔄 Cálculo de penetración y defensas
- 🔄 Combate de escuadras por intervalos

### 🎨 **Interfaz y HUD**
- 🔄 HUD básico de batalla
- 🔄 Minimapa funcional
- 🔄 UI de preparación de batalla
- 🔄 Sistema de marcadores de destino
- 🔄 Chat básico entre jugadores

### 🌐 **Multijugador**
- 🔄 Netcode for GameObjects implementado
- 🔄 Sincronización de héroes y escuadras
- 🔄 Servidor dedicado
- 🔄 Interpolación de movimiento

### 🏗️ **Mapas y Escenarios**
- 🔄 Sistema de captura de puntos
- 🔄 Supply points funcionales
- 🔄 Elementos destructibles
- 🔄 Zonas de spawn

### 🧬 **Progresión**
- 🔄 Sistema de niveles del héroe
- 🔄 Progresión de escuadras
- 🔄 Sistema de perks y habilidades
- 🔄 Guardado local de progreso

---

## 3. 📅 Funcionalidades Planificadas

### 🎭 **Clases de Héroe**
- 📅 Espada y Escudo
- 📅 Espada a Dos Manos
- 📅 Lanza
- 📅 Arco
- 📅 Habilidades exclusivas por clase

### 🪖 **Tipos de Escuadras**
- 📅 Escuderos
- 📅 Arqueros
- 📅 Piqueros
- 📅 Lanceros
- 📅 Habilidades y sinergias por tipo

### 🎯 **Modos de Juego**
- 📅 Asedio (Atacante vs Defensor)
- 📅 Captura de puntos
- 📅 Dominio territorial
- 📅 Escort (escoltar objetivos)

### 🏰 **Escenas del Juego**
- 📅 Feudo (hub social)
- 📅 Barracón (gestión de escuadras)
- 📅 Matchmaking
- 📅 Post-batalla

---

## 4. 💡 Funcionalidades Adicionales (Ideas)

### 🎮 **Mejoras de Gameplay**
- 💡 Modo táctico con cámara elevada
- 💡 Órdenes contextuales con click derecho
- 💡 Menú radial para órdenes avanzadas
- 💡 Sistema de flanqueo automático
- 💡 Coordinación entre múltiples escuadras
- 💡 **NUEVA:** Detección de rango por unidad más cercana al héroe (mejorar lógica Formed→Moving)
- 💡 **NUEVA:** Posicionamiento manual con X mantenida + mouse (feedback visual de markers)

### 🎨 **Calidad de Vida**
- 💡 Replay system
- 💡 Espectador avanzado
- 💡 Customización de HUD
- 💡 Presets de formaciones personalizados
- 💡 Macros de órdenes

### 🌟 **Características Avanzadas**
- 💡 Maquinaria de asedio
- 💡 Sistema de clima dinámico
- 💡 Efectos de terreno en combate
- 💡 Sistema de moral de tropas
- 💡 Eventos dinámicos en batalla

### 🎯 **Competitivo**
- 💡 Ranked matchmaking
- 💡 Temporadas competitivas
- 💡 Torneos automáticos
- 💡 Sistema de clanes
- 💡 Estadísticas avanzadas

---

## 📊 Estado General del Proyecto

| Categoría | Completado | En Desarrollo | Planificado |
|-----------|------------|---------------|-------------|
| **Core Gameplay** | 35% | 45% | 20% |
| **Combate** | 0% | 60% | 40% |
| **UI/UX** | 0% | 70% | 30% |
| **Multijugador** | 0% | 80% | 20% |
| **Progresión** | 0% | 40% | 60% |
| **Contenido** | 10% | 30% | 60% |

---

## 🎯 Próximas Prioridades

1. **Sistema de Liderazgo** - Restricciones de escuadras por loadout
2. **Animaciones Básicas** - Héroe y unidades
3. **Sistema de Stamina** - Implementación completa (sin salto)
4. **HUD Básico** - Interfaz funcional en motor
5. **Netcode Básico** - Primera implementación multijugador

---

## ⚠️ Notas Importantes

- ✅ **Sistema de salto eliminado:** Se removieron todas las referencias al salto del héroe del código y diseño
- **Prioridad en Core:** Enfocar en funcionalidades básicas antes de características avanzadas
- **Testing requerido:** Muchas funcionalidades necesitan pruebas en motor Unity

---

## 🔄 Mejoras Técnicas Específicas

### 🎯 **1. Optimización de Detección de Rango (Formed↔Moving)** ✅

**📋 Problema original:**
- La lógica evaluaba la posición individual de cada unidad vs el héroe
- Podía causar comportamiento inconsistente en formaciones grandes

**🔧 Solución implementada:**
- ✅ **Cambiado algoritmo para usar la unidad más cercana al héroe** como referencia
- ✅ **Aplicada la misma lógica de transición** a todo el escuadrón basado en esa distancia
- ✅ **Mejorada consistencia del comportamiento grupal**

**⚙️ Implementación técnica:**
```csharp
// ANTES - Evaluar cada unidad individualmente:
// foreach(unit) if(distance(unit, hero) > threshold) → Moving

// AHORA - Usar unidad más cercana como referencia:
public static bool isHeroInRange(DynamicBuffer<SquadUnitElement> units, 
    ComponentLookup<LocalTransform> transformLookup, float3 heroPosition, float range)
{
    float closestDistSq = float.MaxValue;
    bool hasValidUnit = false;
    
    foreach (var unitElement in units)
    {
        Entity unit = unitElement.Value;
        if (transformLookup.HasComponent(unit))
        {
            float3 unitPosition = transformLookup[unit].Position;
            float distSq = math.lengthsq(heroPosition - unitPosition);
            
            if (distSq < closestDistSq)
            {
                closestDistSq = distSq;
                hasValidUnit = true;
            }
        }
    }

    return hasValidUnit && closestDistSq <= range;
}
```

**🧩 Sistemas modificados:**
- `FormationPositionCalculator.isHeroInRange()` - Refactorizado para usar unidad más cercana
- `UnitFormationStateSystem` - Ahora usa lógica consistente para todo el escuadrón
- `UnitFollowFormationSystem` - Beneficiado por la nueva lógica unificada

### 🖱️ **2. Posicionamiento Manual con X Mantenida**

**📋 Funcionalidad:**
- **Input:** Mantener presionada la tecla `X`
- **Visual:** Mostrar markers de destino de todas las unidades del escuadrón
- **Interacción:** Markers siguen la posición del mouse en tiempo real
- **Confirmación:** Al soltar `X`, las unidades se mueven hacia sus markers

**🎨 Feedback visual:**
- Markers semi-transparentes con líneas de conexión al mouse
- Preview de la formación resultante
- Indicadores de terreno válido/inválido

**⚙️ Flujo de implementación:**
1. **Detectar X presionada** → Activar modo posicionamiento manual
2. **Mostrar markers** → Instanciar prefabs en posiciones de formación
3. **Seguir mouse** → Raycast a terreno + offset de formación
4. **Soltar X** → Confirmar posiciones y iniciar movimiento
5. **Cleanup** → Destruir markers y aplicar nuevas posiciones objetivo

**🧩 Sistemas involucrados:**
- `SquadControlSystem` - Detección de input extendido
- `ManualPositioningSystem` - ⚠️ **No implementado / Planificado** - Nuevo sistema para esta funcionalidad
- `FormationMarkerSystem` - ⚠️ **No implementado / Planificado** - Extensión del sistema actual de markers
- `TerrainRaycastSystem` - ⚠️ **No implementado / Planificado** - Validación de posiciones en terreno

---

*Última actualización: 3 de Julio, 2025*
