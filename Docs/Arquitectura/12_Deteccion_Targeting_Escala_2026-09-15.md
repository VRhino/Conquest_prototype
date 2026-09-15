# Detección y targeting a escala

Fecha: 2026-09-15. Novena fase del pipeline de control y movimiento. Trabajo posterior al commit `7ec35082`.

## Problema

`EnemyDetectionSystem` ya calculaba una lista de entidades candidatas por squad en `SquadTargetEntity`, pero después copiaba la lista completa al buffer `UnitDetectedEnemy` de cada unidad. En un encuentro de 450 contra 450 unidades esto suponía aproximadamente **405.000 escrituras de buffer por frame** para representar información idéntica.

La copia no expresaba estado propio de la unidad: la selección individual del objetivo pertenece a `UnitTargetingSystem` y se almacena en `UnitCombatComponent.target`.

## Contrato actual

```text
EnemyDetectionSystem
  ├─ DetectedEnemy[]       candidatos a nivel squad
  └─ SquadTargetEntity[]   entidades enemigas compartidas por squad
              ↓
UnitTargetingSystem
  ├─ lee el buffer compartido
  ├─ conserva un target todavía válido o elige el candidato más cercano
  └─ escribe UnitCombatComponent.target por unidad
```

- `EnemyDetectionSystem` limpia y repuebla los dos buffers compartidos una vez por squad.
- `UnitTargetingSystem` consume directamente `SquadTargetEntity`.
- Fuera de `InCombat`, o con la lista vacía, limpia targets y `IsEngagingTag` sin crear mapas temporales.
- En combate, los mapas de reparto se reservan con capacidad acorde a la lista de candidatos para evitar crecimientos intermedios.
- Se eliminó el componente y archivo `UnitDetectedEnemy` y también su creación durante el spawn.
- `CombatGizmosDebug` y `RangedUnitsDiagnostic` fueron adaptados al contrato compartido.
- La política existente se conserva: límite de concentración para melee y distribución separada de unidades a distancia.

## Pruebas y mediciones

El fixture EditMode construye dos squads enemigos con 450 unidades cada uno, calienta ambos sistemas y mide un frame posterior en el mismo hilo.

Resultado observado:

- `EnemyDetectionSystem`: **0,62 ms**, **0 bytes** administrados y **900 entradas** compartidas entre los dos squads.
- `UnitTargetingSystem`: **3,08 ms**, **0 bytes** administrados y **900 objetivos asignados**.
- Una regresión funcional adicional demuestra que el targeting consume el buffer compartido y elige el enemigo más cercano.
- Otra regresión verifica que abandonar combate elimina tanto el target obsoleto como el estado `IsEngagingTag`.
- Validación final: **61/61 EditMode**, **9/9 PlayMode** y **64 assemblies** de Player Windows compiladas.

Las cifras son mediciones aisladas del fixture ECS y no equivalen al tiempo total de una batalla renderizada.

## Complejidad y límites pendientes

La eliminación reduce almacenamiento y escrituras de `O(unidades × candidatos)` a `O(candidatos)` en detección. El targeting todavía compara cada unidad que necesita un objetivo contra los candidatos de su squad; su peor caso continúa siendo `O(unidades × candidatos)`.

No se introduce aún un índice espacial adicional en targeting: los **3,08 ms** medidos para 900 unidades no justifican por sí solos la complejidad. La siguiente decisión debe basarse en un perfil de una batalla real con render, animación y combate activos. Si targeting aparece entre los costes dominantes, las opciones son partición espacial de candidatos, reparto estable por grupos o jobs Burst con contenedores nativos.
