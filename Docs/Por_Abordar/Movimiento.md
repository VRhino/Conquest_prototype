# Movimiento: lo que queda fuera

Fecha: 2026-10-20. Sale del cierre del lote 2 (`tasks/movement-batch2-plan.md`, resultados en
`Docs/Design/Movimiento_Formaciones.md` §10) y del plan actual (`tasks/todo.md`), cuyo paso 5 repara los tirones de
aceleración y la llegada al slot. Lo que ese paso 5 cubre no se repite aquí.

## Resumen
| # | Tema | Prioridad | Estado |
|---|---|---|---|
| M1 | Error al slot en marcha por encima de 0,5 m | Alta | Pendiente |
| M2 | Solapes enemigos persistentes en la melé (S6) | Alta | Pendiente |
| M3 | Escena real con 900 agentes (S8) | Alta | Bloqueado (paquete de VFX) |
| M4 | Animación del paso a carrera | Media | Pendiente |
| M5 | Llegada del ancla (C10) | Media | Pendiente (lote 3) |
| M6 | Anillo de contacto sin cupo duro (C11) | Media | Pendiente (lote 3) |
| M7 | Reasignación de slots por distancia (C12) | Baja | En duda |
| M8 | Detalles de ORCA respecto a la referencia | Media | Pendiente |
| M9 | Memoria por frame en la evitación | Baja | Pendiente |
| M10 | Variación de S6 entre corridas | Media | Pendiente |
| M11 | Separación posicional (C8) | Baja | Descartado mientras ORCA funcione |

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

**Por qué queda fuera.** Lote 3. Cambia la sensación de mando: el héroe y la tropa se separan un poco al arrancar y frenar.

**A considerar.** Puede reducir M1. Rompe la identidad héroe = ancla, que debe quedar en el GDD.

## M6. Anillo de contacto sin cupo duro (C11)
**Qué es.** Reservar puestos alrededor de cada objetivo para colocar a los atacantes, con saturación blanda (D6): los que no
tienen hueco esperan a `d_reanudar + s`.

**Por qué queda fuera.** Lote 3. Necesita un sistema nuevo de reservas (`MeleeSlotReservationSystem`) y liberar las reservas
al morir o cambiar de objetivo.

## M7. Reasignación de slots por distancia (C12)
**Qué es.** Al cambiar de formación, repartir las ranuras minimizando el recorrido total en lugar de por índice.

**En duda.** La referencia **no** reasigna: ranura k = soldado k, con id estable. **[dec]** Con plantillas que conservan el
orden (F1 de `Formaciones_y_Posturas.md`), quizá no haga falta. Medir los cruces en S4 con el modelo nuevo antes de decidir.

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

## M9. Memoria por frame en la evitación
**Qué es.** `UnitAvoidanceSystem` reserva la rejilla y las velocidades con `TempJob` cada frame, y el mapa de posiciones
previas de los héroes no borra entradas de héroes que ya no existen.

**Por qué queda fuera.** El coste entra en los 0,65 ms del benchmark y no genera basura de GC. Es una mejora de orden, no un
fallo visible.

**Cómo abordarlo.** Pasar a memoria persistente con capacidad que solo crece (como el bodyblock) y limpiar el mapa de héroes
cuando la entidad desaparece. Si S8 supera el presupuesto, compartir la rejilla espacial con el bodyblock
(`UnitSpatialGridSystem`, un solo escritor).

## M10. Variación de S6 entre corridas
**Qué es.** S6 cambia mucho de una corrida a otra, y dentro de la suite completa se comporta distinto que solo (hubo
corridas sin combate).

**Cómo abordarlo.** Fijar la semilla de la IA enemiga y del ruido, esperar a un estado inicial idéntico antes de medir y
repetir el escenario tres veces, informando de la mediana.

## M11. Separación posicional (C8)
**Qué es.** Empuje entre aliados en el bodyblock, como paso intermedio antes de ORCA.

**Decisión.** No hace falta mientras ORCA funcione (D1). No debe coexistir con ORCA sin coordinar: habría dos escritores del
desplazamiento.
