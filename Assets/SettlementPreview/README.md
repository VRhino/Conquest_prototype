# Asentamiento dinámico y recorrido con héroe

Abrir `Assets/Scenes/SettlementPreview.unity` **sola**, pulsar Play y elegir **Recorrer asentamiento**. La escena guardada ya incluye el modo a pie y la subescena `SettlementHeroWorld`. Si se reconstruye desde cero, usar Tools > BronzeAge > Crear o abrir asentamiento de prueba y después Preparar modo a pie.

Lee cualquier proyección compatible asignada en `source`. La escena usa por defecto `settlement2.json`, una exportación del Lab; `settlement.json` conserva la captura HTTP original. Si el JSON contiene un solo asentamiento, `settlementId` se deja vacío y se resuelve por su `id`; si contiene varios, hay que indicar el ID deseado en el Inspector. Así, cambiar el TextAsset no obliga a reescribir el ID de la escena. No modifica BattleScene ni conecta por HTTP.

## Controles

- Visor: rueda para zoom, botón central para desplazar, F para encuadrar, Tab para vista inclinada/cenital y clic para consultar edificios.
- Recorrido: WASD para caminar, Shift para correr, ratón para mirar, rueda para distancia y Escape para volver al visor.
- Se puede entrar y salir repetidamente. No reconstruir la geometría mientras el héroe está dentro.

## Escala de combate provisional

El héroe permanece a escala 1. Se midió `Prefabs/esqueletos/ModularHeroVisual` con la animación `A_Idle_Standing_Masc` en 0.5 s: ancho corporal aproximado **0.772 m**, altura **1.870 m**. Diez cuerpos juntos ocupan aproximadamente **7.72 m**; no se usa el ancho de brazos extendidos de la pose de reposo del asset.

`unitsPerLocalUnit = 3.5`: una calle de 3 unidades locales pasa a **10.5 m**, dejando unos 2.78 m adicionales sobre esos diez cuerpos. Comprobado en ejecución: el menor lado de los rectángulos de calle mide al menos 10.5 m. Es una comprobación geométrica, no una prueba de diez combatientes con animaciones y armas.

`Prefabs/Map/City/House_Small_01` mide aproximadamente 8.12 × 7.31 × 9.20 m en bounds de renderers. Se toma su **altura** como referencia: `buildingHeight = 8 m`; centro urbano 16 m; granjas 0.5 m; edificios no activos multiplican su altura por 0.35. Son volúmenes ilustrativos, no alturas enviadas por BronzeAge.

Cada cubo ocupa el **99 %** del ancho y del fondo de su huella (`buildingFootprintFill = 0.99`). Su centro y la huella autoritativa no cambian; el margen visual resultante permite distinguir edificios contiguos. Calles, caminos, suelo y escala general no reciben esta reducción.

Conversión: X = x × 3.5, Z = -y × 3.5. Las huellas vienen por esquina, ya rotadas y en unidades locales; se suma media dimensión para centrar, sin aplicar otra rotación ni multiplicar otra vez por tamaño de celda. Se amplía **todo el plano**, incluidas parcelas: una huella de vivienda de 6 unidades pasa a 21 m. Esto conserva distribución y separación, pero los cubos siguen representando parcelas completas, no fachadas finales. El factor se puede ajustar en el Inspector y todavía no forma parte del contrato táctico compartido ni cambia el JSON del servidor.

## Implementación y límites

La geometría se genera en Play con cubos, un material URP Lit y colores mediante MaterialPropertyBlock. Suelo, edificios, muros y torres tienen colisión; calles y caminos son superficies decorativas. Las puertas se representan como marcadores bajos transitables para que el recorrido pueda atravesarlas. Cuatro límites físicos invisibles evitan salir del suelo finito del visor.

El modo a pie reutiliza HeroEntity_Pure, VisualPrefabRegistry, ModularHeroVisual, EntityVisualSync/CharacterController y Main Camera Follow Hero. SettlementHeroWorld hornea la referencia al héroe y sus configuraciones; SettlementHeroLifecycleSystem crea y elimina únicamente el héroe de este recorrido. Una referencia de prefab específica evita activar el spawn de batallas. Se conserva la apariencia de PlayerSessionService si ya hay héroe seleccionado; sin sesión se utiliza la apariencia base. No se inventa ni persiste un héroe del backend.

No se generan tropas ni NavMeshAgents. Un filtro del héroe de recorrido bloquea ataque, habilidades e interacción; los sistemas existentes siguen dando locomoción y stamina. No es todavía una partida, simulación de combate ni prueba multijugador.

El fixture original no contiene murallas; las exportaciones del Lab sí pueden incluir `murallas[].muro`, `puertas` y `torres`, que ahora se validan, entran en los bounds y se representan. `planificado`, visualSeed, layoutVersion y schemaVersion siguen sin inventarse. Las actualizaciones de red y la conversión compartida de coordenadas quedan pendientes.

## Verificación en Unity 6000.5.8f1 (actualizada 2026-09-13)

- Recompilación oficial sin errores. El validador de Unity acepta los dos JSON locales: 2 asentamientos, 160 huellas y 66 piezas de muralla; también rechaza campos ausentes y dimensiones cero.
- `settlement2.json` en Play genera 91 edificios, 104 calles, 52 caminos, 55 muros, 2 puertas y 9 torres. El ID se resolvió automáticamente con `settlementId` vacío.
- La exportación más reciente conservada en BronzeAge (124 edificios locales, 138 calles, 58 caminos, 102 muros, 9 puertas y 17 torres) pasó la misma validación estructural de IDs, huellas y rectángulos.
- Prueba de recorrido `Conquest.SettlementPreview.Editor.SettlementWalkTests.Run`, con Play activo sobre `settlement2.json`: WASD avanzó 3.62 m, suelo estable, volumen sólido bloqueó en Z = 41.91; habilidades filtradas; cero NavMeshAgents; reentrada y limpieza de héroe/visual/cámara correctas. Usa un teclado temporal, aísla los teclados físicos y restaura todos los dispositivos al terminar.
- Inspección visual de la calle a escala nueva: `Assets/Screenshots/SettlementWalkScale.png`. Las capturas anteriores del visor corresponden a la escala inicial.
- Inspección cenital del Lab: `Assets/Screenshots/SettlementPreviewLab.png`.
- SHA256 del fixture coincide con el original: BCAB831253071B47854A32CD7BC2339575046DB2AB9F715AD3E38B85B2A2CCC3.
- No probado en ejecutable, con tropas o por red. No se añaden escenas a Build Settings.

Origen y contrato de los fixtures: `BronzeAgeFase0/Docs/Coordinacion/fixtures/README.md` y los README de cada subcarpeta. Las copias locales permiten ejecutar Unity sin depender del otro repositorio.
