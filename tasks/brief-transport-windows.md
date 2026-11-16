# Encargo para una sesión aparte: el fallo del Transport de Unity en Windows

Investiga y arregla de verdad el fallo de Unity Transport 6.5 en Windows en la batalla en red de Conquest Tactics (Unity 6000.5 + DOTS/Netcode).

## El síntoma

Está en `Docs/Hoja_De_Ruta.md`, sección «7. Sueltos»: cuando muere el proceso de un cliente en una máquina que contesta «puerto cerrado» (ICMP
port unreachable), el servidor de batalla se queda sordo y deja de recibir de todos los clientes.

- Hoy está tapado con margen en `DriversDeBatalla` (búscalo con grep), no arreglado.
- La Hoja propone como arreglo de verdad «Unity, o servidor en Linux».

## Qué hacer

1. **Lee el código y reproduce el fallo.** Lee `DriversDeBatalla` y el código de red de la batalla (el servidor de batalla y los docs de
   `Docs/Arquitectura/` sobre la batalla en red). Si se puede, reprodúcelo con un servidor y dos clientes en Windows, matando un cliente.
2. **Busca la causa y un arreglo dentro de Conquest.**
   - Lo típico en Windows es `WSAECONNRESET` / `SIO_UDP_CONNRESET` en el socket UDP tras un ICMP port unreachable.
   - Arreglos posibles: desactivar `SIO_UDP_CONNRESET` en el socket, una opción de `NetworkSettings`/`NetworkDriver`, o un driver propio.
   - Si no hay arreglo posible desde Conquest, deja escrito por qué y qué pedir a Unity. Añade la alternativa del servidor en Linux, con lo que
     costaría.
3. **Pruébalo.** Si se puede automatizar, una prueba que reproduzca el caso y pase con el arreglo.
4. **Apúntalo.** El resultado va en `Docs/Hoja_De_Ruta.md` (el suelto del Transport) y en un doc de arquitectura: uno nuevo o el de la
   batalla en red.

## Reglas del proyecto

- Lee `CLAUDE.md`.
- Unity en batch falla si el proyecto está abierto en el editor (bloqueo de carpeta): si es así, dilo y no fuerces nada.
- No toques los ficheros sin commit de Víctor:
  - los prefabs de caballería;
  - `ProjectSettings` y `UserSettings`;
  - `CLAUDE.md`;
  - Anton SDF, los addressables y los ajustes de URP;
  - `.claude/settings.local.json`.
- Sin push ni PR: commits en tu rama y avisa.
- Los logs temporales de diagnóstico llevan el prefijo `[BattleTestDebug]` y se quitan al acabar.

## Hecho es

- La causa, explicada.
- El arreglo con su prueba, o el porqué no y qué pedir a Unity.
- La Hoja, al día.

## Contexto

- **El proyecto:** Conquest Tactics es un juego táctico de escuadras 3v3 en Unity 6 con DOTS/ECS. El mundo lo lleva el motor BronzeAgeMotor
  (Rust). Las batallas se juegan en Unity, en red, con un servidor de batalla dedicado (Netcode for Entities / Unity Transport).
- **El estado:** `main` acaba de recibir el bloque 4.
- **El reparto de los sueltos de la batalla** (Víctor):
  - este, en una sesión aparte;
  - los arqueros sin proyectiles, cambiar de escuadra en red y quitar el cálculo local de experiencia y botín, en la sesión principal;
  - probarlo con alguien al teclado, él.
