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
- **Tanda 3 (rutas, trueques y Región): hecha y a revisar, sin commit.**
  - Está en el doc 33 y en `PantallasDeMundo.Rutas.cs`.
  - La prueba es `TuComercioADistancia`.
  - La página de capturas: https://claude.ai/artifact/Szy4oGJLYBEnYdJMQnPK6F
  - La Taberna sale en el riel solo con el informe de la región: el resto llega en su paso. Fue la opción a) propuesta a Víctor.
- Con la tanda 3 se cierra el paso 10. Sigue el paso 11: los ejércitos, con el carro 3D detrás del ejército y adjuntar, soltar, cargar,
  entregar y aparcar caravanas.
- El plan del paso 10 no entra en el commit del paso 9.

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
- **Pruebas**:
  - las nuevas de cada tanda, con ratón y teclado, a los cuatro tamaños y con capturas (`MUNDO_CAPTURAS` con una carpeta) junto al
    prototipo;
  - al acabar cada tanda, la suite entera sin capturas, en dos lotes a la vez (`bash tasks/pruebas/correr_en_paralelo.sh`) o en serie
    (`bash tasks/pruebas/correr.sh "ConquestTactics.Mundo.UI.Tests" ""`), y la batalla
    (`bash tasks/pruebas/correr_batalla.sh "ConquestTactics.Mundo.UI.Tests.PantallasDeMundoTests.DelMapaALaBatalla" ""`);
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
