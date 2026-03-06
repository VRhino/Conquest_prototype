# Bases de Datos del Proyecto

## Databases existentes

| Database | Clase | Ubicación | Descripción |
|----------|-------|-----------|-------------|
| ItemDatabase | `EnhancedItemDatabase` | `Assets/Resources/items/` | Base de datos de items, armas y equipamiento |
| AvatarPartDatabase | `AvatarPartDatabase` | `Assets/Resources/` | Partes visuales del avatar del héroe |
| DialogueEffectDatabase | `DialogueEffectDatabase` | `Assets/Resources/` | Efectos ejecutables desde diálogos con NPCs |
| SquadDatabase | `SquadDatabase` | `Assets/Resources/Squads/` | Datos base de todos los tipos de escuadra |
| StoreDatabase | `StoreDatabase` | `Assets/Resources/` | Configuración de tiendas y sus inventarios |
| MapDatabase | `MapDatabase` | `Assets/Resources/Maps/` | Datos de mapas de batalla |

## Servicios que gestionan estas bases de datos

- `HeroDataService` — gestión de héroes y sus datos
- `SquadDataService` — creación y gestión de escuadras
- `ItemService` — consultas y gestión de items
- `MapService` — acceso a datos de mapas
- `DataCacheService` — cache de atributos calculados del héroe
- `PlayerSessionService` — estado de sesión del jugador

Estos servicios están en `Assets/Scripts/Core/` y `Assets/Scripts/Inventory/Services/`.
