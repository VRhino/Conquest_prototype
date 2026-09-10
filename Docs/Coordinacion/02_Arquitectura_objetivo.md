# Arquitectura objetivo del juego unificado

**Estado:** DECISIÓN DE PRODUCTO CONFIRMADA  
**Fecha:** 2026-09-10

## División del sistema

El producto final tiene un único cliente de jugador: Unity. El repositorio Vite se usa como referencia durante
la migración y después puede archivarse.

```text
Cliente Unity
  ├── mundo estratégico 3D
  ├── asentamiento 3D
  └── partida táctica de Conquest
          │
          ├── HTTP/WS estratégico ───────► BronzeAgeFase0
          │                                 estado persistente y autoridad estratégica
          │
          └── protocolo tiempo real ──────► Servidor de batalla Unity
                                            autoridad táctica de la partida
                                                      │
                                                      └── resultado firmado/autenticado
                                                          ─────► BronzeAgeFase0
```

El “servidor de batalla Unity” es una ejecución autoritativa del gameplay real de Conquest. No es el cálculo
estadístico actual de BronzeAge ni una simulación de bots que sustituya a la partida. Ejecuta héroes, tropas,
órdenes, daño, muertes y captura de banderas con jugadores humanos conectados.

La palabra “simulación” solo puede usarse en sentido técnico —el servidor mantiene el estado del juego— y no
como resolución abstracta del encuentro.

## Equipos asimétricos

El contrato no fija `15 vs 15`. Cada batalla define dos bandos con capacidades independientes:

- mínimo y máximo de jugadores por bando;
- participantes confirmados;
- plazas reservadas;
- escuadras y efectivos aportados;
- condiciones de inicio, sustitución y abandono.

Una batalla puede ser `15 vs 15`, `10 vs 14` o cualquier composición aceptada por sus reglas. La asimetría
debe ser explícita en el ticket de batalla; no se infiere de clientes que no llegaron a conectarse.

**Nota Importante para codex**: los equipos pueden ser asimetricos pero el cap de partida debe ser simetricos es decir puede se un 10 vs 14 pero el cap de esa puede ser 15 vs 15 al ser un asedio por ejemplo. por ejemplo existen caps de jugadores en un asedios de asentamientos, 15 maximo por bando (este numero es parametrizable a futuro dependiendo de balaceo futuro) y un cap de batallas en mundo abierto sea max 5 por bando por ejemplo, como vez puede variar, con los numeros anteriores imagina que 10 intentan asediar un asentamiento pero hay 25 defensores preparados, a la batalla/partida entran 10 atacantes y 15 defensores, quedando 10 defensores en cola, que cuando vayan muriendo defensores en batalla van entrando los que estan en cola.

## Flujo real de batalla

1. BronzeAge recibe una intención de ataque y valida ubicación, permisos, diplomacia, objetivo y tropas.
2. BronzeAge crea una `Batalla` persistente y reserva participantes, escuadras y suministros.
3. BronzeAge publica un `BattleTicket` inmutable con ambos bandos, héroes, escuadras, efectivos, mapa y
   versiones de contrato/balance.
4. Un orquestador asigna una instancia de servidor Unity. Los clientes autorizados reciben un token limitado
   a esa batalla y se conectan.
5. Los jugadores disputan la partida real de Conquest. El servidor Unity es autoritativo sobre entradas
   aceptadas, daño, muertes, banderas, reloj y victoria.
6. El servidor Unity cierra la partida y genera un `BattleResult`: ganador, razón, captura de objetivos,
   efectivos finales, muertos/heridos y métricas necesarias para consecuencias estratégicas.
7. BronzeAge autentica el productor, comprueba `battleId`, versión y estado, y aplica el resultado una sola
   vez dentro de su cola serial.
8. BronzeAge libera reservas, aplica conquista/bajas/XP/ocupación y publica eventos a los clientes Unity.

## Frontera de autoridad

BronzeAge decide:

- si una batalla puede existir;
- quién puede participar y con qué posesiones;
- qué queda reservado mientras se juega;
- cómo afectan ganador y bajas al mundo persistente;
- qué ocurre ante cancelación, caducidad o fallo de infraestructura.

El servidor Unity decide:

- evolución táctica de la partida;
- posiciones y acciones aceptadas;
- daño, muerte y captura real;
- cuándo se cumple una condición de fin;
- el resumen factual de lo ocurrido.

Un cliente Unity individual no puede declarar el ganador ni las bajas persistentes.

## Consecuencias para Conquest

La lógica actual es reutilizable, pero todavía es local. Para convertirla en una partida real habrá que:

- separar input local de comandos de red;
- ejecutar las reglas críticas en el servidor;
- replicar estado visible y eventos;
- eliminar el `BattleDebugCreator` del flujo productivo;
- cargar `BattleData` desde un ticket versionado;
- producir un resultado completo en vez de guardar únicamente `winnerTeam`;
- distinguir IDs persistentes de IDs/runtime entities de ECS;
- diseñar reconexión, espectador y sustitución sin alterar la autoridad estratégica.

## Mundo 3D

El worldgen actual no se traslada literalmente. El mapa Vite es una representación 2D construida desde datos
públicos; Unity necesita topología y escalas aptas para terreno 3D, navegación, streaming, visibilidad,
asentamientos y encuentros.

BronzeAge conserva la geografía estratégica autoritativa: coordenadas, regiones, rutas, propiedad y elementos
persistentes. Conquest construye su representación 3D. Antes de implementar el revamp debe definirse qué parte
del mundo se genera de forma determinista y qué parte se sirve como asset versionado.

## Asentamientos 3D y asedios

La vista de asentamiento existente en BronzeAge no se reemplaza por un escenario decorativo independiente.
Su grilla, edificios, ocupación, producción e interacción siguen siendo datos autoritativos y Unity los
traduce a un asentamiento 3D.

Cada tipo y nivel de edificio podrá tener varias variantes visuales con la misma huella lógica en la grilla.
La variante permite diversidad entre ciudades, pero no puede cambiar por sí sola producción, capacidad ni
reglas de colocación. Hay que persistir la selección de variante cuando sea necesario reproducir exactamente
un asentamiento.

El mismo estado del asentamiento será la entrada para generar su escenario de asedio. Por tanto, murallas,
puertas, edificios, calles y puntos tácticos necesitan identidades y coordenadas estables que puedan viajar
desde BronzeAge a Unity. El contrato de asentamiento 3D debe diseñarse antes que los mapas de asedio; de lo
contrario existirían dos reconstrucciones incompatibles de la misma ciudad.
