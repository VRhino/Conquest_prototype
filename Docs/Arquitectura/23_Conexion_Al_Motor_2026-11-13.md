# 23. Conquest como cliente del motor: la sesión del mundo (2026-11-13)

Paso 1 del bloque 4 de [`Hoja_De_Ruta.md`](../Hoja_De_Ruta.md). Conquest habla con el servidor del motor (`bronzeage-servidor`, repo
VRhino/BronzeAge) igual que BronzeAgeClient: el acceso por HTTP y la sesión del contrato 05 por WebSocket, en el mismo puerto. La
referencia que funciona es `../BronzeAgeClient/src/contrato/`; el contrato y su explicación, `proto/bronzeage/v1/` y
`Docs/Coordinacion/05_Contrato_de_Sesion.md` y `08_Consumo_del_Contrato.md` del motor.

## Dónde está

`Assets/Scripts/Mundo/`, asmdef propio `Conquest.Mundo`, separado de `Conquest.Net` (la batalla en red). Es C# puro
(`noEngineReferences`): se compila y se prueba sin escena, en EditMode o con `dotnet test`.

| Fichero | Qué |
|---|---|
| `Sesion.cs` | El WebSocket: `Hola` y `Bienvenida`, zstd, secuencia sin huecos, `Ordenar` con su `ResultadoOrden` causal, `PedirCronica`, `CambiarVista`, ping y reloj del mundo (`AhoraMundo`), reconexión reanudando por versión y «sesión reemplazada» |
| `Almacen.cs` | Lo que se guarda por ámbito (`HEROE:heroe-3`, `VISTA:f-1`...): foto, delta, revocado, la plaza con máscara de campos, la niebla parcial por tramos, los hechos y los nombres de héroe. Puerto de `crates/contrato/src/almacen.rs` del motor |
| `Instante.cs` | Lo que cambia con el reloj sin que llegue nada: la posición de quien sigue un plan y el valor de un ritmo |
| `Acceso.cs` | `POST /v1/registro`, `POST /v1/sesiones`, `DELETE /v1/sesiones/actual` y las descargas por ruta del servidor |
| `DatosDeJuego.cs` | `GET /v1/datos/<versión>`, con caché: las cifras de las reglas. Sin valores de reserva: si falta una, `FaltaDato` |
| `MundoEnDisco.cs` | El artefacto de mundo y el paquete de relieve por hash, con caché en disco (uno de cada; el relieve se comprueba con su sha256) |
| `Textos.cs` | Los códigos de rechazo y de acceso en frases (los de `ui/erroresServidor.ts`, más lo que falta del detalle en milésimas) y el nombre de un héroe |

No se portan los adaptadores al REST viejo (`motor/proyeccion.ts`, `motor/ordenes.ts`, `tiposDominio.ts`): las pantallas leen los tipos
del contrato del almacén y dan `Orden` directamente.

## Hilos

El WebSocket recibe en un hilo de fondo y solo encola las tramas. Todo lo demás pasa en `Sesion.Bombear()`, que llama el hilo principal
cada fotograma: descomprimir, aplicar al almacén, resolver órdenes, reconectar, el ping y los eventos (`AlCambiar`, `AlEvento`, `AlAviso`,
`AlEstado`, `AlBienvenida`). Así el almacén solo se toca desde un hilo y nada de Unity se toca fuera del principal. Las esperas de HTTP
llevan `ConfigureAwait(false)`; quien las espera desde Unity vuelve al hilo principal con su propio `await`.

## Decisiones

- **Las órdenes se dan con los tipos del contrato:** `sesion.Ordenar(new Orden { MarcharA = ... })`. La sesión pone `peticion_id`; la
  tarea acaba con el `ResultadoOrden`, y entonces el almacén ya tiene lo que cambió.
- **Una incoherencia no tumba el cliente:** una delta que no encaja tira ese ámbito y reconecta; el servidor lo manda otra vez como foto.
- **«Sesión reemplazada» cierra para siempre:** reconectar sola echaría a la otra sesión, y se echarían una a la otra en bucle. Se vuelve a
  entrar a petición del jugador.
- **Las claves de ámbito salen del enum del contrato.** BronzeAgeClient las saca de una lista a mano (`claveCorta` en `sesion.ts`) que no
  sigue el orden de `AmbitoTipo` (ahí FACCION sale como BATALLA): al reconectar descarta ámbitos que siguen suscritos. Aquí no pasa;
  allí está por arreglar.
- **Librerías:** ZstdSharp.Port 0.8.1 (MIT, C# gestionado; `Assets/Plugins/ZstdSharp`) para zstd, y Newtonsoft.Json de Unity
  (`com.unity.nuget.newtonsoft-json` 3.2.2, que ya llegaba como dependencia y ahora está en el manifiesto) para los JSON del acceso y de
  `/v1/datos`. El contrato sigue en `Assets/Plugins/BronzeAge` (`Tools/ContratoBronzeAge`); el de hoy está al día con main del motor
  (4a16dab: ningún cambio en `proto/` desde 457c278, con el que se generó).

## Pruebas

`Assets/Tests/EditMode/Mundo` (asmdef `Conquest.Mundo.Tests`):

- `AlmacenTests`: foto, delta, revocado, plaza parcial, niebla parcial, incoherencias, el `Hola` de quien reanuda, las rutas al instante
  y los textos. Sin servidor.
- `SesionContraMotorTests`: de punta a punta contra un servidor de verdad. Crea una cuenta, entra, recibe `Bienvenida` y fotos, baja las
  reglas, el artefacto y el relieve (y la segunda vez los lee de la caché sin red), crea un héroe en un campamento, sale, marcha (resultado
  causal, delta y posición que avanza con el reloj), reconecta reanudando, comprueba que una segunda sesión de la cuenta echa a la primera y
  cierra sesión. **Solo corre con `BRONZEAGE_PRUEBA_URL`**; sin ella sale como ignorada.

El servidor de la prueba ha de ser uno propio, con otro puerto y una carpeta de datos nueva, nunca el de juego (localhost:7878): la
prueba crea cuentas, y una sesión por cuenta. Desde la raíz del checkout del motor, con el binario compilado (`cargo build --release -p
servicio`) y el mundo generado (`node scripts/generar-mundo.mjs`, si no hay uno en `generador/salida/mundo`):

```sh
BRONZEAGE_DIRECCION=127.0.0.1:7891 BRONZEAGE_MUNDO=generador/salida/mundo BRONZEAGE_DATOS=<carpeta nueva> target/release/bronzeage-servidor
```

Y en Conquest, con el editor cerrado:

```sh
BRONZEAGE_PRUEBA_URL=http://127.0.0.1:7891 Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testFilter ConquestTactics.Mundo.Tests -testResults Logs/mundo/editmode.xml -logFile Logs/mundo/editmode.log
```

Al acabar, se para el servidor y se borra su carpeta de datos.

## Las pantallas (UI Toolkit)

La interfaz de fuera de la batalla va en UI Toolkit con la guía «bronce y arcilla» de BronzeAgeClient (`docs/Diseno_Interfaz.md`); la de
la batalla sigue en UGUI. Escena: `Assets/Scenes/GameScenes/MundoScene.unity` (una cámara y un objeto `Mundo` con `ConexionDeMundo` y
`PantallasDeMundo`). LoginScene, CharacterSelection y las demás pantallas de relleno siguen como estaban, y la escena nueva no está en el
flujo de la batalla ni en Build Settings.

| Pieza | Dónde |
|---|---|
| `ConexionDeMundo` | El puente con Unity: token guardado (PlayerPrefs, solo vale para el servidor que lo dio), `Bombear()` en `Update`, carga del mundo con caché en `persistentDataPath/mundo`, avisos por héroe y `CerrarSesion` |
| `PantallasDeMundo` | El flujo: acceso → partidas → carga → la sesión viva (provisional hasta el mapa), y la sesión reemplazada o caducada |
| `PantallaCarga`, `PanelAvisos`, `Dialogo` | La pantalla de carga con sus consejos, el aviso flotante y el panel de avisos, y el cuadro de confirmación |
| `Avisos` (en `Conquest.Mundo`) | El mecanismo: urgentes aparte, el resto agrupado por clave, leídos, guardado y sin repetir lo ya avisado al reanudar |
| `Assets/Resources/MundoUI` | Los UXML de cada pantalla, `Mundo.uss` (los tokens y componentes), el tema y las 12 imágenes de carga con `consejos.json` (el mismo archivo que BronzeAgeClient) |

USS no tiene degradados ni sombras: la placa de bronce se hace con bordes, sin dentículos (el arte en 9-slice está apuntado
en el bloque 5 de la hoja de ruta). Las letras son las del prototipo, Inter y Cinzel (SIL OFL, en `MundoUI/Fuentes` con sus licencias).

Avisos que ya se cuentan: te observaron o pidieron tu plano, columna con hambre, te echan de un lugar, misión cumplida y bandidos
vencidos. Los demás entran con su pantalla (inventario §3.19). El informe de combate va con el paso 5.

Prueba en PlayMode (`Assets/Tests/PlayMode/Mundo`, con `BRONZEAGE_PRUEBA_URL`): crea una cuenta desde la pantalla, ve la partida, entra
con la carga, comprueba la sesión viva y el panel de avisos y cierra sesión con su confirmación. Con `MUNDO_CAPTURAS=<carpeta>` (y sin
`-nographics`) guarda una captura de cada pantalla.
