# Prueba Unity de calibración — factor 12

Fecha: 2026-10-12. Referencia prioritaria: `Calibracion_Distancias_Definicion.md` del worktree de Claude, incluidos §2.1, §9 y §10. Sustituye la prueba de 12 u/s y radios 90–540.

## Abrir y probar

Escena: `Assets/Scenes/WorldMapTools/Experiments/CalibrationFactor12/CalibrationFactor12.unity`. Abrir y pulsar Play. Comparte los sectores, el relieve, las texturas, las masas forestales y los datos de navegación de `WorldMapCampaign`; no duplica TerrainData ni genera otro mundo.

Durante esta escena el juego continúa en segundo plano, para poder comparar con documentos sin que el Editor congele el viaje; al deshabilitar la prueba se restaura el valor anterior. No cambia la configuración global del proyecto.

1. **Viajar A → B**: ruta seca de 360 unidades horizontales. Ligera: 60 segundos, sin penalizaciones por terreno.
2. **Cámara habitual**: seguimiento del símbolo y ancho real de 180 unidades a la profundidad del jugador. Equivale a 15 unidades estratégicas y a 30 segundos de movimiento horizontal en pantalla con ligera.
3. **Ver ambas ciudades**: encuadre mayor para comparar sus entornos. No es el zoom habitual.
4. **Inicio / N1–N5**: radios de referencia 72 / 120 / 180 / 240 / 300 / 360. Ambas ciudades usan el mismo nivel; el solapamiento se recorta por su bisectriz. Estos anillos no implementan las reglas de propiedad del motor.
5. Cambiar velocidad reinicia la ruta, evitando mezclar tiempos. WASD desplaza la cámara, rueda cambia zoom y botón derecho gira.

## Contrato compartido

`Assets/WorldMap/Campaign/ScaleContract.asset` contiene los parámetros. La escena de campaña normal también utiliza este perfil para velocidad, proximidad, cámara y tamaño de símbolos. Su pareja de ciudades existente conserva sus posiciones; la pareja de 360 unidades pertenece exclusivamente a esta prueba.

```text
Unity = estratégicas × 12
Unity/segundo = estratégicas/tick × 12 / 60
Mundo: 32.000 × 24.000 Unity = 2.666,6667 × 2.000 estratégicas
```

| Movimiento | Unity/s | Tiempo para 360 Unity |
|---|---:|---:|
| Ligera | 6 | 60 s |
| Héroe solo | 6,6 | 54,545 s |
| Montada | 8,4 | 42,857 s |
| Caravana de caballo | 7,2 | 50 s |
| Caravana de camello | 5,6 | 64,286 s |
| Media / buey | 4,8 | 75 s |
| Pesada | 3,6 | 100 s |
| Fundación de vuelta | 3 | 120 s |

Miniatura urbana: radio máximo conservador 9,6 Unity, normalizado con las geometrías de todos sus niveles; la geometría activa puede ocupar menos. La figura estática anterior tenía altura 10 Unity. La sustituye el héroe modular animado de Feudo: **escala 6**, aprobada por el usuario después de probar locomoción, con una altura visual aproximada de 12 Unity en reposo. `heroSymbolScale` permite modificar esa proporción sin tocar las distancias ni las velocidades. Puerta: 24 Unity. El radio de encuentro 36 se registra en el contrato, pero esta prueba no incorpora encuentros.

## Validación ejecutada

`AgentScripts/ValidateCampaignCalibration.cs`, ejecutado mediante Pipeline en Play, comprueba velocidades, viaje completo y mitad de recorrido para los ocho perfiles, actualizaciones cortas de movimiento, acceso a 23 y rechazo a 25 unidades, radios y recorte de rivales. También mide el ancho mediante intersecciones de rayos con el plano horizontal a la altura del foco, verifica extremos de pantalla en 16:9, 4:3 y 21:9 y comprueba que volver al mapa conserva ese encuadre.

Resultado reproducible: `Assets/Scenes/WorldMapTools/Experiments/CalibrationFactor12/Validation.txt`. El control del tiempo usa segundos simulados de movimiento; las pausas de carga o del Editor no representan tiempo de viaje. La comprobación de pantalla se refiere al plano de profundidad del jugador, no a un ancho constante en todas las alturas y profundidades de una cámara perspectiva.

Preparación: menú **BronzeAge → World map → Prepare factor 12 calibration**, con Play detenido y escena guardada. `WorldMapCalibrationBuilder` conserva el perfil existente para permitir ajustes manuales y reconstruye la escena de ensayo mediante las APIs del Editor. La escena experimental no se añade a las escenas de juego del build.

## Alcance pendiente

Esta entrega aplica la presentación y la prueba local. Los focos, fronteras, distritos y exclusiones quedan expresados en el perfil, sin redistribuir aún campamentos o recursos. No modifica el backend, el tick económico, la detección por subpasos, la propiedad de nodos ni las reglas de influencia. Los símbolos de caravana se ensayan con la figura existente; se compara velocidad, no su arte final.

La geografía conserva su SHA256 `51f1ef7b9836be40ea89563ddfc3575c93c23610f9865b8a25f41341071d2f2d`. Las masas forestales mantienen su tamaño anterior: su proporción frente a la miniatura urbana se evalúa en esta prueba, sin alterar simultáneamente la densidad o el relieve.

## Héroe animado de campaña

Desde 2026-10-13, el ensayo admite también destinos por clic, ruta visible y pausa/reanudación, conservando la prueba A–B. Véase [Movimiento por clic](WorldMap_Movimiento_Por_Clic.md).

`Assets/WorldMap/Campaign/Symbols/Hero.prefab` deriva del héroe por defecto del registro visual de FeudoScene: `ModularHeroVisual`, avatar humanoide y `AC_Polygon_Masculine` de Synty. La apariencia de ensayo usa la misma base de partes del avatar y conserva 14 piezas visibles. No altera el prefab de Feudo ni sus controles.

`WorldMapHeroPresentation` observa el desplazamiento horizontal confirmado por el controlador de campaña y alimenta los parámetros del Animator. Usa las reglas de gait y hashes compartidos, ajusta la cadencia al tamaño de la miniatura y desactiva root motion. El héroe de campaña no contiene entrada WASD, CharacterController, NavMeshAgent, EntityVisualSync ni controladores de combate. WASD del worldmap desplaza exclusivamente la cámara.

El tamaño aceptado manualmente por el usuario es **escala uniforme 6**, mantenida en `ScaleContract.asset` para que reconstruir el ensayo no vuelva a normalizarlo con una medida de piel incorrecta. En Play, los límites visibles en reposo miden aproximadamente 11,85 Unity de alto, con pequeñas variaciones por animación. El antiguo objetivo de altura 10 corresponde al símbolo estático anterior, no a este héroe.

`AgentScripts/ValidateWorldMapHero.cs` comprueba en Play escala, tamaño, avatar y controlador, reposo real, clip de caminar, parada y reanudación, giro, despeje de todos los flags de movimiento y ausencia de desplazamiento producido por animación. También valida el clip de correr con una velocidad temporal que restaura al terminar, y el reinicio de observación tras teletransporte. Resultado: `CalibrationFactor12/HeroValidation.txt`. La validación general de distancias se ejecutó nuevamente con el héroe animado.
