# Movimiento: lo que queda fuera

Fecha: 2026-10-20. Sale del cierre del lote 2 (`tasks/movement-batch2-plan.md`, resultados en
`Docs/Design/Movimiento_Formaciones.md` §10) y del plan actual (`tasks/todo.md`), cuyo paso 5 repara los tirones de
aceleración y la llegada al slot. Lo que ese paso 5 cubre no se repite aquí.

## Resumen
| # | Tema | Prioridad | Estado |
|---|---|---|---|
| M1 | Error al slot en marcha por encima de 0,5 m | Alta | **Hecho** (S1 0,51 m con C10 y margen en Walk) |
| M2 | Solapes enemigos persistentes en la melé (S6) | Alta | **Hecho** (2026-10-28, P0+P1: S6 de 9 a 0 y S7 de 12 a 0) |
| M3 | Escena real con 900 agentes (S8) | Alta | Bloqueado (paquete de VFX) |
| M4 | Animación del paso a carrera | Media | **Hecho** (2026-11-04: el paso sigue la marcha; falta verlo en vídeo) |
| M5 | Llegada del ancla (C10) | Media | **Hecho** |
| M6 | Anillo de contacto sin cupo duro (C11) | Media | **Hecho**; la saturación blanda de D6 ya está (L4-04) |
| M7 | Reasignación de slots por distancia (C12) | Baja | **Hecho** (`e0aa54a1`) |
| M8 | Detalles de ORCA respecto a la referencia | Media | **Hecho** (horizonte 1 s) |
| M9 | Memoria por frame en la evitación | Baja | **Hecho** |
| M10 | Variación de S6 entre corridas | Media | **Mitad hecha**: aproximación idéntica y mediana; la melé aún diverge |
| M11 | Separación posicional (C8) | Baja | Descartado mientras ORCA funcione |
| M12 | Picos de aceleración en la melé | Media | **Mejorado**: S6 de 2,4 a 1,5 % con L4-04; con P0+P1, S6 1,8 → 1,5 % y S7 1,5 → 1,1 % (máx. 140 y 107) |
| M13 | Llegada más lenta al frenar con aceleración limitada | Media | **Hecho** en S4; S3 con matiz (primera parada) |
| M14 | Aceleración en S7 multitud con C13 | Media | **Hecho** (medido: dentro de M10; 1,4 % con L4-04) |
| M15 | Colas y coste de la validación en cuellos de botella (C13) | Media | Pendiente |
| M16 | Más unidades quietas lejos de su slot en la melé tras L4-04 | Baja | **Hecho** (2026-11-04: son soldados en contacto; métrica `idleStalls`) |
| M17 | Monturas: vínculo jinete-montura (L5-6), arco de ataque y aguante de montura (L5-8) | Baja | Bloqueado (sin caballería) |
| M18 | Datos del aguante de escuadra (L5-7) y su escala por la pila | Media | **Hecho** (datos 2026-11-03; escala por la pila 2026-11-04) |

---

## M1. Error al slot en marcha por encima de 0,5 m
**Qué es.** Tras C9, el error medio en marcha es 0,57 m (S1) y 0,60 m (S3), frente al objetivo de 0,5 m del lote 2 y al
objetivo final de 0,35·espaciado.

**Por qué queda fuera.** La causa es la decisión D4: en Walk, el tope de velocidad es la del héroe, así que el retraso que
se acumula al arrancar solo se recupera corriendo. La tropa corre una vez por arranque (entra en Run a 0,9 m y sale a 0,45 m)
y luego camina con un pequeño retraso que no puede cerrar.

**A considerar.**
- Bajar la salida de Run por debajo de 0,45 m dejaba la tropa en Run toda la marcha (se medía Run con velocidad de paso).
- Una salida: que la ancla arranque algo más suave que el héroe (C10), para que el retraso inicial sea menor.
- Otra: un pequeño margen en Walk (por ejemplo, 1,05 × la velocidad base) solo para recuperar, que D4 no permite hoy. Es una
  decisión del usuario.

**Cómo abordarlo.** Medir de nuevo tras el paso 5 (los tirones también afectan al seguimiento) y tras el espaciado nuevo
(el slot de 1,1 m cambia la geometría). Si sigue por encima, plantear al usuario la opción del margen en Walk.

**Hecho (2026-10-21, `Movimiento_Formaciones.md` §14-15).** La llegada del ancla (C10) bajó S1 a 0,61 m y el margen en
Walk (`walkCatchUpRatio` 1,05, decidido por el usuario) a 0,51 m. S3 se queda en 0,71 m: ahí manda la marcha de carrera tras
cada arranque. Coste: S4 forma en 1,00 s en lugar de 0,60 s, dentro del límite de 2 s.

## M2. Solapes enemigos persistentes en la melé
**Qué es.** En S6 quedan entre 5 y 11 pares enemigos solapados más de 0,3 s (antes 16-32). La distancia media de contacto es
1,25-1,55 m.

**Por qué queda fuera.** La parada derivada de C5 deja los cuerpos separados al llegar, pero en la melé los empujes del
bodyblock, los objetivos que se mueven y la falta de reparto alrededor del objetivo vuelven a juntarlos.

**A considerar.** El anillo de contacto (C11, M6) es el arreglo de fondo: coloca a los atacantes alrededor del objetivo en
lugar de amontonarlos en la línea recta. El interruptor de colisión por escuadra (C16) cambia el caso del Muro de escudos.

**Avance (2026-10-21, `Movimiento_Formaciones.md` §17-18).** El anillo, incluso lleno (S7), no cambió los solapes. Lo que sí:
que el bodyblock deje de ser un muelle y resuelva solo el solape, como la cápsula de la referencia: S7 baja de 19 a 9 pares y
de 1,4 a 0,7 % de picos; S6 queda en 15-17 pares. El retroceso del golpe (§19) se probó y no cambia los solapes:
queda como sensación de golpe. Siguiente paso posible: el interruptor de colisión por escuadra (C16).

**Medido (2026-10-26, `Movimiento_Formaciones.md` §21).** El reparto de objetivos con saturación blanda (L4-04, D6) y la
búsqueda por escuadra (L4-09) bajan el p95 de atacantes por objetivo de 6 a 2, pero los solapes no cambian: S6 11 → 11,5
(rango 3-16) y S7 10 → 9,5 (6-11), con 6 corridas. Se confirma que no vienen del reparto. Lo siguiente sigue siendo C16 (F9).

**Hecho (2026-10-28, `Movimiento_Formaciones.md` §22).** La causa: con objetivo el bodyblock usaba 0,35 m de contacto (la
métrica, 0,9 m) y nunca corregía a quien pelea contra un quieto. P0 (una sola distancia, 2·bodyRadius) y P1 ("chocar y
resbalar": la unidad que se mueve pierde la aproximación al enemigo antes de integrar, patrón de la referencia) dejan 0
pares en las tres corridas de S6 y de S7 (antes 9 y 12). Queda vigilar paradas detrás de un enemigo que no es el objetivo
(P4 de `game-re/notes/battle-movement-combat.md`). El interruptor por escuadra (F9, P2) y el bloqueo leído por el objetivo
(P4) están hechos (§22).

## M3. Escena real con 900 agentes (S8)
**Qué es.** La medida obligatoria de D8: 900 agentes en escena real con todos los sistemas nuevos, con un presupuesto de
≤ 2 ms.

**Por qué queda fuera.** `BattleSceneIntegrationTests` falla en el arranque si falta el paquete "Travis Game Assets"
(efectos de impacto), que está fuera de git. Solo hay benchmarks sintéticos: ORCA con 900 agentes da 0,65 ms de mediana y
el bodyblock tiene su propia regresión de 900 agentes.

**Cómo abordarlo.** Importar el paquete (lo hace el usuario) o extender a los tests de rendimiento la excepción de logs que
ya usan los escenarios de medición. Medir también el grabador de métricas a esa escala.

## M4. Animación del paso a carrera
**Qué es.** C9 añadió las marchas Walk y Run, pero no se ha revisado en vídeo que la animación pase a correr cuando la unidad
cambia de marcha (`animationRunningSpeedRatio` = 0,6).

**Cómo abordarlo.** Grabar S1 con Unity Recorder y comprobar el cambio. Si no se ve, que la animación lea la marcha de
`UnitMovementGearComponent` en lugar de deducirla de la velocidad.

**Comprobado (2026-11-04).** `UnitAnimationAdapter` elige el paso por la velocidad medida frente a la velocidad base del
soldado (`LocomotionAnimationSettings`: correr por encima de 0,6 × base, esprintar por encima de 1,1 × base si se pide más
de 1,1 × base), no por la marcha. Pero la marcha Walk va hasta la velocidad base (× 1,05 para alcanzar el slot) y Run, a
2 × base. Con las muestras de S1 (escuderos, base 3 m/s):

| Marcha | Paso Walk | Paso Run | Paso Sprint |
|---|---|---|---|
| Walk | 13 % | 87 % | — |
| Run | 12 % | 18 % | 69 % |

La animación sí cambia con la marcha, pero un paso por encima. Andando en formación se ve la carrera, y corriendo, el
sprint. La coordenada del blend tree (`MoveSpeed`) también sube: un escudero que anda a 3 m/s recibe 5 (la de correr) y uno
que corre a 6 m/s, 12. Los prefabs de soldado no cambian estos umbrales.

**Hecho (2026-11-04, opción elegida por el usuario: el paso sigue la marcha).** Un soldado con `UnitMovementGearComponent`
anda en Walk y corre en Run, sin sprint (`LocomotionAnimationSettings.GetGait(MovementGear, …)`). `MoveSpeed` llega a la
coordenada de su clip (3 andando, 5 corriendo) a 0,6 × base andando y a la base corriendo. Por debajo mezcla con el reposo,
como al arrancar o al parar. El héroe y los soldados sin marcha siguen con la regla anterior. Lo prueban `SoldierGaitTests`
y, en Play, `SoldiersWalkAndRunAsTheirGearSaysAndNeverSprint` (marcha de 6 s: nunca esprinta y el paso coincide con la
marcha). Queda verlo en vídeo; los clips no ajustan su ritmo a la velocidad, así que los Lanceros que andan a 5 m/s pueden
deslizar los pies.

## M5. Llegada del ancla (C10)
**Qué es.** Que el ancla de la escuadra tenga velocidad y aceleración propias (llegada tipo *arrive*) en lugar de ser el héroe
exacto. Por D5, sin limitar el giro.

**Hecho (2026-10-21, `Movimiento_Formaciones.md` §14).** `AnchorArrival` en `SquadAnchorSystem` (`anchorAcceleration` 8 m/s²,
salto a más de 10 m). Mejora todas las llegadas (S4 de 1,37 a 0,60 s; la peor parada de S3 de 2,27 a 1,70 s) y el error en
marcha. La separación héroe-escuadra al arrancar y parar queda en el GDD (§4.3). Con `anchorAcceleration` = 0 se vuelve al
ancla pegada al héroe.

**A considerar.** Si en juego se nota la separación al arrancar, subir `anchorAcceleration` (menos retraso, arranque más
brusco) antes de quitarlo.

## M6. Anillo de contacto sin cupo duro (C11)
**Qué es.** Reservar puestos alrededor de cada objetivo para colocar a los atacantes, con saturación blanda (D6): los que no
tienen hueco esperan a `d_reanudar + s`.

**Hecho (2026-10-21, `Movimiento_Formaciones.md` §13).** `MeleeContactRingSystem` reserva posiciones en el anillo, solo
dentro de ±60° del lado del atacante; sin hueco, espera fuera. En S6 no cambia los números más allá del ruido: con una
escuadra por bando el anillo casi nunca se llena.

**Lo que queda.** Medirlo en una melé de varias escuadras contra una (escenario nuevo, S7) y, si se confirma que ayuda,
añadir el "ancla suave" de L-7 (quien ya está en su posición no recibe empuje del bodyblock). La saturación blanda en la
puntuación de objetivos (D6, `p' = p·(1 − n·k)`) sigue sin hacer: hoy el targeting usa un cupo de 2 por escuadra.

**Hecho lo de D6 (2026-10-26, L4-04, `Movimiento_Formaciones.md` §21).** El targeting puntúa con saturación blanda entre
todas las escuadras y ya no hay cupo duro. Queda el "ancla suave" de L-7.

## M7. Reasignación de slots por distancia (C12)
**Qué es.** Al cambiar de formación, repartir las ranuras minimizando el recorrido total en lugar de por índice.

**Hecho (2026-10-20, `e0aa54a1`).** La referencia no reasigna (ranura k = soldado k) **[dec]**, pero sus plantillas se diseñan
para que la ranura k quede cerca entre formaciones. Las de Conquest no: en S4 (Línea ↔ Testudo) media tropa atravesaba a la
otra y había 6-7 atascos. Ahora, al rehacer la disposición, cada unidad toma la ranura libre más cercana (voraz, solo en el
evento): 0 atascos. Si en el futuro las plantillas se diseñan con orden coherente (F1), se puede volver a "k = k".

**Actualizado (2026-11-02, F1).** No se vuelve a "k = k": tras las bajas los índices quedan dispersos. Al voraz se le añade
una pasada que intercambia ranura entre parejas mientras acorten su camino, de modo que no quedan trayectorias rectas cruzadas.

## M8. Detalles de ORCA respecto a la referencia
**Qué es.** Diferencias conscientes del C14 actual frente a lo leído en la referencia:
- Horizonte de tiempo: la referencia usa 5 s al pasar de "sin estado" a un estado y 1 s en las transiciones; aquí 2 s en
  marcha y 1 s cerca del slot.
- Solapes: la referencia separa en un solo paso de simulación; aquí en 0,25 s, para evitar tirones.
- Vecinos: la referencia no limita su número; aquí se toman los 8 más cercanos por presupuesto.
- Compañeros de escuadra: aquí radio 0,9·espaciado y los quietos no son obstáculo; en la referencia lo deciden las
  relaciones de `RVOSetting`, que están en datos cifrados.

**Cómo abordarlo.** Revisar tras el paso 5 (límite de aceleración) y con el espaciado nuevo. El horizonte de 5 s puede dar
esquivas más tempranas y suaves en los cruces entre escuadras.

**Hecho (2026-10-21, `Movimiento_Formaciones.md` §16).** Se midieron 1, 2 y 5 s. 5 s retrasaba las llegadas de S1 y S4; 1 s,
que es lo que la referencia usa tras su primer cambio de estado, mejora todas las llegadas (S4 de 1,00 a 0,50 s) sin más
solapes entre compañeros. Queda 1 s. Sigue distinto a propósito: 8 vecinos como máximo (presupuesto) y la separación de
solapes en 0,25 s.

## M9. Memoria por frame en la evitación
**Qué es.** `UnitAvoidanceSystem` reserva la rejilla y las velocidades con `TempJob` cada frame, y el mapa de posiciones
previas de los héroes no borra entradas de héroes que ya no existen.

**Por qué queda fuera.** El coste entra en los 0,65 ms del benchmark y no genera basura de GC. Es una mejora de orden, no un
fallo visible.

**Cómo abordarlo.** Pasar a memoria persistente con capacidad que solo crece (como el bodyblock) y limpiar el mapa de héroes
cuando la entidad desaparece. Si S8 supera el presupuesto, compartir la rejilla espacial con el bodyblock
(`UnitSpatialGridSystem`, un solo escritor).

**Hecho (2026-10-21, `c404b6fb`).** Rejilla y velocidades persistentes; mapa de héroes reconstruido cada frame. 900 agentes:
0,48 ms de mediana. Compartir la rejilla con el bodyblock queda para cuando S8 lo pida.

## M10. Variación de S6 entre corridas
**Qué es.** S6 cambia mucho de una corrida a otra, y dentro de la suite completa se comporta distinto que solo (hubo
corridas sin combate).

**Hecho en parte (2026-10-21).**
- La causa principal era el paso de tiempo: el grabador fijaba 30 fps solo mientras filmaba, y la carga de la escena y la
  preparación (unos 8 s) corrían al ritmo real del editor. Cada corrida llegaba a la medida en otro estado. Ahora los
  escenarios corren a paso fijo desde el `SetUp` y vuelven a sembrar `Random` antes de preparar. Resultado: tres corridas
  seguidas son **idénticas frame a frame durante toda la aproximación** (antes ni el primer frame coincidía).
- La mediana: cada corrida guarda también `<escenario>_<fecha>_summary.json`, y el menú *Tools > Movement > Median Of Last
  3 Runs* (`MovementMetricsMedian.Write(n)`) escribe `<escenario>_median_summary.txt` con la mediana campo a campo.

**Lo que queda.** Las corridas siguen separándose desde los primeros contactos de la melé (una adquiere objetivo un frame
antes que otra). Hipótesis **[hip]**: los golpes se detectan con disparadores de PhysX, y `Enhanced Determinism` está
desactivado en *Project Settings > Physics*, así que el orden de los contactos puede cambiar entre corridas.

**Cómo abordarlo.** Probar a activar `Enhanced Determinism` (es un ajuste de todo el proyecto, con algo de coste en CPU:
decisión del usuario) y repetir tres corridas. Si con eso coinciden, la mediana deja de hacer falta para S6; si no, buscar
el siguiente origen comparando el CSV frame a frame (`cmp` sobre las columnas de posición).

**Nuevo (2026-10-26).** S1 tampoco se repite ya en el tiempo hasta formar: con el mismo código, tres corridas dan 0,17, 0,37
y 0,43 s. El error en marcha sí se repite (0,50 m) y pasa a ser el dato de regresión de S1.

## M12. Picos de aceleración en la melé
**Qué es.** Fuera de la melé los picos (> 20 m/s²) están por debajo del 1 %. En S6 eran un 2-3,5 % con máximos de 200-280 m/s².

**Hecho en parte (2026-10-21, `Movimiento_Formaciones.md` §12).** La causa principal no era el bodyblock, como se suponía,
sino el `NavMeshAgent` parando en un frame al llegar a un destino quieto (destino = posición actual en la melé, slot recién
parado). Con el tope de llegada √(2·a·d) y el punto de parada al frenar, S6 queda en un 1,5 % de media con máximos de unos
130 m/s², sin más solapes.

**Lo que queda y por qué.**
- Unidades que entran en alcance **ya pegadas al objetivo** (los dos se acercan corriendo): no hay sitio para frenar sin
  meterse en su cuerpo, y paran casi en seco. El arreglo de fondo es el anillo de contacto (C11, M6): reservar el puesto
  antes de llegar y frenar hacia él.
- Los **empujes del bodyblock** (`agent.Move`, hasta `bodyblockMaxPushSpeed` = 18 m/s por frame en muros). Se probó pasarlo a
  una velocidad con aceleración limitada (10 m/s²): no bajó los picos, subió los solapes y el empuje oscilaba, porque es un
  muelle y con retardo se pasa y sigue empujando cuando el contacto ya terminó. Descartado.

**Cómo abordarlo.** Primero C11. Después, si siguen los picos de contacto, probar un `bodyblockMaxPushSpeed` menor fuera de
los muros (18 m/s es para que un muro no se atraviese) midiendo los solapes (M2), en lugar de rampas sobre el empuje. Para
medir, usar el detector de caídas de `agent.velocity` por frame (se quitó tras usarlo; está descrito en §12) y separar enemigos en el
CSV, que hoy solo graba la escuadra enfocada.

**Avance (2026-10-26, L4-04, `Movimiento_Formaciones.md` §21).** Con el reparto por puntuación, la histéresis y el enganche
(la unidad en alcance no cambia de objetivo), S6 baja de 2,4 % (2,39-2,44 en tres corridas) a 1,5 % de mediana (0,9-2,3,
seis corridas) y el máximo de 197 a 150 m/s². Los cambios de objetivo bajan de 0,22 a 0,16 por unidad y segundo. Quedan los
empujes del bodyblock y las llegadas ya pegadas al objetivo; el siguiente paso de arriba (tope de empuje fuera de los muros)
sigue sin probar.

## M13. Llegada más lenta al frenar con aceleración limitada
**Qué es.** Desde que la velocidad respeta la aceleración, el tiempo hasta formar subió (S3 0,51 → 0,83 s, S4 unos 2 s).

**Hecho (2026-10-21, `Movimiento_Formaciones.md` §12).** La unidad frena hacia un destino quieto con el tope √(2·a·d)
(`arrivalDecelerationFraction` = 0,6): S4 forma en 1,03-1,07 s y la media de S3 baja a 0,75 s.

**Lo que queda.** En S3 la primera parada tarda más (1,10 → 1,43 s): las unidades aún corren para recuperar el retraso del
arranque y ahora frenan en el último metro y medio. Subir la fracción a 0,8 empeoraba S3 y S4 (más rodeo). Se resuelve mejor
reduciendo ese retraso (M1) o con la llegada del ancla (C10, M5), que hace que el slot no se pare en seco cuando para el héroe.

## M11. Separación posicional (C8)
**Qué es.** Empuje entre aliados en el bodyblock, como paso intermedio antes de ORCA.

**Decisión.** No hace falta mientras ORCA funcione (D1). No debe coexistir con ORCA sin coordinar: habría dos escritores del
desplazamiento.

## M14. Aceleración en S7 multitud con C13
**Qué es.** Con `FormationSlotResolveSystem` activo, S7 multitud da una mediana de 1,75 % de muestras por encima de 20 m/s²
(4 corridas), frente a 0,9 % con el sistema apagado (3 corridas). Los solapes no cambian (14 frente a 12) y S6 queda dentro
de su rango. La escuadra local no reubica ningún slot en la melé de las corridas limpias, y entonces sigue el camino
antiguo.

**Hipótesis [hip].** La escuadra enemiga sí reubica: su héroe aparece junto a un borde de la malla (z ≈ 37,8 en
`BattleTestScene`). Eso cambia la melé de forma legítima. También puede ser solo la variación de M10.

**Cómo abordarlo.** Junto con el bloque L4-04/L4-09, que toca los mismos sistemas: 5 corridas por variante y la validación
apagada solo para el enemigo. Si la diferencia sigue con el enemigo sin reubicar, buscarla en `UnitNavMeshSystem` (rama del
slot validado).

**Medido (2026-10-26, `Movimiento_Formaciones.md` §21).** La línea base de este día (C13 activo, targeting antiguo) dio
1,98 % de mediana en 5 corridas, con un rango de 0,78 a 2,53 %: la diferencia de §20 está dentro de la variación de M10.
Con L4-04 + L4-09 queda en 1,43 % (0,86-1,73, 6 corridas), cerca del 0,9 % sin C13. No se probó la validación apagada solo
para el enemigo, que pide un interruptor por escuadra; con la cifra actual no hace falta. Se da por cerrado salvo que S8
(M3) muestre otra cosa.

## M15. Colas y coste de la validación en cuellos de botella (C13)
**Qué es.** En el pasillo de S7, con C13 todos los soldados entran por la boca. En un paso transitable de 1 m hacen cola y se
paran en la boca y en la esquina (4 paradas de más de 0,5 s). La validación hace unas 44 consultas por unidad y segundo en
marcha dentro del pasillo; en campo abierto, unas 15. El sistema cuesta 0,15 ms por frame con dos escuadras.

**A considerar.**
- La cola es esperable. Se puede suavizar ordenando la entrada (reubicar primero a los más cercanos a la boca) o con F12
  (cohesión como consulta: frenar el ancla mientras haya rezagados). La consulta ya existe (2026-11-04,
  `SquadAIComponent.formationReady`); frenar el ancla con ella no está hecho porque cambiaría también al jugador.
- Coste: subir `slotResolveRevalidateDistance`, guardar entre validaciones las celdas ya comprobadas, o pasar la sonda a
  `NavMeshQuery` en un job. Medir con S8 (M3) antes de optimizar.

## M17. Monturas (L5-6, L5-8)
**Bloqueado:** no hay escuadras de caballería, ni modelos, animaciones o datos de monturas (`UnitType.Cavalry` existe pero
ninguna escuadra lo usa). Lo que proponía el catálogo, para cuando las haya:
- L5-6: `MountBinding` (jinete → montura) y `MotorMode`; el jinete reenvía su entrada al motor de la montura, como en la
  referencia.
- L5-8: un arco de giro respecto a la montura fuera del cual el jinete no ataca, y un aguante de montura aparte del del
  jinete.

Lo que ya está preparado: el radio de cuerpo por tipo de escuadra (L5-5, `SquadData.bodyRadius`), la carga por distancia
hacia el objetivo (L1-11), el tipo de unidad en las condiciones de daño (L1-02) y el aguante por marcha (L5-7).

## M18. Datos del aguante de escuadra (L5-7)
**Hecho los datos (2026-11-03, tabla aprobada).** Solo gasta quien corre de verdad: en marcha de carrera y por encima de
`staminaRunSpeedFactor` (1,1) × su paso de marcha, porque un soldado con objetivo de combate está en marcha de carrera aunque
luche quieto (en la melé, el 59-97 % de las muestras). El máximo es 10 por soldado, así que un sprint de toda la escuadra
dura 10 / coste:

| Escuadra | Máximo | Coste por soldado y s | Sprint completo | Recuperación por s | Vuelve a correr al |
|---|---|---|---|---|---|
| Escuderos | 120 | 0,8 | 12,5 s | 8 | 30 % |
| Lanceros | 200 | 0,6 | 16,7 s | 13 | 30 % |
| Piqueros | 200 | 0,9 | 11 s | 13 | 30 % |
| Arqueros | 120 | 0,5 | 20 s | 8 | 30 % |

En S1, S3, S4, S6, S7 y S9 (y sus variantes) el aguante mínimo de la escuadra medida queda entre el 79 y el 93 % y nunca se
agota: el movimiento normal no cansa; cansan las cargas largas encadenadas. **Escala hecha (2026-11-04):** stat de escuadra
`StaminaCost` (`Movimiento_Formaciones.md` §25); ningún asset lo usa aún. Sin barra
de aguante de escuadra en el HUD (decisión del usuario, 2026-11-03).

**Qué es.** El aguante de escuadra funciona y está probado, pero ningún `SquadData` tiene `staminaMax`, así que hoy nadie
se cansa. Además, el catálogo pedía que el coste escale con la pila de modificadores; aquí el coste es un dato fijo.
**Cómo abordarlo.** Fijar máximo, coste por soldado y recuperación por tipo de escuadra (los soldados corren solos para
alcanzar su ranura, así que el coste debe dejar margen para eso), y medir con S1, S4 y S6 cuándo se agotan. Para la
escala, un stat de la pila de la escuadra (como los de L4-10) que multiplique el coste.

## M16. Más unidades quietas lejos de su slot en la melé tras L4-04
**Qué es.** Desde L4-04 + L4-09 el resumen cuenta más "paradas" (unidad a menos de 0,3 m/s y a más de 1 m de su slot durante
más de 0,5 s) en la melé: S6 de 13 a 26 de mediana y S7 de 15 a 31. Fuera de la melé (S1, S4) no cambian.

**Hipótesis [hip].** Con el reparto hay más atacantes en su puesto de contacto, quietos y lejos de su slot de formación, y
menos esperando o persiguiendo; la distancia media de contacto baja de 1,75 a 1,40 m. Sería lo esperado, pero la métrica no
distingue esa parada de una unidad atascada sin objetivo.

**Cómo abordarlo.** Revisar el vídeo de S6 (`Recordings/MovementBaseline`) y, si hace falta, contar en el analizador solo las
paradas sin objetivo o lejos del objetivo.

**Hecho (2026-11-04).** El analizador cuenta aparte `idleStalls`: paradas sin objetivo a menos de `ContactRange` (2,5 m).
Tres corridas: S6 da 24/40/43 paradas y solo 1/0/2 sin objetivo; S7, 20/41/36 y 4/4/4. La hipótesis se confirma: son
atacantes en su puesto. Las 4 de S7 son las colas del pasillo (M15). A partir de ahora, la regresión de atascos es
`idleStalls`, no `stalls`.
