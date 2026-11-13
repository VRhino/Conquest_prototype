# Plan: bloque 4, paso 5 — del mundo a la batalla y vuelta

Estado: **5A hecha** (2026-11-13, commit en main); 5B pendiente del visto bueno de Víctor a los dos cambios en el hilo de tropas. Al plan lo corrigió el coordinador: la sesión del mundo **sigue viva** (DontDestroyOnLoad), no se cierra. El encargo viene de Víctor («Vale, avanza con lo siguiente») por el coordinador.
Primero va el análisis; el plan se manda antes de tocar nada y hay una parada al final de cada tanda.

## Lo que hay (análisis)

**Motor** (358af5ff):
- **Con `BRONZEAGE_BATALLAS_EN_UNITY=1`** (regla de partida nueva: no se cambia con la partida empezada), `atacar` sobre `band-N` abre
  una entidad `Batalla` en el ámbito `BATALLA:<uuid>`. Los estados son convocando → asignada → en_curso → aplicada, más cancelada y
  fallida.
  - El lanzador reclama la batalla, arranca un proceso de Conquest por batalla (`--batalla --lanzador --puerto`) y genera un token
    por héroe humano.
  - El cliente lo recibe en `Batalla.asignacion_propia` ({host, puerto, udp}, token, expira). **No hay GET de asignación en Rust.**
  - Plazos: 5 min para asignarse y 5 min para empezar. Si no se cumplen, queda `fallida` sin castigo.
- **Sin el interruptor**, el combate se resuelve por cifras en el acto y no hay `Batalla`.
- **Rechazos de `atacar`:**
  - `heroe.desconocido`, `bandidos.desconocido`, `orden.forma_invalida`, `batalla.bloqueado`, `no_autorizado`,
    `heroe.fuera_del_mundo`;
  - `heroe.sin_columna`, `ejercito.desconocido`, `columna.en_batalla`, `columna.en_formacion`, `heroe.herido`;
  - `combate.lejos`, `combate.proteccion_de_campamento`.
- **Rechazos de `cancelarBatalla`:** `heroe.desconocido`, `batalla.no_existe`, `no_autorizado`, `heroe.fuera_del_mundo`,
  `batalla.no_puede_cancelar` (solo quien la inició, y solo en convocando o asignada).
- **Informe:** no hay mensaje propio. Sale de `Batalla.resultado` (BattleResult: ganador, razón, por escuadra, por héroe) más los
  hechos `evento_pve.*`, `heroe.herido` y `heroe.botin`. El botín y la experiencia los pone el motor.
- **Sesión:** puede caerse durante la batalla. El héroe sigue en su columna (`en_batalla`). Al reconectar con
  `versiones_conocidas` se recupera todo.
- **Batalla de mentira** (`LANZADOR_PROCESO=mentira`): hace el ciclo, pero **no escucha en el puerto**, así que el cliente no
  puede entrar.

**Conquest (hilo de tropas):**
- **Entrada de cliente que ya existe:** `OpcionesRed` (Cliente, Ip, Puerto, Token, BatallaReal=1) y luego reiniciar los mundos
  como hace `BatallaRealServidorTests`.
  - `EntrarEnBatallaClienteSystem` manda el token.
  - El servidor monta héroe y tropas desde el ticket. El aspecto sale de `TestHeroPool` (bloque 3, pendiente).
- **`EspejoClienteSystem`** carga BattleScene en modo Single, así que MundoScene se va.
- **No hay gancho de vuelta:** el cliente se queda en BattleScene al acabar.
- **Lo que no se reinicia entre batallas:** `Abandonada`, `OpcionesRed`, `NoLocalHero`…
- **Servidor sin pantalla:** `Builds/PruebaRed/PruebaRed.exe` (`PruebaRedEditor.Construir`). Es un reproductor normal en
  batchmode; ya existe.

**Prototipo:**
- No tiene pantalla de espera, ni `cancelarBatalla`, ni token: proyecta `batallas: []`.
- Sí tiene:
  - el botón ⚔ Atacar con su motivo de bloqueo (herido, sin columna, lejos, protección del campamento de mercenarios);
  - la ficha de batalla (`panelBatalla.ts`: «Contra bandidos», estados, «Atacan / Defienden»);
  - el informe (`informeCombate.ts`: Victoria o Derrota, bandos con poder, tabla de bajas con el icono de tropa a 16, botín en
    palabras, «Pierdes del carro», «Quedas herido»);
  - los avisos «Combate».

## Decisiones (mías; el coordinador puede corregirlas)

1. **La sesión del mundo se cierra al entrar y se recupera limpia a la vuelta.** No se queda viva.
   - El hilo de tropas carga BattleScene en Single, y mantener viva MundoScene obligaría a cambiarlo a Additive y a convivir con
     dos mundos ECS. Eso cambia su diseño.
   - El motor admite la desconexión durante la batalla, y el token de acceso ya se guarda en PlayerPrefs.
   - Al entrar se cierra el WebSocket **sin** `cerrarSesion`, porque esa orden sacaría al héroe del mundo.
2. **Batalla real, no de mentira:** motor 358af5ff con el interruptor, el lanzador de verdad (compilado de 358af5ff en el
   scratchpad, sin tocar el checkout de Víctor) y `PruebaRed.exe` como servidor sin pantalla.
   - El cliente es el editor en PlayMode.
   - Si no sale, lo digo antes de sustituirlo.
3. **Tanda 5A:**
   - Atacar desde la ficha y la pantalla de espera (UI Toolkit, con la ficha de batalla del prototipo y sus iconos).
   - `cancelarBatalla` mientras se pueda.
   - Las frases de todos los rechazos.
   - Con `asignacion_propia`, entrar en BattleScene en red con el token.
   - Llega hasta «en la batalla, con mi héroe y mi tropa».
4. **Tanda 5B:** la vuelta al mundo y el informe de combate.

## Cambios en lo del hilo de tropas (se proponen; no se hacen sin visto bueno)

- Reiniciar los estáticos al empezar una batalla de cliente (`Abandonada`, `OpcionesRed`, `NoLocalHero`). Es necesario desde la
  segunda batalla del mismo proceso; es cosa de la 5B.
- Una señal de vuelta (fin de partida, abandono o expulsión) en el cliente. Es cosa de la 5B.
- `NetworkEndpoint.Parse` necesita una IP literal. El lanzador da 127.0.0.1, así que vale hoy.
