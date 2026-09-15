# Escala de bodyblock con 900 agentes

Fecha: 2026-09-14. Octava fase del pipeline de movimiento. Trabajo posterior al commit `7ec35082`.

## Problema medido

`UnitBodyblockSystem` limpiaba el diccionario espacial cada frame y creaba una nueva `List<int>` para cada celda ocupada. En una distribución dispersa de 900 agentes esto podía generar hasta 900 objetos administrados por frame, aunque las listas internas principales ya reutilizaban capacidad.

## Cambio

- Se mantiene el diccionario espacial administrado y el algoritmo de nueve celdas vecinas.
- Las listas de índices pasan a un pool persistente propiedad del sistema.
- Al comenzar el frame se reinicia el cursor del pool; cada bucket reutilizado se limpia y se asocia a la celda activa.
- El pool solo crece si un frame necesita más celdas simultáneas que todos los frames anteriores.

No se migra todavía a Native containers/Burst: el fixture no aporta evidencia que justifique ese coste y complejidad.

## Stress test

PlayMode crea una superficie NavMesh temporal de 60 × 60 m y 900 agentes reales distribuidos en una cuadrícula 30 × 30. Ejecuta un frame de calentamiento y mide el siguiente en el mismo hilo.

Resultado observado:

- **900 agentes**.
- **900 buckets reutilizados**.
- **0 bytes de asignación administrada** en el frame caliente medido.
- **0,86 ms** en la medición aislada y **0,88 ms** en la repetición dentro de la suite completa.
- **16,72 ms** al comprimir artificialmente los 900 agentes en un área de 1,45 × 1,45 m; **16,93 ms** en la validación final completa.
- **0 bytes de asignación administrada** también en el frame denso; el pool conserva sus buckets.
- **1/1 test aprobado** para la medición aislada y **9/9 PlayMode** en la suite completa.

Comando reproducible:

```powershell
unity test . --mode PlayMode --filter SquadNavigationRegressionTests.NineHundredBodyblockAgentsReuseSpatialBucketsAfterWarmup --output Logs/bodyblock-scale-allocation-results.xml --timeout 300 -- -nographics -logFile Logs/bodyblock-scale-allocation-tests.log
```

La validación final completa mantiene **58/58 EditMode**, **9/9 PlayMode** y **64 assemblies de Player Windows** compiladas correctamente.

## Interpretación y límites

La cifra no representa una batalla renderizada: no incluye animación, targeting, combate, cámaras, render ni coste completo del PlayerLoop. Sí demuestra que la estructura espacial deja de generar basura administrada después del calentamiento y que 900 agentes sin contactos densos no justifican por sí solos una reescritura Burst. Los **16,72–16,93 ms** del caso denso son un extremo físico patológico, no una expectativa de batalla normal, pero dejan identificado el coste cuadrático local cuando demasiados agentes comparten las mismas celdas.

La siguiente fase midió y simplificó `EnemyDetectionSystem` y `UnitTargetingSystem`; véase [Detección y targeting a escala](12_Deteccion_Targeting_Escala_2026-09-15.md). El siguiente perfil de bodyblock debe hacerse dentro de una batalla real para determinar si el caso denso exige Native containers/Burst o una cota explícita de vecinos.
