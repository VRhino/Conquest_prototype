# Traspaso a una sesión nueva (bloque 4, paso 8)

2026-11-15. Lo que la sesión nueva necesita para seguir sin preguntar. Léelo entero antes de tocar nada.

## Dónde está el trabajo

- **Commit hecho**: `df41998a` en main local (sin push), con los pasos 6 y 7 enteros.
- **Paso 8 (Facción, tecnología, sigilo y estandartes)**: el plan y las respuestas de Víctor están en `tasks/bloque4-paso8-plan.md`, y lo hecho
  en `Docs/Arquitectura/31_Faccion_Y_Sigilo_2026-11-15.md`. La fila del paso 8 de la Hoja (`Docs/Hoja_De_Ruta.md`) está al día.
  - **Tanda 1, aprobada por Víctor y sin commit**:
    - el sigilo del prototipo como textura (`Sigilos.cs`, `TexturaDeSigilo.cs`);
    - dónde sale: el blasón de la plaza, el selector, los carteles del mapa y el color de Facción;
    - la ventana de Facción sin Facción: elegir, crear con el selector de sigilo y pedir ingreso.
  - **Tanda 2, hecha y a revisar, sin commit**:
    - tu Facción: el lateral y las pestañas Miembros, Plazas y Ajustes del Rey;
    - los cargos de Facción en la Base;
    - el Fondo de refundación como subpestaña de la taberna del campamento.
  - **Tanda 3, lo acordado**:
    - la pestaña Tecnología: el árbol por carriles y Eras sobre el tablero del árbol de tropas, con «Adoptar» (el Rey, en la capital) y los
      logros del mundo;
    - en el árbol de tropas, el estado «adoptable» con su etiqueta, que lleva a la pestaña Tecnología en esa carta;
    - quitar el «Sin Facción ni tecnologías» del árbol del campamento.
  - **Tanda 4, lo acordado**:
    - la pestaña Diplomacia entera: la lista y la ficha, alianza, vasallaje con tributo, guerra, paz, romper, rebelión, anexión, fusión, la
      Liga y los títulos;
    - los estandartes en la batalla:
      - el de Synty sobre el héroe, con la tela del sigilo (en una copia del material, con `MaterialPropertyBlock`); los bandidos, con el
        sigilo neutro;
      - **un estandarte por escuadra**;
      - **las banderas de las zonas de captura** con el sigilo del bando que tiene la zona;
      - **ver el estandarte de las tropas es una opción de los ajustes del juego**, encendida por defecto.
    - **Lo pide al motor otro hilo**: el `Sigilo` en `BattleSide` y en `BattleParticipantSnapshot` del ticket de batalla. Sin él no hay sigilo
      en la batalla: no lo hagas tú en el motor.
- **Pendiente de la tanda 2**:
  - **Los 5 Twemoji** del lateral (🏆 ⚖️ 🤝 🏛 🗿) ya están bajados y puestos.
  - **«Ver en el mapa»** de la pestaña Plazas no sale: la cámara del mapa de Conquest sigue siempre al héroe. Se puede añadir si Víctor lo pide.

## Reglas de trabajo

- **Servidor**: uno de prueba propio en el puerto **7891**, con el motor de **main** compilado en un clon aparte (`../BronzeAgeMotorPruebas`,
  `tasks/pruebas/preparar_motor.sh`).
  - Nunca el servidor de Víctor (7878), ni su cuenta, ni su checkout del motor (`../BronzeAgeFase0`, solo lectura).
  - El mundo del servidor de prueba es una copia en `../BronzeAgeMotorPruebas/mundo-pruebas/` (el `mundo-*.bin` y su `relieve-*.bin`, fuera
    de git por `.git/info/exclude`), no la carpeta de Víctor (`BronzeAgeFase0/generador/salida/mundo`), que él regenera cuando quiere.
    `correr.sh` y `correr_batalla.sh` lo leen de ahí. Para cambiar de mundo, copia los dos ficheros nuevos a esa carpeta.
  - La administración, solo con la cuenta admin de tu servidor.
  - Apágalo al acabar (`tasks/pruebas/parar.sh`).
- **Vía Rápida** en toda prueba con plaza: `FundarPlaza` llama a `ConViaRapida`, y `ConViaRapidaHasta` la reactiva en las esperas largas.
- **Calcar el prototipo** (`../BronzeAgeClient`) panel por panel, sin rediseñar. Los botones que no se pueden usar no salen, y sin mensajes de
  imposibilidad.
- **Scroll**: ninguno si cabe; si no, la barra estrecha común.
- **Iconos**: los del prototipo. Cada Twemoji nuevo necesita el permiso escrito de Víctor. Si falta alguno, pon el más parecido que haya y
  apúntalo.
- **Pruebas**:
  - con ratón y teclado de verdad, a los cuatro tamaños (1280x720, 1600x900, 1920x1080, 1950x1130) y con capturas junto al prototipo;
  - al acabar cada tanda, la suite entera del mundo (`bash tasks/pruebas/correr.sh "ConquestTactics.Mundo.UI.Tests" ""`) y la batalla
    de punta a punta (`bash tasks/pruebas/correr_batalla.sh "ConquestTactics.Mundo.UI.Tests.PantallasDeMundoTests.DelMapaALaBatalla" ""`);
  - si el editor de Víctor está abierto, nada de compilar en lote y no lo cierres.
- **Capturas del prototipo**:
  - Vite contra tu servidor: en `../BronzeAgeClient`, `VITE_API_BASE=http://127.0.0.1:7891 npx vite --port 5199 --strictPort`.
  - Chrome sin pantalla por CDP: los ejemplos están en `tasks/pruebas/cdp/`.
  - Las cuentas de las pruebas usan la clave `clave-de-prueba-1`, y sus nicks están en `Temp/pruebas-mundo/datos/acceso/cuentas.jsonl`.
- **El análisis de cada paso** (`tasks/bloque4-pasoN-plan.md`) va solo en texto: el plan, «Lo que voy a hacer» y las dudas reales. Sin
  capturas del «antes» ni página (Víctor, 2026-11-15: ya ve el antes y el después al acabar cada tanda).
- **Al acabar cada tanda**: para, avisa con la página de capturas junto al prototipo y espera a Víctor.
- **Commits**:
  - Solo cuando Víctor lo diga: en main local, sin push, sin stash, reset, amend ni force.
  - Nunca incluyas sus ficheros sin commit: los prefabs de caballería y `HorseArchers.prefab`, `CLAUDE.md`, `ProjectSettings/*`,
    `UserSettings/*`, `Anton SDF.asset`, `addressables_content_state.bin`, `link.xml`, `URP-*-Renderer.asset` y `SceneDependencyCache/*`.
  - Antes de cada commit, mira el índice con `git status`.
- **Logs temporales**: con el prefijo `[BattleTestDebug]`, y se quitan después.
- **Materiales**: solo en copias.
- **Ningún identificador de modelo** en commits, código ni docs.

## Ficheros

- **Planes**: `tasks/bloque4-paso8-plan.md` (paso 8) y `tasks/bloque4-paso7-plan.md` (paso 7).
- **Docs**:
  - `Docs/Arquitectura/31_Faccion_Y_Sigilo_2026-11-15.md` (paso 8);
  - `Docs/Arquitectura/30_Plaza_Por_Dentro_2026-11-15.md` (paso 7, con la regla de la Vía Rápida);
  - `Docs/Hoja_De_Ruta.md` (bloque 4);
  - `Docs/Inventario_BronzeAgeClient.md` (§3.11 tecnología, §3.12 Facción).
- **Código del paso 8**:
  - `Assets/Scripts/Mundo/Sigilos.cs` y `Assets/Scripts/UI/TexturaDeSigilo.cs`;
  - `Assets/Scripts/Mundo/UI/PantallasDeMundo.Faccion.cs` (sin Facción), `.FaccionPropia.cs` (tu Facción) y `.Fondo.cs` (el fondo);
  - `Assets/Resources/MundoUI/Faccion.uss` y `Assets/Resources/MundoUI/Sigilo/` (el catálogo, los emblemas, las insignias y los créditos).
- **Pruebas del paso 8**:
  - `Assets/Tests/PlayMode/Mundo/SigilosTests.cs`;
  - `PantallasDeMundoTests.Faccion.cs` (`TuFaccionSinFaccion`, `ElSelectorConElSigilo`);
  - `PantallasDeMundoTests.FaccionPropia.cs` (`TuFaccionDelRey`).

## Reglas «allow» para `.claude/settings.local.json` (las copia Víctor)

Cada orden que esta sesión vio bloqueada o usa a menudo, lo más estrecha posible. Sin push ni nada destructivo. Van dentro de
`permissions.allow`; `git add` y `git commit` ya están.

```json
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
"Bash(git clone https://github.com/VRhino/BronzeAge.git ../BronzeAgeMotorPruebas)",
"Bash(cargo build --release -p servicio -p lanzador --manifest-path ../BronzeAgeMotorPruebas/Cargo.toml --target-dir ../BronzeAgeMotorPruebas/target)",
"Bash(node tasks/pruebas/cdp/:*)",
"Bash(npx vite --port 5199 --strictPort)"
```

Notas:
- **Vite**: la última regla se lanza desde `../BronzeAgeClient` con `VITE_API_BASE` en el entorno. Si el permiso pide la orden entera, usa
  `Bash(VITE_API_BASE=http://127.0.0.1:7891 npx vite --port 5199 --strictPort)`.
- **Lo que no entra, a propósito**: `git push`, `git reset`, `git stash`, `git checkout` en Conquest, borrar fuera de `Temp/`, y cualquier
  orden contra el 7878 o contra `../BronzeAgeFase0`.
