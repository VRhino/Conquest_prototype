# Campaña autocontenida en Unity

La conexión backend queda postergada por decisión del usuario. BronzeAgeCampaignConnection está deshabilitado en WorldMapCampaign. No hay peticiones, credenciales, comandos de servidor ni cambios del worldgen.

Controles: clic en ciudad o etiqueta abre ficha; Acercarse calcula ruta hasta el acceso. Entrar solo se habilita a 18 unidades del acceso con terreno detallado cargado. Doble clic sobre TerrainCollider detallado propone destino libre; la vista lejana no genera órdenes de movimiento. Detener viaje cancela el avance. Seguir jugador centra cámara; WASD mueve cámara, rueda zoom y botón derecho orbita. Ir a ciudad B es un atajo de prueba que también calcula ruta, no un itinerario fijo. Esc cierra selección. F3 activa diagnóstico.

WorldMapLocalNavigation: asset de datos locales, horneado de los 48 TerrainData y 186480 triángulos de ríos/lagos. Rejilla 512x384 de celdas 62.5u, 120337 transitables. Mar bajo 375.5u, pendiente muestreada superior a 25 grados o celda tocada por agua bloquean paso. No se crean puentes ni vados: por ahora los ríos bloquean. Es deliberadamente conservador junto a orillas; no es la navegación económica ni militar del motor. Bosques, reinos y edificios no alteran todavía el coste de marcha; las ciudades siguen siendo símbolos locales.

A* de ocho vecinos, sin diagonales entre celdas bloqueadas; penaliza diferencias de altura al elegir recorrido. Simplificación valida todos los segmentos. El movimiento horizontal se mantiene a 12u/s y la altura sigue Terrain cargado. La distancia y los segundos de la interfaz son longitud de ruta /12, no el coste de A*. El recorrido puede ser mayor que la recta. No constituye una garantía de accesibilidad de todas las regiones. La granularidad puede cerrar pasos estrechos o no detectar detalle de relieve menor que la celda.

El acceso de City B se ajustó al centro seco más próximo (búsqueda limitada a 140u) porque su celda anterior tocaba el río. No se movió la ciudad, ni nodos o agua canónicos. El recorrido A/B actual mide 1141.391u; la calibración histórica de 1200u de CampaignScale sigue siendo una prueba diferente intacta.

La línea dorada marca ruta restante; el streaming prioriza al actor mientras viaja y conserva como máximo nueve sectores cercanos. Terreno lejano permite inspección de cámara. El punto de entrada, cámara y ruta completa se conservan al volver desde SettlementScene; el recorrido vuelve detenido, sin avanzar mientras se está dentro.

Validación: tests sintéticos de desvío alrededor del agua, barrera cerrada, destino fuera del mundo y esquina diagonal; ruta A/B comprobada sin segmentos bloqueados; SHA del asset coincide con manifiesto. Play: 10 segundos simulados=120u, entrada lejana bloqueada, llegada=0 distancia restante, héroe Ready en SettlementScene, retorno al acceso B=0 y ruta de dos puntos preservada con nueve sectores. Captura worldmap-local-route.png revisada. No es validación de todos los cruces ni benchmark de rendimiento.

Reproducir: AgentScripts/BakeLocalCampaignNavigation.cs y ValidateLocalCampaignNavigation.cs. Los datos se hornean en Editor una vez; el runtime no carga los TerrainData lejanos para buscar rutas. El asset se verifica contra el SHA del mundo. La futura integración del motor debe sustituir este proveedor local.

Siguiente orden local: entidades y ciudades de prueba por ID (sin inventar recursos económicos), selección y acciones contextuales; representación de caravanas/ejércitos de prueba; transición de batalla con escenario explícito; guardado del estado local; finalmente máscara de niebla local. La UI y contratos deben mantener posible conectar al backend después.
