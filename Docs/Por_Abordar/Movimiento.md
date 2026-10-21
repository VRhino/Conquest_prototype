# Movimiento: lo que queda fuera

Fecha: 2026-10-20. Sale del cierre del lote 2 (`tasks/movement-batch2-plan.md`, resultados en
`Docs/Design/Movimiento_Formaciones.md` §10) y del plan actual (`tasks/todo.md`), cuyo paso 5 repara los tirones de
aceleración y la llegada al slot. Lo que ese paso 5 cubre no se repite aquí.

## Resumen
| # | Tema | Prioridad | Estado |
|---|---|---|---|
| M1 | Error al slot en marcha por encima de 0,5 m | Alta | **Hecho** (S1 0,51 m con C10 y margen en Walk) |
| M2 | Solapes enemigos persistentes en la melé (S6) | Alta | Pendiente |
| M3 | Escena real con 900 agentes (S8) | Alta | Bloqueado (paquete de VFX) |
| M4 | Animación del paso a carrera | Media | Pendiente |
| M5 | Llegada del ancla (C10) | Media | **Hecho** |
| M6 | Anillo de contacto sin cupo duro (C11) | Media | **Hecho**; sin efecto medible aún en S6 |
| M7 | Reasignación de slots por distancia (C12) | Baja | **Hecho** (`e0aa54a1`) |
| M8 | Detalles de ORCA respecto a la referencia | Media | **Hecho** (horizonte 1 s) |
| M9 | Memoria por frame en la evitación | Baja | **Hecho** |
| M10 | Variación de S6 entre corridas | Media | **Mitad hecha**: aproximación idéntica y mediana; la melé aún diverge |
| M11 | Separación posicional (C8) | Baja | Descartado mientras ORCA funcione |
| M12 | Picos de aceleración en la melé | Media | **Mitad hecha** (frenado de llegada); quedan picos de contacto |
| M13 | Llegada más lenta al frenar con aceleración limitada | Media | **Hecho** en S4; S3 con matiz (primera parada) |

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

## M7. Reasignación de slots por distancia (C12)
**Qué es.** Al cambiar de formación, repartir las ranuras minimizando el recorrido total en lugar de por índice.

**Hecho (2026-10-20, `e0aa54a1`).** La referencia no reasigna (ranura k = soldado k) **[dec]**, pero sus plantillas se diseñan
para que la ranura k quede cerca entre formaciones. Las de Conquest no: en S4 (Línea ↔ Testudo) media tropa atravesaba a la
otra y había 6-7 atascos. Ahora, al rehacer la disposición, cada unidad toma la ranura libre más cercana (voraz, solo en el
evento): 0 atascos. Si en el futuro las plantillas se diseñan con orden coherente (F1), se puede volver a "k = k".

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
