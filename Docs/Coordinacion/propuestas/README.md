# Propuestas entrantes para Conquest

Esta es la única carpeta de `Conquest_prototype` que Claude puede modificar. Contiene solicitudes detectadas
al trabajar en `BronzeAgeFase0`; no son decisiones ni implementaciones automáticas.

Claude crea aquí propuestas `CQ-*`. Codex, como propietario de Conquest, revisa su estado y las implementa o
rechaza desde su rama de trabajo. Claude no modifica ninguna otra ruta de Conquest.

Las notas humanas de `../Notas_de_integracion.md` tienen prioridad sobre cualquier propuesta.

## Revisión de Conquest — 2026-10-15

Se revisan las once propuestas por su **necesidad**, usando el contrato actual para decidir cómo
atenderla. La antigüedad de un mecanismo no invalida el problema que pretendía resolver.
`ACEPTADO` confirma el alcance que Conquest asume; **no significa implementado ni contrato completo**.
Las condiciones pendientes se indican en cada propuesta y en esta revisión. No se ha implementado gameplay
ni transporte durante esta revisión.

### Fuentes y alcance

- BronzeAge: [modelo de datos](../../../../BronzeAgeFase0/Docs/Coordinacion/01_Modelo_de_datos.md),
  [sesión](../../../../BronzeAgeFase0/Docs/Coordinacion/05_Contrato_de_Sesion.md),
  [mundo](../../../../BronzeAgeFase0/Docs/Coordinacion/06_Contrato_de_Mundo.md) y
  [cierre de BA-001 a BA-006](../../../../BronzeAgeFase0/Docs/Coordinacion/propuestas/CIERRE_FASE_2.md).
- Forma de los mensajes: `BronzeAgeFase0/proto/bronzeage/v1/`, especialmente `session.proto`,
  `battle.proto`, `entities.proto`, `common.proto`, `orders.proto` y `world.proto`. Ante diferencias
  de forma, manda el `.proto`; las reglas de juego se contrastan con `Docs/Game` y `Docs/Mechanics`.
- Conquest: código y assets del checkout `c7bda70c`,
  [arquitectura actual](../../Arquitectura/1_Arquitectura_Actual.md),
  [responsabilidades ECS](../../../Assets/Scripts/Squads/SystemResponsibilities.md) y
  `graphify-out/GRAPH_REPORT.md` (mismo commit). BronzeAge se leyó desde su checkout local actual.
- `Notas_de_integracion.md` está eliminado en el árbol local al revisar. No se restaura ni se deduce
  que sus decisiones hayan sido revocadas. La nota humana conservada en `02_Arquitectura_objetivo.md`
  sobre capacidades simétricas y cola de defensores sigue siendo una necesidad a resolver.

Revisión estática de documentos, esquemas, código y assets. No se ejecutó Unity ni las pruebas de los
repositorios; la existencia de pruebas o ejemplos no se presenta como una ejecución satisfactoria.

### Qué cambia para las once propuestas

1. **Transporte:** una sesión Protobuf por WebSocket, mantenida entre escenas. Desaparecen del diseño
   objetivo el polling de proyecciones/incorporaciones, las rutas HTTP de batalla y los canales antiguos.
   Autenticación inicial y descarga del mundo son las excepciones del contrato 05.
2. **Estado:** almacén por identidad, `Foto`, `Delta`, borrados explícitos, revocación por ámbito,
   versiones y reanudación. Las órdenes se correlacionan por `peticion_id`; la UI recibe el estado
   antes de `ResultadoOrden`. Los eventos narran; no sustituyen a las entidades.
3. **Movimiento y tiempo:** planes y ritmos evaluados con reloj de mundo, factor y pausa. Coordenadas
   estratégicas en milésimas; tiempos en ms de mundo. No reutilizar coordenadas JSON, fechas ISO ni
   velocidades por tick sin conversión explícita. La geometría local de ciudades necesita su propia
   convención documentada; no es una coordenada estratégica por el hecho de usar `Punto`.
4. **Mundo:** artefacto inmutable y paquete Unity derivado, ambos identificados por hash; sectores y
   factor del manifiesto. Unity presenta los planes estratégicos del servidor. La navegación táctica
   dentro de una batalla sigue siendo responsabilidad de Conquest.
5. **Contenido:** IDs canónicos y archivo común de datos versionado; las cifras de las propuestas
   (11/26 tropas, versión 4, composición de NPC, velocidades o huellas) son referencias históricas hasta
   cotejarlas con ese archivo. Añadir una tropa no exige por sí mismo `bronzeage.v2`.
6. **Autoridad:** se mantiene la propiedad acordada en `01_Modelo_de_cooperacion.md`. Sus referencias
   a JSON, fixtures y HTTP son históricas frente al contrato publicado actual: se generan tipos C# y
   se consumen ficheros dorados de Protobuf. Los DTO locales y ECS son representaciones internas.

### Dictamen de las once propuestas

| Propuesta | Estado tras revisión | Necesidad vigente y adaptación |
|---|---|---|
| [CQ-001](CQ-001_xp_calculada_por_servidor_de_batalla.md) | ACEPTADO | XP táctica por héroe/escuadra; exportar curvas, separar del guardado local y resolver recompensas de salidas anticipadas. |
| [CQ-002](CQ-002_escuadras_sin_heroe_y_heroes_bot.md) | ACEPTADO | Héroes bot y tropas autónomas con IA de servidor; separar propietario persistente de comandante táctico. |
| [CQ-003](CQ-003_catalogo_de_tropas_de_bronzeage.md) | ACEPTADO | IDs de tropa canónicos, módulos válidos y efectivos del ticket; completar junto con CQ-006 usando el catálogo vigente. |
| [CQ-004](CQ-004_botin_y_catalogo_de_objetos.md) | ACEPTADO | Botín autoritativo y catálogo de objetos; faltan operaciones persistentes de inventario y contrato de consumo. |
| [CQ-005](CQ-005_incorporaciones_a_una_batalla_en_curso.md) | ACEPTADO | Incorporaciones por sesión, sin polling; conciliar altas, salidas, revisión, tokens y resultado. |
| [CQ-006](CQ-006_tropas_y_edificios_de_las_eras_I_a_III.md) | ACEPTADO | Ampliar contenido y mecánicas tácticas; caballería/carros y levas sin formación requieren trabajo real, además de assets. |
| [CQ-007](CQ-007_escena_del_campamento_de_mercenarios.md) | ACEPTADO | Interior 3D desde planta del servidor; el `.proto` actual todavía no puede transmitir esa planta. |
| [CQ-008](CQ-008_aedas_en_el_mundo_y_la_interfaz.md) | ACEPTADO | Aedas, tecnologías, épicas y crónica mediante ámbitos y órdenes; faltan historia recuperable y algunos datos de presentación. |
| [CQ-009](CQ-009_sigilo_de_faccion_y_estandartes.md) | ACEPTADO | Composición visual por IDs; resolver disponibilidad de sigilos de facciones ajenas y en la instancia táctica. |
| [CQ-010](CQ-010_desconexion_y_abandono_en_batalla.md) | SUSTITUIDO | La necesidad continúa en CQ-011; no crear `AbandonoBatalla`, `/abandonos` ni `porHeroe.salida`. |
| [CQ-011](CQ-011_tipos_de_batalla_y_salidas.md) | ACEPTADO | Ciclo y modos actuales de `battle.proto`; ampliar la propuesta a guerra logística y cerrar reconexión, salidas y cola. |

### Revisión por propuesta

#### CQ-001 — XP

`ResultadoHeroe.xp_ganada` y `ResultadoEscuadra.xp_ganada` mantienen la necesidad. El delta se calcula
en el servidor táctico; BronzeAge persiste XP y aplica la progresión acordada. Conquest ya dispone de
`HeroExperienceCalculator`, `HeroLevelSystem`, `SquadProgressionSystem` y eventos de XP, pero hoy los
sistemas escriben en `SaveSystem` local. No constituyen un productor de resultados de servidor ni una
fórmula completa de puntuación de todos los participantes.

Hay una divergencia que exige acuerdo: `Heroe_Y_Tropa.md` §10.2 describe XP acumulada de escuadra y
una curva estratégica; Conquest guarda XP restante del nivel y utiliza otra curva. El adaptador no puede
copiar `SquadSnapshot.experiencia` directamente a `currentXP`. Para héroe, el modelo dice explícitamente
`experiencia_hacia_siguiente_nivel`. Publicar curvas como tablas versionadas con redondeo, máximos y
semántica de cada experiencia; concretar qué curva gobierna cada nivel antes de migrar progresión.

`xp_ganada` es `sint64`: Protobuf permite valores negativos, por lo que la validación del dominio sigue
siendo necesaria. El `BattleRules` actual no contiene topes de XP: cualquier tope necesita publicación
versionada. No calcular el delta restando XP restante inicial y final, porque subir de nivel la reduce.
Llevar un acumulador de recompensas de la batalla por ID persistente. Resolver también recompensas de
quien sale antes del final (G4). La tropa prestada no gana experiencia según el canon; no identificarla
simplemente por carecer de comandante, que es un concepto diferente.

Cierre verificable: varios héroes y escuadras, subida de varios niveles, escuadra no desplegada con XP 0,
prestadas, reenvío del mismo resultado y salida anticipada, sin doble escritura persistente.

#### CQ-002 — IA y escuadras autónomas

La forma vigente es `BattleParticipantSnapshot.es_bot` y `BattleSide.escuadras_sin_heroe`.
`SquadSnapshot.heroe_id` es opcional. Una guarnición puede conservar dueño persistente aunque su héroe
no la mande. No crear héroes persistentes ficticios para satisfacer la simulación.

El chat **«Localiza el último artefacto de Work»** (`01a139af-ee92-7cd3-82b4-605863f19fa1`) ya lleva
el [diseño de IA táctica v0.3](../../Design/ia-tactica/diseno-ia-tactica.html) y su
[plan](../../../tasks/tactical-ai-design-plan.md). Se incorpora a esta revisión por indicación del
usuario. D13 acuerda que humano y bot solicitan acciones por canales y validaciones comunes; el
cerebro coordina misiones y el gameplay conserva capacidades, cohesión y conducta de unidades.
D14, el contrato común de control/resultados, sigue pendiente. El diseño tiene un primer MVP de
escudos y arqueros, contactos compartidos con memoria/incertidumbre y fases posteriores.
**No se pasa a código de gameplay hasta que el usuario cierre ese diseño.**

Ya existen `SquadAISystem`, FSM, resolución de órdenes y percepción/decisión/ejecución de héroes IA.
Se pueden extender. Sin embargo, `SquadSpawningSystem` nace de una entidad de héroe y añade
`SquadOwnerComponent`; esa dependencia impide considerar resuelto el caso autónomo. La retirada por
muerte del dueño tampoco debe retirar una guarnición cuyo propietario no la comanda.

El flujo de despliegue autónomo debe encajarse en ese diseño reutilizando combate y visuales, con
ancla y un productor de órdenes sobre la ejecución común. Guarniciones/escoltas y tropas sin dueño
son casos pendientes explícitos: la coordinación de héroes bot no los resuelve por sí sola.
La dificultad es configuración táctica versionada de Conquest; solo necesita viajar en
el ticket si BronzeAge debe elegirla. Cantidades y capacidades vienen de datos/ticket, no de los
valores históricos 13/15/5 de la propuesta.

Cierre verificable: bando solo IA, bando vacío permitido, héroe bot, guarnición con dueño presente y
tropa sin dueño; todas sus escuadras quedan contabilizadas en el resultado.

#### CQ-003 — Puente de tropas

Se mantiene la aceptación previa. Las tres bases siguen siendo `spm01`, `arc01` y `sqd01`;
`Spearmen.asset` sigue sin `meleeData` serializado, y `SquadData.IsMelee` depende de ese módulo.
Hay que corregir esa base antes de derivar tropas. Honda, jabalina y arco necesitan módulos propios.

Resolver una sola definición canónica por `tropa_id`, alias explícitos solo para migración y rechazo
de contenido desconocido antes de iniciar la batalla. No inventar una equivalencia entre versiones
de catálogo. CQ-003 se conserva como hito de las once tropas originales; CQ-006 amplía su cobertura.
La compatibilidad real se comprueba contra los IDs del catálogo versionado disponible, cuyo archivo
común aún debe concretarse (G7).

El spawn ya consume snapshots de supervivientes e identidad mediante `SquadIdMapElement`; se reutiliza
ese camino adaptándolo al ticket, sin confundirlo con soporte de `BattleTicket` ya implementado.
`efectivos_autorizados` manda sobre `unitCount`. Rareza y coste local no autorizan liderazgo estratégico.

#### CQ-004 — Objetos e inventario

`Botin`, `ObjetoBotin`, `ItemInstancia`, `Equipamiento` y las versiones del catálogo conservan la
necesidad. La estadística actual es `repeated Estadistica{nombre, valor}`, no el JSON serializado local.
Hay que publicar IDs, categorías, apilamiento, reglas de hueco, compatibilidad y escalas de estadísticas
desde los assets/servicios existentes, con versión reproducible.

Respuestas desde Conquest: `ConsumableManagerService` sí descuenta consumibles; eso no demuestra un
flujo de consumo táctico en red. Hay tienda (`StoreTransactionService`), efectos de moneda/equipo y
recompensas de diálogo fuera de batalla. `InventoryStorageService.InventoryLimit` es configurable y
parte de 72; no existe hoy una capacidad persistente por héroe en ese servicio. Se encontró
compatibilidad arma/armadura y desequipado automático en `EquipmentManagerService`; deben validarse
en BronzeAge para el modo conectado. No se ha identificado un sistema de durabilidad de equipo.

Huecos del contrato actual: `orders.proto` no publica equipar, desequipar, usar consumible ni comprar
objetos del héroe. `HeroSnapshot` no trae inventario de consumibles y `BattleResult` no informa gasto
ni daño de objetos. No habilitar esas operaciones conectadas hasta definir su autoridad, reserva y
aplicación atómica. Un saldo de casillas libres no describe los stacks disponibles; para admitir botín
apilable sobre stacks existentes hace falta una política explícita o información adicional.

Cierre verificable: catálogo válido, huecos/compatibilidad, capacidad y apilamiento, unicidad de
instancias en toda la partida, botín de participante y reintento sin duplicación. Rechazar objetos y
cantidades inválidos en el dominio; la deserialización por sí sola no los valida.

#### CQ-005 — Incorporaciones

`IncorporacionBatalla` llega por `MensajeDeServidor.batalla`; no se consulta `/incorporaciones`.
Deduplicar por batalla y secuencia, conservando el registro en reanudación. La incorporación no sube
`ticket_revision`. Punto de aparición y cola táctica son de Conquest; autorización y reserva, de BronzeAge.

El runtime local tiene spawn de héroes remotos y snapshots reutilizables, pero no un ciclo de admisión
de jugadores en red. Hace falta un registro de participantes independiente de la escena y de las
entidades ECS, también para quien está reservado o nunca se desplegó.

Con CQ-011, el conjunto final es `(ticket + incorporaciones) − salidas confirmadas`. Las escuadras sin
héroe también se incluyen. Las nunca desplegadas conservan efectivos: `desplegados = 0`, `muertos = 0`,
`supervivientes_al_cierre = efectivos_autorizados`, XP 0. No enviar todas las cifras a cero.

La sustitución del token vía `POST /tokens` ya no es un mecanismo vigente: la dirección de
`TokensBatalla` en `session.proto` requiere aclaración (G2). Definir además el corte entre última
incorporación y aceptación del resultado: una alta concurrente no debe dejar un resultado incompleto
sin una respuesta que permita conciliarlo.

#### CQ-006 — Eras, tropas y edificios

Se acepta la ampliación I-III. Su lista de quince tropas nuevas complementa CQ-003; no se fija 26 como
el tamaño perpetuo del catálogo. Eras IV-V siguen pospuestas según el cierre BA-006.

`UnitType.Cavalry` es clasificación; no acredita monturas ni carga implementadas. Tampoco basta un
asset para representar carro, arquero montado o una falange. Reutilizar proyectiles, combate, órdenes
y formaciones existentes; añadir solo las capacidades que faltan. `FormationType.Dispersed` no equivale
a «sin formación»: el spawn exige biblioteca y posiciones de formación, de modo que leñadores y
granjeros necesitan una vía compatible con su comportamiento desorganizado.

No hace falta ampliar el antiguo `tipo` estratégico para describir cada mecánica táctica: Conquest la
resuelve por `tropa_id` y configuración propia. Para carros, el número de unidades autorizadas debe
definir qué cuenta como unidad y baja; auriga, combatiente y caballos pueden ser partes de esa unidad,
pero su muerte no se puede traducir a varios soldados sin acuerdo explícito.

`caballerizas` y `salaConsejo` no tienen entrada visual específica en `SettlementPreview` (el visor
puede dibujar un cubo genérico). Los edificios de ámbito mapa se excluyen allí correctamente. La
representación vigente de extractores y pueblos debe seguir el modelo nuevo, no colocar `minaHierro`
en una rejilla urbana por la tabla histórica. Las huellas se reciben del trazado/catálogo versionados.

#### CQ-007 — Campamento 3D

La necesidad sigue completa: ámbito `CAMPAMENTO`, escena al confirmar la ubicación del héroe y
liberación al revocarlo. Se reutiliza la construcción por huellas de `SettlementPreview`, cuyo lector
actual consume un JSON local antiguo y no sirve directamente como consumidor de sesión.

**Bloqueo concreto:** `CampamentoMercenarios.edificios` solo contiene tipos de edificio. No hay
`EscenaCampamento`, posiciones por edificio, calles, empalizada ni `layout_version` del campamento en
los `.proto` leídos. El contrato 06 exige que el servidor calcule el trazado; Unity no puede completarlo
regenerándolo desde el ID. La planta debe incorporarse al contrato de sesión con unidades, huellas,
orientación y versión, y su fichero dorado.

`origen` puede seleccionar aspecto; las variantes puramente visuales pueden derivarse de IDs estables
y de una versión artística sin decidir geometría autoritativa. No se necesita `visualSeed` de red para
un primer blockout. Conquest puede representar una puerta al sur si así viene en la planta; no impone
una orientación nueva al servidor. Presencia caminable de otros héroes requiere otro contrato si se
quiere compartir posiciones, no se deduce de `residentes_ids`.

#### CQ-008 — Aedas y crónica

Los Aedas ya son entidades de sesión; los itinerantes tienen `Plan`. Evaluar ese plan con el reloj,
respetar niebla y retirar lo revocado. Las tres órdenes existen en `orders.proto`: comprar tecnología,
empezar y abandonar épica. Las pantallas siguen la respuesta causal y muestran el rechazo del dominio.

Los textos de tecnología, capítulos, requisitos y enfriamientos deben salir del archivo común
versionado o de estado proyectado explícito. El esquema actual de `EpicaEnCurso` no trae todos los
textos/objetivos que resumía la propuesta; `TecnologiasFaccion.reveladas` es una lista de IDs.

La crónica en vivo puede usar `EventoDominio`, pero **no hay consulta paginada, entidad de crónica ni
historia inicial equivalente al antiguo `GET /cronica`** en el contrato de sesión. La reanudación no
garantiza la historia para un cliente nuevo que recibe `Foto`. Hace falta un mecanismo de historia
por sesión y definir audiencia, orden estable, identidad y deduplicación de entradas. No deduplicar
solo por versión: un cambio puede producir varios eventos.

#### CQ-009 — Sigilos

`Sigilo` en `common.proto`, `Faccion.sigilo` y `Titulo` sostienen la petición. Componer por IDs del
catálogo versionado, con fallback neutro visual y sin cambiar la identidad canónica. Reutilizar la
referencia SVG del cliente antiguo cuando proceda, conservando los créditos de cada recurso.
Cantidades de formas/emblemas y versión 4 no se convierten en constantes del renderer.

Una bandera representa la Facción del participante o ejército, no necesariamente la Facción titular
de su bando. Los bandos mixtos impiden teñir a todos con `BattleSide.faccion_id`.
`BattleTicket` solo transmite IDs de Facción, sin sigilo; la sesión táctica no publica un catálogo de
Facciones. Tampoco está concretado qué referencias públicas ajenas se entregan en `VISTA` frente al
ámbito `FACCION` propio. Definir cómo recibe cada consumidor los sigilos que necesita, sin acceder a
la información privada de otra Facción. Coronas, ligas y títulos requieren esas relaciones públicas.
Se acepta presentación inicial de identidad; editor de creación del sigilo y arte definitivo son otros alcances.

#### CQ-010 y CQ-011 — Reconexión, modos y salidas

CQ-010 pasa a `SUSTITUIDO` por CQ-011, conservando sus necesidades. Usar `SalidaBatalla.motivo` y
`ResultadoHeroe.derrotado` con presencia: ausente aplica la regla heredada del bando; `false` explícito
no es ausencia. La desconexión de transporte no debe confundirse con abandono voluntario.

Para conservar «desconectado = resultado de su bando», mantener al participante reservado hasta que
reconecte o se conozca el resultado. Sacarlo antes exige fijar `derrotado` cuando todavía se desconoce
el ganador; `SalidaBatalla` no expresa una consecuencia diferida. No cambiar esa regla por una derrota
automática al agotar un tiempo de gracia. Conducta de su tropa y plazo son decisiones tácticas de
Conquest, parametrizadas; no estaban resueltas por la propuesta.

El servidor conserva el estado al reconectar: mismo héroe e instancia, sin repetir spawn ni reservas.
`no_conectado` antes de inicio invalida asignación/tokens y cambia revisión; esperar el nuevo ticket
autoritativo antes de reasignar. Una salida durante el combate conserva revisión y deja de pertenecer
al resultado final solo cuando BronzeAge la confirma. No basta con quitar su GameObject.

Los modos vigentes son los seis casos de `ContextoEstrategico`: asedio; campo abierto (el booleano
`solitarios` distingue persecución); caravana con `MODALIDAD_DISPUTA`/`MODALIDAD_AGUANTE`; bandidos;
saqueo de pueblo; avanzada. En caravana se reportan `carros_perdidos`; BronzeAge calcula consecuencias
de carga y destrucción. No persistir un cambio de dueño de la caravana por la descripción antigua.
En saqueo, respetar `protegido`; no convertirlo en conquista. Avanzadas incluyen etapa.

Conquest tiene captura, reloj y ciclo de match reutilizables, pero `BattleData` no selecciona esos
modos; `HeroRespawnSystem` solo reaparece al héroe local, y `MatchControllerSystem` no es un servidor
de red por llamarse «server-side» en un comentario. Faltan política de reaparición por modo, victoria,
admisión, reconexión, salidas autenticadas y resultado completo. Las opciones de diseño de CQ-011
(por ejemplo rendición a diez minutos) no se convierten en reglas aprobadas por esta revisión.

La cola humana de defensores excedentes sigue sin representación explícita en `BattleTicket` ni
mensaje de promoción. No se puede cumplir «Unity mete al siguiente» si BronzeAge no identifica y
reserva a los que esperan. Concretar si se entregan como participantes reservados o como incorporaciones
autorizadas; la cola táctica no autoriza a inventar un héroe ni superar la capacidad simultánea.

### Huecos que debe cerrar el propietario del contrato

Estos hallazgos son solicitudes de aclaración/cambio para BronzeAge; no alteran unilateralmente sus
esquemas ni afirman que el backend los haya aceptado.

| ID | Prioridad | Evidencia y decisión necesaria |
|---|---|---|
| G1 | Alta | `Cambio` permite parches, pero los escalares no `optional` y las listas/mapas carecen de presencia. No se distingue «no cambió» de `false`, `0`, `""` o colección vacía. Añadir máscara de campos o mensajes de parche con semántica explícita; definir borrado de colecciones. La presencia Protobuf actual no basta. Afecta a todas las pantallas. |
| G2 | Alta | `TokensBatalla` solo existe en `MensajeParaServidorDeBatalla` (BronzeAge → Unity); `BattleServerAssignment` incluye tokens enviados por Unity y no hay mensaje de actualización en la dirección contraria. Definir emisor/generador y renovación tras incorporaciones. Falta además un mensaje privado para entregar al jugador instancia y token propios; `Batalla` no contiene asignación. No usar ámbitos colectivos para credenciales. |
| G3 | Alta | Los mensajes de servidor táctico no tienen `peticion_id` ni respuesta de aceptación/rechazo correlacionada. `ResultadoOrden` solo responde a `Orden`. Definir confirmación, conflictos de asignación/revisión, reenvío, salida aceptada y resultado aplicado, y reconstrucción del registro tras reconectar. Un envío WebSocket no prueba persistencia. |
| G4 | Alta | `SalidaBatalla` solo lleva bajas y derrota; `BattleResult` excluye salidos. No hay vía para XP/botín de quien salió ni para posponer la consecuencia de desconexión hasta conocer el bando ganador. Completar ambos casos y la conservación exacta entre salidas y cierre. |
| G5 | Alta | `CampamentoMercenarios` no tiene planta; `Asentamiento` contiene edificios y recintos pero no calles ni un mensaje de trazado con versión. `SettlementBattleSnapshot` tampoco lleva calles. Publicar la geometría resuelta que prometen 05/06 y CQ-007, con unidades locales inequívocas. |
| G6 | Alta | CQ-004 requiere catálogo validable y órdenes persistentes de inventario; no hay órdenes de equipo/consumo en `orders.proto` ni reporte de gasto en batalla. Definir capacidad, stacks, mutación y reservas antes de habilitar economía conectada. |
| G7 | Alta | Se publican números/versiones de catálogos, pero falta concretar distribución e identidad del archivo común para Unity y los catálogos de héroe/objetos, curvas, IDs de formaciones/perks y representación de carros. No inferir el roster vigente del antiguo JSON retirado. |
| G8 | Media | CQ-008 exige crónica para un cliente nuevo; `Foto`/`EventoDominio` no definen su recuperación histórica ni identificador de entrada. Publicar ese mecanismo por sesión y los textos/requisitos versionados de épicas y tecnologías. |
| G9 | Media | CQ-009 necesita sigilos y relaciones públicas ajenas en mapa y batalla. Definir entrega por visibilidad/identidad sin obligar a abrir el ámbito privado de otra Facción. |
| G10 | Alta | La cola de excedentes pedida por el autor necesita IDs, reserva y promoción autorizada. El esquema actual no distingue esa cola de participantes activos ni define su promoción. Conciliarla con capacidades y CQ-005/CQ-011. |

Dos precisiones adicionales: (a) el reloj local debe avanzar según `factor_milesimas` y detenerse con
`pausado`; sumar un desfase fijo solo basta con factor 1000 sin pausa; (b) el SHA de Protobuf no debe
recalcularse desde una reserialización arbitraria de mapas: la propia documentación de los dorados
advierte que su orden no está fijado. Especificar bytes/orden canónicos del hash de mundo y del paquete.

### Orden de adopción y estado comprobado

1. Cerrar G1-G3 y G7; generar C# desde una versión fijada del contrato y conectar la sesión con almacén,
   reloj, reanudación y respuesta causal. Reutilizar los dorados de `BronzeAgeFase0/contratos`.
2. Consumir mundo/paquete por hash y planes; adaptar trazado e interiores cuando se cierre G5.
3. Construir el ciclo autoritativo de batalla (CQ-005/CQ-011 y G4/G10) junto al puente inicial de
   tropas/IA (CQ-002/CQ-003); no iniciar tickets con contenido o modos sin soporte.
4. Integrar XP y objetos con sus catálogos y mutaciones persistentes (CQ-001/CQ-004), y ampliar
   contenido/mecánicas con CQ-006. El consumo de botín requiere G6.
5. Integrar campamentos, Aedas/crónica y sigilos (CQ-007/008/009) sobre la misma sesión; su trabajo
   visual puede avanzar al disponer de geometría y catálogos, sin esperar todos los modos de batalla.

En Conquest, `BronzeAgeCampaignConnection` sigue siendo polling HTTP y `SettlementPreview` lee un
JSON local. No se encontró consumo de `BronzeAge.Contract.V1` ni transporte autoritativo de batalla en
`Assets/Scripts`. Esas piezas son base reutilizable, no integración finalizada.

BronzeAge ya contiene servidor de sesión Rust, generador/cargador de artefactos y pruebas de contrato
C#/Rust/TypeScript; las cabeceras «sin código» de 05/06 están desactualizadas respecto al checkout.
Sin embargo, `crates/nucleo/src/sesiones.rs` maneja `ComandoSesion::Batalla { .. } => {}` sin procesarlo.
Tener mensajes de batalla en `.proto` aún no acredita el ciclo funcional en Rust. Los dorados observados
cubren sesión y manifiesto; no se encontraron casos dorados de ticket/incorporación/salida/resultado
ni planta de campamento. Esos casos y la comprobación Unity pertenecen al cierre de cada integración.

