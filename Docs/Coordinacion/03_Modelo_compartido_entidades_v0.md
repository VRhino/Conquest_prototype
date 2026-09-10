# Modelo compartido de entidades v0

**Estado:** BORRADOR DE DISEÑO  
**Versión:** 0.1  
**Fecha:** 2026-09-10  
**Alcance:** inventario y modelo conceptual; todavía no es contrato de red implementado.

## 1. Qué significa “compartido”

No se compartirá una clase compilada entre TypeScript y C#. Se comparte el significado, identidad y forma
serializada de las entidades que cruzan procesos.

Cada concepto puede tener hasta tres representaciones:

| Capa | Ejemplo | Propietario |
|---|---|---|
| Persistente | escuadra con dueño, veteranía y cantidad | BronzeAge |
| Contrato/DTO | snapshot de esa escuadra autorizado para una batalla | BronzeAge publica; Unity consume |
| Runtime | entidad Squad y entidades Unit con componentes ECS | Conquest |

Solo la capa DTO es compartida. Las clases `HeroData`, `SquadInstanceData` y los componentes ECS actuales se
convertirán en adaptadores internos; no deben dictar por accidente el modelo del servidor.

## 2. Grafo conceptual propuesto

```text
Cuenta (userId)
  └── JugadorDeMundo (gameId + playerId)
        ├── Héroes (heroId)
        ├── Escuadras (squadId)
        ├── inventario/equipamiento
        ├── ubicación ───────────────┐
        └── membresía de Facción    │
                                     ▼
Facción ──► Asentamientos ──► Edificios / Recintos / Población / Almacén
                 │
                 ├── guarnición: referencias/instancias de Escuadra
                 └── trazado lógico ──► representación 3D y mapa de asedio Unity

Ejército/Columna
  ├── participantes: JugadorDeMundo
  ├── escuadras trasladadas
  ├── suministro/caravanas
  └── ubicación/ruta
          │
          ▼
Batalla persistente ──► Reserva ──► BattleTicket inmutable
                                      ├── BattleParticipant + HeroSnapshot
                                      ├── BattleSquadSnapshot
                                      ├── BattleMapSnapshot/Reference
                                      └── BattleRules
                                                │
                                                ▼
                                  entidades ECS temporales en servidor Unity
                                                │
                                                ▼
                                          BattleResult
                                                │
                                                ▼
                               consecuencias persistentes en BronzeAge
```

## 3. Identidad y ámbito

| ID | Ámbito | Persistente | Regla |
|---|---|---:|---|
| `userId` | cuenta global | sí | identidad autenticada, no personaje |
| `gameId` | mundo/partida estratégica | sí | delimita todo estado de juego |
| `playerId` | un jugador dentro de `gameId` | sí | nunca se toma del cuerpo si puede derivarse de sesión |
| `heroId` | héroe dentro de `gameId` | sí | reemplaza el uso de `heroName` como llave |
| `squadId` | escuadra dentro de `gameId` | sí | sobrevive a quedar con cero efectivos si las reglas permiten reponerla |
| `itemInstanceId` | objeto único dentro de `gameId` | sí | distinto de `itemDefinitionId` |
| `settlementId` | asentamiento dentro de `gameId` | sí | mismo ID en estrategia, vista 3D y asedio |
| `buildingId` | edificio dentro de asentamiento | sí | mismo ID aunque cambie estado/modelo visual |
| `enclosureId` | recinto amurallado | sí | sus celdas/puertas conservan identidad lógica |
| `armyId` | columna/ejército del mundo | sí | no es una entidad de batalla |
| `battleId` | encuentro entre apertura y aplicación | sí | une ticket, instancia y resultado |
| `participantId` | plaza/participación en una batalla | sí durante el ciclo | permite reintentos y sustituciones sin confundir al héroe |
| `resultId` | liquidación de batalla | sí | clave de idempotencia persistente |
| `runtimeEntityId` | un World ECS concreto | no | nunca cruza API ni se guarda |

Decisión propuesta para v1: todo dato que afecte al poder —héroes, escuadras, equipo y progresión— está
limitado por `gameId`. La cuenta puede conservar cosméticos y progresión puramente global, pero no introducir
estadísticas de otro mundo sin una regla explícita.

## 4. Identidad social y personaje

### Cuenta

Entidad global de acceso: `userId`, credenciales externas/internas y estado de cuenta. BronzeAge ya separa
usuario autenticado de jugador en partida. Los clientes nunca reciben hashes, secretos ni permisos internos.

### JugadorDeMundo

Extiende conceptualmente el `Jugador` de BronzeAge:

- `gameId`, `playerId`, `userId`;
- liderazgo y progresión propia del mundo;
- ubicación: asentamiento, columna o desconectado;
- `activeHeroId` cuando se necesita representación física;
- facción/residencia derivadas de las reglas existentes;
- memoria personal y exploración.

Cardinalidad propuesta: una cuenta tiene como máximo un `JugadorDeMundo` por `gameId`; el jugador puede tener
varios héroes, como permite hoy `PlayerData`.

### Héroe

Fusiona el personaje persistente de Conquest con la identidad de BronzeAge, sin copiar caches calculadas:

- `heroId`, `ownerPlayerId`, `displayName`, `classDefinitionId`, `gender`, avatar;
- nivel, XP, puntos y atributos base;
- perks desbloqueados;
- inventario y equipamiento por referencia a `itemInstanceId`;
- loadouts por ID;
- estado de disponibilidad: en asentamiento, desplegado, reservado, herido u otra regla futura.

Los valores derivados de equipo/clase se recalculan con una versión de catálogo, no se persisten como otra
fuente de verdad. `heroName` queda exclusivamente como nombre visible.

## 5. Tropas, escuadras y unidades

### Definición de tropa/escuadra

Catálogo versionado que alinea `tropaId` de BronzeAge con `SquadData.id` de Conquest:

- `squadDefinitionId` estable;
- tipo/origen/escalón/rareza;
- coste de liderazgo y reclutamiento;
- tamaño máximo/base;
- formaciones y habilidades permitidas;
- módulos melee/ranged y estadísticas de batalla;
- referencias visuales Unity separadas de los valores de balance.

No se deben mantener dos IDs distintos para la misma tropa. Durante migración habrá una tabla explícita de
aliases; después el ID canónico será único.

### Escuadra persistente

Decisión propuesta: la escuadra pertenece a `playerId`; los loadouts de héroe la referencian. Esto conserva
el modelo de BronzeAge y permite escoltas sin héroe, a la vez que reproduce la selección de Conquest.

Campos conceptuales:

- `squadId`, `ownerPlayerId`, `squadDefinitionId`, `displayName`;
- `totalUnits`, `availableUnits`, `injuredUnits`, `reservedUnits`;
- moral, veteranía, nivel y XP si ambas progresiones se conservan;
- habilidades/formaciones desbloqueadas y formación preferida;
- equipo de escuadra y pérdidas;
- ubicación/contenedor actual: asentamiento, ejército, caravana o reserva de batalla.

Invariantes:

- las categorías de efectivos no pueden sumar más que `totalUnits`;
- una escuadra está en un solo contenedor estratégico a la vez;
- reservar efectivos no los clona;
- las bajas del resultado se aplican sobre los efectivos que figuraban en el ticket, no sobre el estado que
  un cliente reporte después.

### Unidad individual

No es persistente en v1. Cada soldado es una entidad temporal creada por el servidor Unity a partir de un
`BattleSquadSnapshot`. Conserva durante la partida `battleUnitId`, `squadId`, salud, equipo y estado, pero al
cerrar se agrega en supervivientes/muertos/heridos.

Si en el futuro se desea veteranía, nombre o equipo individual, será una nueva decisión de dominio; no debe
aparecer accidentalmente por guardar componentes ECS.

### Loadout

- `loadoutId`, `heroId`, `displayName`;
- referencias ordenadas a `squadId`;
- perks seleccionados;
- liderazgo calculado por servidor;
- estado activo/preferido.

El servidor valida propiedad, disponibilidad y liderazgo. El total enviado por Unity es informativo, no
autoritativo.

## 6. Inventario, equipo, perks y habilidades

Se distinguen:

- `ItemDefinition`: catálogo versionado, tipo, rareza, slots, reglas y generador permitido;
- `ItemInstance`: `itemInstanceId`, definición, cantidad o estadísticas únicas;
- `EquipmentSet`: referencias por slot, no copias completas de objetos;
- `HeroClassDefinition`: clase y fórmulas versionadas;
- `PerkDefinition`, `HeroAbilityDefinition`, `SquadAbilityDefinition`;
- desbloqueos/selecciones persistentes por IDs.

Sprites, prefabs, animaciones y efectos visuales permanecen en Unity. Las estadísticas que afectan el resultado
de batalla deben corresponder con la versión de build/balance autorizada por el ticket.

Las monedas `bronze/silver/gold` locales de `HeroData`, el `gold` de `PlayerData` y la economía de BronzeAge
se solapan y no se pueden fusionar por nombre. Hasta una decisión de diseño, se clasifican como monedas
distintas y no se migran automáticamente.

## 7. Mundo estratégico

Entidades persistentes ya presentes en BronzeAge y que Unity consumirá por proyecciones:

- `Faccion`, `Jugador`, `Asentamiento`, `Edificio`, `Recinto`, `CeldaMuro`;
- `Poblacion`, `RecursoAlmacenado`, `PoliticaActiva`, `CargosAsentamiento`;
- `Escuadron`, `Ejercito`, `Caravana`, `CarroCaravana`;
- `AcuerdoTrueque`, `OrdenMercado`, `CaminoComercial`, `RelacionPolitica`;
- `CampamentoBandido`, nodos de recurso, bosques, ríos y zonas de influencia/facción;
- memoria, niebla de guerra y ubicaciones.

Valores como `Point`, tipos de recurso/edificio/terreno/bioma, cargo, región y estados son tipos de contrato,
no entidades independientes con ciclo de vida.

`Titulo` y las geometrías/proyecciones visibles pueden ser datos derivados: Unity los consume, pero no los
persiste ni los devuelve como autoridad.

## 8. Asentamiento lógico, representación 3D y asedio

El `Asentamiento` persistente sigue conteniendo su estado económico y social. Para Unity se publica una vista
que conserva:

- `settlementId`, centro/origen, sistema de coordenadas y escala;
- `layoutVersion` y semilla/perfil de trazado;
- edificios con `buildingId`, tipo, nivel, estado, posición, orientación y huella;
- recintos con celdas ordenadas, clase muro/puerta/torre, nivel y progreso;
- caminos/calles y puntos estructurales necesarios;
- campos visibles según audiencia, sin filtrar información estratégica privilegiada.

Cada variante 3D se registra en un `SettlementVisualCatalog` de Unity mediante una clave estable compuesta por
tipo/nivel/variante. Todas las variantes de una entrada comparten la misma huella lógica.

Propuesta de reproducibilidad: BronzeAge persiste `visualSeed` y `visualCatalogVersion`; Unity elige variantes
determinísticamente usando `buildingId`. Solo se guarda `visualVariantId` individual si una elección del
jugador o una regla futura hace que la variante tenga significado persistente.

El mapa de asedio se crea desde un `SettlementBattleSnapshot` del mismo asentamiento. No se mantiene una
segunda ciudad diseñada a mano con edificios que puedan divergir. El snapshot puede añadir elementos
tácticos —spawns, objetivos, límites navegables— sin alterar edificio, puerta o muralla de origen.

## 9. Mundo y worldgen

Entidades/datos del contrato geográfico:

- `WorldDefinition`: `gameId`, `mapId`, dimensiones, región, seed y `worldgenVersion`;
- `CoordinateSystem`: origen, ejes, unidades y conversión a coordenadas Unity;
- topología estratégica, biomas, alturas base, ríos, bosques y nodos;
- chunks/sectores versionados para streaming;
- estado mutable separado: recursos agotados, caminos, asentamientos, ejércitos y visibilidad.

El detalle cosmético —microrelieve, vegetación visual, rocas— no forma parte del estado estratégico. No puede
crear obstáculos o accesos con efecto en reglas sin ser promovido al contrato autoritativo.

## 10. Ejército, columna y presencia

Se conserva `Ejercito` como entidad estratégica:

- `armyId`, facción, origen, tipo personal/ejército y líder;
- participantes con antigüedad;
- escuadras trasladadas y suministro;
- posición, ruta, destino/persecución y caravanas adjuntas;
- política y solicitudes de unión.

El héroe activo de cada participante viaja como referencia, pero no reemplaza al participante. Un ejército no
se convierte directamente en una entidad ECS: al abrir batalla produce reservas y snapshots de sus miembros.

## 11. Batalla y contratos derivados

Entidades persistentes nuevas propuestas:

- `Battle`: ciclo, contexto estratégico, bandos, plazos, asignación y resultado aplicado;
- `BattleSide`: atacante/defensor u otra identidad de bando, capacidad y facción;
- `BattleReservation`: recursos, escuadras y participantes inmovilizados;
- `BattleParticipant`: plaza autorizada, jugador, héroe, lado y estado de conexión/confirmación;
- `BattleServerAssignment`: instancia y credenciales limitadas;
- `AppliedBattleResult`: `resultId`, huella del payload, instante y versión resultante.

DTO inmutables:

- `BattleTicket`;
- `BattleParticipantSnapshot`;
- `BattleHeroSnapshot`;
- `BattleSquadSnapshot`;
- `BattleMapReference` o `SettlementBattleSnapshot`;
- `BattleRules` con capacidades asimétricas;
- `BattleResult`, `ParticipantResult`, `SquadResult`, `ObjectiveResult`.

Las entidades runtime existentes —`MatchStateComponent`, héroes, squads, units, proyectiles, zonas de captura,
supply points, buffers de objetivos, órdenes y formaciones— son implementación interna de Conquest. Al final
se agregan al DTO de resultado; no se serializa el World ECS completo.

## 12. Inventario del cliente Vite

El cliente Vite no introduce entidades autoritativas. Sus tipos son copias/proyecciones de BronzeAge:

- `ProyeccionJugador`, `AsentamientoAvistado/Conocido`, `EjercitoAvistado`, `NieblaProyectada`;
- `TrazadoAsentamiento`, `TrazadoMuralla`, `RectanguloLocal`, datos de producción;
- `MapaGenerado` y tipos del evaluador 2D.

Se migrarán como DTO/vistas en Unity. No deben convertirse en un tercer dominio ni conservarse como contrato
independiente después de retirar Vite.

## 13. Catálogos que requieren alineación

BronzeAge ya mantiene catálogos de recursos, edificios, producción, tropas, liderazgo, logística, worldgen,
regiones, política y balance. Conquest mantiene ScriptableObjects para clases, squads, formaciones, habilidades,
perks, ítems, mapas, daño y visuales.

Cada catálogo se clasificará así:

| Categoría | Ejemplos | Fuente futura |
|---|---|---|
| Regla estratégica | coste de reclutamiento, huella de edificio | BronzeAge |
| Regla táctica | daño, formación, captura | paquete/build versionado de Conquest |
| Puente | tropa ↔ squad prefab, edificio ↔ variantes 3D | manifiesto versionado acordado |
| Solo visual | sprite, material, modelo, VFX | Conquest |

## 14. Decisiones aún abiertas

1. Si los héroes son globales a la cuenta o específicos de cada mundo. Este borrador propone mundo.
2. Cómo se unifican veteranía de BronzeAge con nivel/XP de Conquest.
3. Qué significa herido frente a muerto y cuánto detalle devuelve la batalla.
4. Economía y monedas de héroe frente a oro/recursos estratégicos.
5. Si el equipo de escuadra se modela agregado o por piezas/efectivos.
6. Qué reglas tácticas necesitan ser auditables por BronzeAge sin reejecutar la batalla.
7. Política de héroe activo, sustitución y desconexión en una columna.
8. Formato del worldgen 3D y generación de mapas de asedio.

Estas decisiones deben cerrarse antes de publicar `schemaVersion: 1`.

## 15. Siguiente entrega técnica

Tras aceptar/corregir este modelo:

1. crear schemas JSON v1 para identidad, héroe, escuadra, asentamiento y batalla;
2. añadir fixtures dorados neutrales al backend;
3. crear DTO C# sin referencias a UnityEngine ni Entities;
4. probar deserialización de fixtures en Conquest;
5. adaptar gradualmente `HeroData`, `SquadInstanceData` y `BattleData` a esos DTO.

