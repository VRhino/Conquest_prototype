# Traspaso a una sesión nueva (bloque 4, paso 10)

2026-11-16. Lo que la sesión nueva necesita para seguir sin preguntar. Léelo entero antes de tocar nada.

## La rama `migracion`

- Todo el trabajo de la migración, hasta el paso 13, va en la rama local **`migracion`**, creada desde main (`b37be317`). Main no se toca
  hasta que Víctor junte la rama. **El push lo hace Víctor.**
- **El paso 9 ya está en commit**: `66c4c77c`, con 65 ficheros (las tandas 1, 2 y 3, los 4 Twemoji, el doc 32, la Hoja, el plan y el
  arreglo del aviso de HeroInputSystem).
- El modo automático solo acepta un commit o una rama si Víctor lo escribe directamente en el hilo. Si se bloquea, dile la orden exacta y el
  mensaje del bloqueo, sin buscar otra vía.
- **Cada commit**: solo cuando Víctor lo diga, en `migracion`. Escribe el mensaje en un fichero de `tasks/` y lánzalo con una orden suelta
  `git commit -F <fichero>`, sin `cd`, `-C` ni tuberías delante. Repasa el índice con `git status` antes. Sin push, stash, reset, amend ni
  force.
- **Nunca entran en un commit** los ficheros de Víctor: los prefabs de caballería (`HorseArchers`, `LanceCavalry`, `SwordCavalry`),
  `CLAUDE.md`, `ProjectSettings/*`, `UserSettings/*`, `Anton SDF.asset`, `addressables_content_state.bin`, `link.xml`,
  `URP-*-Renderer.asset`, `SceneDependencyCache/*` y `.claude/settings.local.json`. Tampoco `Tools/IconosDelPrototipo/.paquete.mjs` ni
  `tasks/paso6-tanda3/`.

## Paso 10: el mercado y las caravanas

- El plan está en `tasks/bloque4-paso10-plan.md`, con las respuestas de Víctor:
  - **Región** (comprar el informe de la región y tomar una orden a distancia) va en la tanda 3.
  - **Ninguna caravana en la columna del héroe**: una columna personal no puede llevarlas (`columna.personal_sin_caravanas` en el motor).
    Solo se enseña lo que el jugador puede hacer.
  - **El carro 3D con su animal** sustituye a la caja de las caravanas que viajan solas por el mundo 3D, con su movimiento y su animación.
    Es una pieza reutilizable: en el paso 11 sigue al ejército que lleve caravanas. `aparcarCaravana`, `adjuntar`, `soltar`, `cargar` y
    `entregar` van con los ejércitos, en el paso 11.
- **Tanda 1 (las órdenes de mercado): hecha y a revisar, sin commit.**
  - Lo hecho está en `Docs/Arquitectura/33_Mercado_Y_Caravanas_2026-11-16.md`, y el código en `PantallasDeMundo.Mercado.cs`.
  - La prueba es `TuMercado`.
  - La página de capturas: https://claude.ai/artifact/FYPj5TcivSFcuMqcxNummR
  - En commit: `122eb377` en `migracion`, con la mejora de las pruebas en `30f8d971`.
- **Tanda 2 (caravanas, escolta y carro 3D): hecha y a revisar, sin commit.**
  - Está en el doc 33, `PantallasDeMundo.Flota.cs`, `Mapa/CarroDeCaravana.cs` y `Editor/CarroDeCaravanaSetup.cs`, que arma el prefab con
    `-executeMethod CarroDeCaravanaSetup.Construir`.
  - La prueba es `TuFlota`.
  - El animal del carro es el caballo para todos: es duda planteada a Víctor.
  - En commit: `a8832b0d`.
- **Tanda 3 (rutas, trueques y Región): en commit `1f6dc42a`.**
  - Está en el doc 33 y en `PantallasDeMundo.Rutas.cs`.
  - La prueba es `TuComercioADistancia`.
  - La página de capturas: https://claude.ai/artifact/Szy4oGJLYBEnYdJMQnPK6F
  - La Taberna sale en el riel solo con el informe de la región: el resto llega en el paso 12. Víctor eligió la a) el 2026-11-16.
- **Paso 11 (los ejércitos)**: el plan está en `tasks/bloque4-paso11-plan.md`.
  - **Tanda 1 (salir como ejército): en commit `8a2bd3ea`.** Está en el doc 34 y en `PantallasDeMundo.Convocatoria.cs`; la prueba es
    `TuEjercitoEnPreparacion`.
  - **Tanda 2 (el ejército en el mapa, unirse y la batalla): en commit `9b7ee80b`.** Está en el doc 34, en
    `PantallasDeMundo.Ejercito.cs` (la ventana ⚑ y «Unirse a un ejército» del riel de la plaza) y en `CosasDelMapa.Acciones` («Unirme» y
    «Unirse» en las fichas). Las pruebas son `TuEjercitoEnElMapa` y `TuUnionALaBatalla` (esta, en la vuelta de la batalla); con `MUNDO_PROTO=ejercito` solo deja la escena para
    `tasks/pruebas/cdp/ejercito-mapa.mjs`.
  - **Tanda 2: en commit `9b7ee80b`.**
  - **Añadido (las tarjetas del ejército en el lateral de la plaza): en commit `3e4ceba4`.** Está en el doc 34 y en
    `PantallasDeMundo.Ejercito.cs` (`PintarEjercitosDePlaza`); la prueba es `TuEjercitoEnElMapa`.
  - **Tanda 3 (las caravanas del ejército): en commit `0004b7f4`.** Está en el doc 34, en `PantallasDeMundo.Convocatoria.cs` (las
    comerciales en la preparación), `PantallasDeMundo.Ejercito.cs` (cargar, entregar y aparcar) y `CosasDelMapa` (el carro detrás y «Entrar con
    el ejército»). La prueba es `TuEjercitoConCaravanas`; con `MUNDO_PROTO=caravanas` deja la escena para
    `tasks/pruebas/cdp/ejercito-caravanas.mjs`.
  - Con la tanda 3 se cerró el paso 11.
- **Paso 12 (mercenarios por contrato, taberna y misiones)**: el plan, con las respuestas de Víctor (1a 2b 3a 4a 5b 6 y 7a), está en
  `tasks/bloque4-paso12-plan.md`; el doc es el 35.
  - **Tanda 1 (la Taberna): en commit `eced9885`.** Está en `PantallasDeMundo.Taberna.cs`; la prueba es `TuTaberna` (con
    `MUNDO_PROTO=taberna`, la escena para `tasks/pruebas/cdp/taberna.mjs`).
  - **Tanda 2 (los contratos): en commit `051e82b5`.** Está en `PantallasDeMundo.Mercenarios.cs` (el tablón, Facción › Mercenarios y
    `ContratosPorValorar`) y en `Avisos`; la prueba es `TuTablonDeMercenarios` (con `MUNDO_PROTO=mercenarios`, la escena para
    `tasks/pruebas/cdp/mercenarios.mjs`). Los 9 Twemoji nuevos, con permiso de Víctor.
  - **Tanda 3 (las misiones): en commit `16c97fc4`.** Está en `PantallasDeMundo.Misiones.cs` (el botón, el diario, el rastreador, los
    tablones y Facción › Misiones); la prueba es `TusMisiones` (con `MUNDO_PROTO=misiones`, la escena para `tasks/pruebas/cdp/misiones.mjs`).
    Los 5 Twemoji nuevos, con permiso de Víctor. Con ella se cierra el paso 12.
- **Paso 13 (Aedas, épicas y crónica)**: el plan, con las respuestas de Víctor (1a 2a 3a), está en `tasks/bloque4-paso13-plan.md`; el doc es el 36.
  - **Tanda 1 (los Aedas): en commit `615e23d4`.** Está en `PantallasDeMundo.Aedas.cs`, `CosasDelMapa` (el nombre y el parado junto a
    su plaza) y `Avisos`; la prueba es `TusAedas` (con `MUNDO_PROTO=aedas`, la escena para `tasks/pruebas/cdp/aedas.mjs`).
  - **Tanda 2 (la crónica): en commit `37eb571b`.** **El bloque 4 está cerrado y en `main`** (2026-11-16: `migracion` juntada con `main` por avance rápido, sin push; el push es de Víctor). Está en `PantallasDeMundo.Cronica.cs`; la prueba es `LaCronica` (con
    `MUNDO_PROTO=cronica`, la escena para `tasks/pruebas/cdp/cronica.mjs`). Con ella se cierra el paso 13 y el bloque 4; lo que queda fuera de los
    pasos y lo que no tiene prueba está en la Hoja, al final de la tabla del bloque 4.
  - Los servidores de prueba guardan ya sus datos en `Logs/pruebas-mundo` y `Logs/pruebas-batalla`, no en `Temp/` (Unity la vacía).
  - Las tres dudas, contestadas por Víctor el 2026-11-16: el caballo para todos los carros hasta tener los modelos (el animal real, en el
    icono de la ventana); la Taberna solo con el informe (el resto, en el paso 12); las caravanas comerciales se enganchan en la preparación,
    junto a la de fundación, solo si hay alguna libre en la plaza (tanda 3; ver el plan).
- Con la tanda 3 se cerró el paso 10. Sigue el paso 11: los ejércitos, con el carro 3D detrás del ejército y adjuntar, soltar, cargar,
  entregar y aparcar caravanas.
- El plan del paso 10 no entra en el commit del paso 9.

## Sueltos de la batalla 12, 13 y 16 (2026-11-16, en commit `2c1046dc` en `main`)

- El análisis y las respuestas de Víctor están en `tasks/batalla-sueltos-plan.md`. El 14 (el Transport de Unity en Windows) va en otra sesión, con
  el encargo en `tasks/brief-transport-windows.md`; el 15 (probarla con alguien al teclado) lo hace Víctor.
- **12, los arqueros:** `ReservaDeProyectiles` pasó a `Combat/Projectiles/RangedCombatBootstrap.cs` y `ProjectileSpawnSystem` la crea con el
  primer disparo. Pruebas: `ProyectilesSinReservaTests` y `BattleSceneIntegrationTests.ProductionBattleSceneFootArchersShootProjectiles`.
- **13, cambiar de escuadra en red y apuntar con la cámara:**
  - el comando lleva `CambiarEscuadra`, `EscuadraPedida` y `Mirar`. El dato de cada `InputEvent` (la escuadra, la orden y su punto, la
    ranura, la formación) va siempre con su último valor: el aviso llega en su tick, pero los demás campos son los del último comando de ese
    tick (antes la Q llegaba como golpe básico y «mantener» como «seguir»);
  - el servidor (`MandoSystem`) pone `SquadSwapRequest` si el héroe está en un punto de suministro, y manda al dueño la activa, la
    canalización, la espera y la reserva (`ReservaRed`);
  - el cliente (`EspejoClienteSystem.HeroeDelPanel`) da al panel de suministro de siempre una entidad suya
    (`SupplyPointUIController.NetworkHero`), y avisa al HUD del cambio;
  - **el servidor de batalla es un ejecutable:** tras tocar código del servidor hay que reconstruirlo con
    `-executeMethod ConquestTactics.Net.Editor.PruebaRedEditor.Construir`.
  - La prueba es `DelMapaALaBatalla`: con el ticket de dos escuadras elegidas, anda con WASD al punto de suministro, gira la cámara, F, elige
    con el ratón y cambia; luego la Q, «mantener» (X) y «seguir» (C) en red.
- **16:** borrados `HeroLevelSystem`, `SquadProgressionSystem`, `XPEventComponent`, `XPSource` y sus pruebas.
- **Desde dentro de un sitio**, el clic en el mapa no hace nada (`MapaDeMundo.MarcharA`).
- Capturas en `tasks/sueltos-batalla/capturas/`.

## El motor ecfb23d y c94a960 (2026-11-16): tanda 1 en `e82a40a3`, tanda 2 en `92d29616`, tanda 3 hecha y sin commit

- El análisis, las tandas y las respuestas de Víctor están en `tasks/motor-ecfb23d-plan.md`.
- El motor de pruebas (`../BronzeAgeMotorPruebas`) está en `ecfb23d`, y el contrato regenerado en `Assets/Plugins/BronzeAge/BronzeAge.Contrato.dll`
  con `Tools/ContratoBronzeAge/actualizar.ps1 -Proto ../BronzeAgeMotorPruebas/proto`.
- **Tanda 1:**
  - **el id de tropa:** fuera `IdDeTropa`, porque el motor ya manda ids en todas las proyecciones;
  - **«Bajarse de la batalla» en la preparación:** para quien se unió, y para quien la inició si hay otro humano; el único humano solo
    «Cancelar» (Víctor, 2a). Al aceptarse, la vigilancia vuelve al mapa sola. Prueba: `TuUnionALaBatalla`;
  - **la preparación lee también las incorporaciones**, para las escuadras y la tira de héroes de quien se une;
  - **la salida propia en el informe:** quien abandona espera el resultado, con «Derrota · tu bando ganó/perdió» y la línea de cómo salió.
    Prueba: `DelMapaALaBatalla`;
  - **el informe ya no rehace «Salir» al repintar:** perdía el clic (el fallo de la prueba que salía a veces);
  - **los mercenarios de la región** en `TuTaberna` (`dar_fondo_monedas` y `dar_monedas`) y **los Aedas** en `TusAedas` (`colocar_aeda`,
    itinerante y residente). La compra al Aeda sigue sin prueba (Víctor, 3a).
- **Tanda 2 (hecha y sin commit): el informe con el hecho de cierre.**
  - `PantallasDeMundo.Informe.cs`: `CierreDe` lee `heroe.cierre_batalla` de `Almacen.Hechos`, y `LoDelCierre` pinta los recuadros (botín, experiencia,
    carro perdido, lo que no cupo y la herida).
  - «Exp.» por escuadra en «Tus escuadras». Fuera la comparación de antes y después (`Antes`), así que funciona igual desde el aviso.
  - Quien sale antes del final no tiene hecho de cierre: sus escuadras salen del ticket y de la escuadra de ahora, y la herida del héroe. Está
    apuntado en la Hoja para pedirlo al motor.
  - `DelMapaALaBatalla` también elige el punto de «mantener» lejos de por donde vuelve el héroe: antes fallaba a veces.
  - El motor de pruebas está en `c94a960`, con el contrato regenerado.
- **Tanda 3 (hecha y sin commit): elegir escuadras al defender desde tu base.**
  - `PantallasDeMundo.Defensa.cs`: el aviso «Asedio a tu plaza · Defender» (`AvisoDeAsedio`, encima de la vista de la plaza) y el panel
    `VentanaDeDefensa`, con el riel de la salida: elegidas con ✕, el «+» solo si algo cabe, la milicia que no cabe en «Se quedan en el campamento»
    y el Liderazgo con lo que ya llevas fuera. Unirse manda `escuadron_ids`; ya dentro, «Elegir escuadras» en la preparación manda
    `OrdenElegirEscuadrasDeBatalla`.
  - Desde la plaza, unido a la batalla, va a la preparación (antes solo lo hacía el mapa). El orden de la preparación sigue a las elegidas.
  - En Unity nadie entra de oficio en un asedio, tampoco el residente: se une con «Defender».
  - El ticket con la revisión subida no pierde incorporaciones: el motor las reenvía todas con el ticket nuevo.
  - Prueba: `TuDefensaDeTuPlaza` (con lanzador), el ejemplo de Víctor: 14 de Liderazgo, granjeros y mineros dentro, la milicia fuera y sin salir
    en la batalla. La plaza recién fundada está protegida un día de mundo: la prueba adelanta el reloj hasta `protegida_hasta_ms`.
- **La interfaz de la batalla (hecha y sin commit; `tasks/interfaz-batalla-plan.md`):**
  - el aviso de conexión, una banda UGUI (`AvisoDeRed`) con la cuenta atrás de la reconexión (`QuedaParaPerderla`); prueba `AvisoDeRedTests`;
  - abandonar, en las Opciones (Esc en la batalla, `EscEnLaBatalla`), con su confirmación; fuera F10 y el botón de la capa de batalla;
  - el panel de suministro con los huecos apagados y sin «+» (`TroopsViewerController.HuecosApagados`), «Aceptar» solo con una elegida y sin aviso de F si no hay qué
    sacar. `DelMapaALaBatalla` lo comprueba y abandona por las Opciones.
- **Las clases y los objetos del motor** (`tasks/clases-objetos-motor-plan.md`, Víctor: la a en todas):
  - **tanda 1, en commit `8ee2e39f`:**
    - los catálogos del héroe, importados tal cual (`Editor/HeroeDelMotor.cs`, `Resources/Heroe/BronzeAge`) y leídos por
      `Net/CatalogoDelHeroe.cs`;
    - otra versión de los catálogos, o una clase u objeto desconocidos, rechazan la batalla con su motivo. Quien se une después con algo
      desconocido no sale al campo;
    - los carros de guerra, con el aspecto de la caballería de lanza (37 de 37 tropas);
    - fuera `terrain-tools`;
  - **tanda 2, en su commit (`Clases y objetos del motor, tanda 2`):**
    - el héroe del ticket, con sus cifras del motor (`Net/CifrasDelHeroe.cs`, `HeroEngineStatsComponent`);
    - las reglas del héroe del autor del motor, en `HeroEngineRules` dentro de `HeroGameplayConfig`;
    - su montura como caballo (`HeroHorseStatsComponent`);
    - el capitán, con las cifras de un héroe de espada y escudo de inicio;
    - un héroe del ticket sin montura va a pie (22a, `HeroOnFootTag`);
  - sigue la tanda 3 (23a y 24a): ver el final de `tasks/clases-objetos-motor-plan.md`.
- **Sigue, por el orden de Víctor:** las clases y los objetos del motor, los carros de guerra y rechazar lo desconocido; al final, la
  predicción con choque contra los soldados (6c). Sin capturas en esta serie (Víctor, 2026-11-16).

## Las pruebas más rápidas (2026-11-16, hecho y sin commit)

| | Antes | Después |
|---|---|---|
| Suite entera del mundo (28 pruebas), en serie | 1.875 s (31 min) | 506 s (8,4 min) |
| La suite en dos lotes a la vez (`correr_en_paralelo.sh`) | — | 259 s (4,3 min) |
| Fundar una plaza (`FundarPlaza`) | 141 s | unos 13 s |
| `PorDentroDelPuebloYDeLaAvanzada` | 371 s | 27 s |
| `TuTerritorio` | 270 s | 32-74 s |
| `TuPlazaPorDentro` | 257 s | 60-92 s |
| `TuAvanzadaYSuObra` | 244 s | 30 s |
| `TuTecnologiaDelRey` | 233 s | 55 s |
| `TuMapaEstrategico` | 179 s | 38 s |
| `TuPlazaAjenaDeVisita` | 171 s | 37 s |
| La batalla de punta a punta | 100 s | 93 s |

**Las mejoras**:
- **El reloj del motor**: en `FundarPlaza`, saltos con `AvanzarReloj` (`avanzar_hasta`), de un minuto en la marcha y de diez en las obras cortas y
  en la muralla. En las esperas de las pruebas, `EsperarConReloj(url, s, cond, seg, que, pasoMs)` sube el factor del reloj mientras espera y lo
  deja como estaba; no son saltos, porque la sesión va por detrás de ellos hasta su ping. `ConViaRapidaHasta` también salta.
- **Esperas a condiciones**: las dos esperas fijas de 15 s de `FundarPlaza` esperan a que lo corto esté en pie y lo largo en la plaza.
- **Las causas de raíz que salieron al acelerar**:
  - la muralla se paraba con la madera a 20: ahora la prueba da madera y piedra antes de empezarla;
  - la auto-construcción llenaba la cola antes de pausarla: ahora se pausa justo al fundar, y la cola se vacía hasta que queda vacía.
- **Capturas**: los cuatro tamaños con capturas solo si hay `MUNDO_CAPTURAS`, que son las pruebas nuevas de una tanda. La regresión va sin
  capturas y a 1280x720.
- **Dos lotes a la vez**:
  - `tasks/pruebas/clon_lote2.sh` monta `../Conquest_prototype_lote2`. Assets y Packages van enlazados (junction), Library va copiada (11
    GB; en cada vuelta solo copia lo cambiado) y ProjectSettings va copiado con otro productName, para que sus PlayerPrefs no pisen las del
    principal. Lo marca un fichero `.clon`.
  - Para quitarlo, `clon_lote2.sh quitar`, que borra solo los enlaces.
  - `tasks/pruebas/correr_en_paralelo.sh` levanta los servidores 7891 y 7892 (con `mundo-pruebas-2`) y corre los dos Unity a la vez.
  - El reparto lo hace `Lote2` en `PantallasDeMundoTests.cs`, con `MUNDO_LOTE`. Una prueba nueva va al lote 1, salvo que la apuntes en
    `Lote2`.
  - Nunca con el editor de Víctor abierto.
- **Esperas fijas**: las 45 del cuerpo de las pruebas, todas menos las de la batalla, esperan ahora a que la pantalla deje de cambiar
  (`Quieto(max)`: 0,25 s sin cambios, sin pasar de lo que esperaban antes).
- **Sin hacer, a propósito**:
  - **La plaza compartida entre pruebas**: con la fundación en unos 13 s, ganaría unos 90 s en serie y menos en paralelo, con el riesgo de que
    una prueba cambie la plaza de otra. Está pendiente de que Víctor lo decida con estas cifras.

## Arreglado

- El aviso de `HeroInputSystem` («FullscreenPanelManager no está disponible») ya no sale: se escribía con su traza en cada fotograma (unas
  200.000 veces por suite, 3,5 GB de log). Está en el commit del paso 9.

## Reglas de trabajo

- **Servidor**: uno de prueba propio en el puerto **7891**, con el motor de **main** compilado en un clon aparte (`../BronzeAgeMotorPruebas`,
  `tasks/pruebas/preparar_motor.sh`).
  - Nunca el servidor de Víctor (7878), ni su cuenta, ni su checkout del motor (`../BronzeAgeFase0`, solo lectura).
  - El mundo del servidor de prueba es una copia en `../BronzeAgeMotorPruebas/mundo-pruebas/` (el `mundo-*.bin` y su `relieve-*.bin`). No
    es la carpeta de Víctor, que él regenera cuando quiere. `correr.sh` y `correr_batalla.sh` lo leen de ahí.
  - La administración, solo con la cuenta admin de tu servidor.
  - Apágalo al acabar con `tasks/pruebas/parar.sh`.
- **Vía Rápida** en toda prueba con plaza: `FundarPlaza` llama a `ConViaRapida`, y `ConViaRapidaHasta` la renueva en las esperas largas.
- **Calcar el prototipo** (`../BronzeAgeClient`) panel por panel, sin rediseñar. Los botones que no se pueden usar no salen, y no hay
  mensajes de imposibilidad.
- **Scroll**: ninguno si cabe; si no, la barra estrecha común.
- **Iconos**: los del prototipo. Cada Twemoji nuevo necesita el permiso escrito de Víctor, directo en el hilo. Si falta alguno, pon el más
  parecido que haya y apúntalo.
- **Cuándo pasar qué pruebas** (Víctor, 2026-11-16):
  - en un cambio pequeño, solo las pruebas de lo que toca (por ejemplo, las batallas si se toca la batalla);
  - la suite completa en dos lotes y las batallas, justo antes de cada commit. No hay ningún gancho de git que la lance: los ganchos de
    `.git/hooks` son los de Git LFS y el de graphify.
- **Pruebas**:
  - las nuevas de cada tanda, con ratón y teclado, a los cuatro tamaños y con capturas (`MUNDO_CAPTURAS` con una carpeta) junto al
    prototipo;
  - al acabar cada tanda, la suite entera sin capturas, en dos lotes a la vez (`bash tasks/pruebas/correr_en_paralelo.sh`) o en serie
    (`bash tasks/pruebas/correr.sh "ConquestTactics.Mundo.UI.Tests" ""`), y la batalla
    en dos vueltas de `correr_batalla.sh`, cada una con su servidor y su lanzador:
    `bash tasks/pruebas/correr_batalla.sh "ConquestTactics.Mundo.UI.Tests.PantallasDeMundoTests.DelMapaALaBatalla" ""` y
    `bash tasks/pruebas/correr_batalla.sh "ConquestTactics.Mundo.UI.Tests.PantallasDeMundoTests.TuUnionALaBatalla" ""`.
    `TuUnionALaBatalla` necesita las batallas en Unity; juntas no, porque el lanzador que deja la primera asigna la batalla de la segunda y
    deja de admitir a quien llega);
  - si el editor de Víctor está abierto, nada de compilar en lote y no lo cierres.
- **Capturas del prototipo**:
  - Vite contra tu servidor: en `../BronzeAgeClient`, `VITE_API_BASE=http://127.0.0.1:7891 npx vite --port 5199 --strictPort`.
  - Chrome sin pantalla por CDP: los ejemplos están en `tasks/pruebas/cdp/`.
  - Las cuentas de las pruebas usan la clave `clave-de-prueba-1`.
- **El análisis de cada paso** (`tasks/bloque4-pasoN-plan.md`) va solo en texto: el plan, «Lo que voy a hacer» y las dudas, solo si son
  reales. Sin capturas del «antes».
- **Al acabar cada tanda**: para, avisa con la página de capturas junto al prototipo y espera a Víctor.
- **Logs temporales**: con el prefijo `[BattleTestDebug]`, y se quitan después.
- **Materiales**: solo en copias.
- **Ningún identificador de modelo** en commits, código ni docs.
- **Avisos**: se informa solo por `send_message` al `@parent`. Las decisiones que trae el coordinador son de Víctor, pero los permisos del
  modo automático (commits, ramas, descargas) piden que lo escriba él.

## Ficheros

- **Planes**: `tasks/bloque4-paso10-plan.md` (paso 10) y `tasks/bloque4-paso9-plan.md` (paso 9).
- **Docs**: `Docs/Arquitectura/32_Mapa_Estrategico_Y_Territorio_2026-11-15.md` (paso 9), `Docs/Hoja_De_Ruta.md` (bloque 4) y
  `Docs/Inventario_BronzeAgeClient.md` (§3.14, mercado y caravanas).
- **Ayudas de las pruebas**:
  - `FundarPlaza`, `ConViaRapida` y `ConViaRapidaHasta` (`PantallasDeMundoTests.Plaza.cs`);
  - `AvanzarReloj`, `Reloj` y `OrdenAdmin` (`.Batalla.cs`);
  - `Esperar`, `EsperarConReloj`, `EnTamanos` y `Capturar` (`PantallasDeMundoTests.cs`).

## Reglas «allow» para `.claude/settings.local.json` (las copia Víctor)

Las mismas de antes, más una para la rama. Van dentro de `permissions.allow`; `git add` y `git commit` ya están.

```json
"Bash(git switch -c migracion)",
"Bash(node Tools/IconosDelPrototipo/generar.mjs:*)",
"Bash(bash tasks/pruebas/correr.sh:*)",
"Bash(bash tasks/pruebas/correr_batalla.sh:*)",
"Bash(bash tasks/pruebas/parar.sh)",
"Bash(bash tasks/pruebas/preparar_motor.sh)",
"Bash(\"/c/Program Files/Unity/Hub/Editor/6000.5.8f1/Editor/Unity.exe\" -batchmode -quit -projectPath . -logFile Logs/mundo/compilar.log)",
"Bash(\"/c/Program Files/Unity/Hub/Editor/6000.5.8f1/Editor/Unity.exe\" -batchmode -projectPath . -runTests:*)",
"Bash(git status:*)",
"Bash(git diff:*)",
"Bash(git log:*)",
"Bash(git -C ../BronzeAgeMotorPruebas fetch origin main)",
"Bash(git -C ../BronzeAgeMotorPruebas checkout --detach origin/main)",
"Bash(cargo build --release -p servicio -p lanzador --manifest-path ../BronzeAgeMotorPruebas/Cargo.toml --target-dir ../BronzeAgeMotorPruebas/target)",
"Bash(node tasks/pruebas/cdp/:*)",
"Bash(npx vite --port 5199 --strictPort)"
```

**Lo que no entra, a propósito**: `git push`, `git reset`, `git stash`, borrar fuera de `Temp/`, y cualquier orden contra el 7878 o contra
`../BronzeAgeFase0`.
