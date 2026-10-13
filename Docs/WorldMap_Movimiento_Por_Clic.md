# Movimiento local del worldmap · 2026-10-13

La campaña y el ensayo CalibrationFactor12 permiten elegir un destino terrestre con un clic izquierdo. En la campaña, pulsar un asentamiento selecciona su menú; «Acercarse» ordena ir a su acceso y «Entrar» conserva la comprobación de proximidad. La interfaz consume sus clics antes de ordenar viajes.

## Controles y presentación

- Clic sobre el suelo: calcular una nueva ruta desde la posición actual del héroe.
- Espacio o botón de pausa: detener/reanudar el recorrido disponible.
- WASD: desplazar la cámara; botón derecho: girarla; rueda: zoom.
- «Seguir jugador» / «Cámara habitual»: recuperar el seguimiento del héroe.
- Línea dorada: recorrido restante; anillo: destino. En pausa se atenúan. El panel muestra distancia y tiempo restante a la velocidad actual.

Se reutilizan WorldMapCampaignController, WorldMapLocalNavigation y WorldMapLocalRouteVisual. El ensayo conserva el trayecto A–B de 360 unidades y su botón Reiniciar. No cambia la geografía ni la escala 6 del héroe, el factor 12, los radios o las velocidades aprobadas.

## Navegación y streaming

LocalNavigation.asset se deriva del Terrain y las mallas de agua canónicas, con el mismo SHA del manifiesto. La cuadrícula pasa de 512×384 (62,5 unidades/celda) a 1024×768 (31,25). Sus datos de altura y bloqueos ocupan aproximadamente 3,75 MiB antes de la sobrecarga del motor. No necesita instanciar los 48 sectores para buscar un camino.

A* calcula en tandas de 512 nodos por fotograma. La simplificación vuelve a comprobar agua, esquinas y pendiente, evitando que un atajo visual atraviese un obstáculo. Una orden nueva cancela la búsqueda anterior; una pausa también puede cancelarla. El destino exacto bloqueado se rechaza, sin trasladarlo silenciosamente a otra orilla. Si falla una búsqueda se conserva el recorrido anterior pausado, que se puede reanudar.

Los clics usan el Terrain detallado si existe; fuera de sus colliders, intersectan el campo de alturas global. La cámara y el héroe mantienen barrios de hasta nueve sectores cada uno; un sector adicional anticipa el siguiente tramo. Máximo de 19 sectores deseados, con descarga de los sobrantes. El movimiento espera si aún no está cargada la altura detallada del siguiente punto.

El coste de pendiente influye en la elección de ruta. La velocidad de desplazamiento permanece constante: la duración mostrada es distancia horizontal restante / velocidad del perfil, sin sumar esperas de carga. No se introducen penalizaciones nuevas por bosque o terreno.

## Verificación y límites

ConfigureWorldMapTravel.cs configura ambas escenas mediante las APIs de Unity. BakeLocalCampaignNavigation.cs regenera únicamente el activo de navegación; no mueve ciudades, accesos ni recursos. ValidateLocalCampaignNavigation.cs comprueba el rodeo de agua, barreras cerradas, esquinas y el trayecto real. ValidateWorldMapTravel.cs valida clic proyectado, sustitución desde la posición actual, pausa/reanudación, terreno remoto, streaming independiente y cruce real de sector. Los informes quedan en Docs/*Navigation* y WorldMap_ClickTravel_Validation.txt.

Esta navegación es local de prototipo. Todo el agua representada por la máscara se considera infranqueable; todavía no existen permisos de vados, puentes, navegación marítima ni órdenes de servidor. La celda de 31,25 sigue siendo una aproximación: los pasos menores necesitan una representación de navegación específica antes de integrarse con las reglas definitivas del motor. El bosque decorativo y las miniaturas urbanas no actúan como colliders de navegación.
