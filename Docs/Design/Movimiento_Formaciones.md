# Movimiento y formaciones: modelo objetivo para la experiencia de las unidades en pantalla

Estado: propuesta de diseño (no implementada). Origen: análisis estático de `Assets/Scripts/` y estudio comparado de otro juego táctico (solo patrones y órdenes de magnitud; no se copian valores ni código). Complementa `Aprendizajes_Referencia.md` §4.3, §4.5 y §5 (L3-01..06, L5-1..5), que aquí se concretan en leyes, parámetros y pruebas.

Prioridad declarada por el usuario: **cómo se ven y se sienten las unidades en pantalla**: marcha, formación, relación entre aliados, contacto con el enemigo y distancias.

Convenciones: rutas relativas a `Assets/Scripts/`. `(verificado)` = releído en el código del proyecto al escribir este documento. `(no reverificado)` = viene de las notas de análisis y no se ha vuelto a abrir. `[hip]` = hipótesis que solo Unity en ejecución puede confirmar.

Símbolos usados en las fórmulas:
- `rc`: radio de cuerpo de contacto de una unidad (hoy no existe como dato; el agente NavMesh usa 0,5).
- `m`: margen entre cuerpos (aire visible entre dos soldados).
- `s`: espaciado de la rejilla de formación (`s = 2·rc + m`).
- `R`: radio de la formación (distancia máxima de un slot al centro).
- `v_i`: velocidad máxima de marcha de la unidad `i`.

---

## 1. Qué se ve hoy y por qué

### 1.1 Afirmaciones de problema comprobadas en el código

| Afirmación | Resultado | Dónde |
|---|---|---|
| Retardo de salida aleatorio de 0,5-1,5 s en Follow | (verificado) `Random.Range(unitFollowDelayMin, Max)` al pasar Formed a Waiting; valores 0,5 y 1,5 en el authoring | `Squads/Systems/UnitFormationState.System.cs:126`; `Squads/SquadSpawnConfig.Authoring.cs:39-40` |
| Un único umbral de 0,2 m, sin histéresis | (verificado) la misma `slotThresholdSq` decide Formed a Waiting (l.121) y Moving/Waiting a Formed (l.58, 140, 149). Solo Hold usa un umbral distinto (1,0 m) | `UnitFormationState.System.cs:20, 58, 120-122, 140, 149`; `Authoring.cs:10-11` |
| Destino que avanza a saltos de 0,25 m | (verificado) el destino proyectado se reutiliza mientras el slot se mueva menos de 0,25 m | `Squads/Systems/UnitNavMesh.System.cs:90-91, 306-309`; `Authoring.cs:25` |
| Rejilla rígida con el yaw del héroe | (verificado) `rotation = heroWorldPos.rotation` sin filtro y `position = héroe + forward·offset`; la rejilla se rota con esa rotación | `Squads/Systems/SquadAnchor.System.cs:85-88`; `Squads/FormationPositionCalculator.cs:~77-79` |
| `NoObstacleAvoidance` forzado cada frame | (verificado) | `UnitNavMesh.System.cs:118` |
| `UnitSpacing` sin lector | (verificado) `minDistance` y `repelForce` solo se hornean y se copian; `Formation.System` solo escribe el campo `Slot`; ninguna consulta los lee | `Squads/SquadUnitSpawnBuilder.cs:52-53`; `Formation.System.cs:167-170`; `Authoring.cs:30-31` |
| Contacto real de 0,35 m | (verificado) dos unidades con objetivo solo se separan por debajo de `bodyblockEngagingRadius` (0,35) y con fuerza 3 | `Squads/Systems/UnitBodyblock.System.cs:242-247`; `Authoring.cs:22-23` |
| Lanceros parados a 0,75 m | (verificado) `attackRange` 1,0 en `Spearmen.asset:148`, por `StopDistanceFactor` 0,75: parada a 0,75 m centro a centro, menos que un cuerpo de 2·0,5. Escuderos: 1,5·0,75 = 1,125 m | `UnitNavMesh.System.cs:71, 183`; `Resources/Squads/Spearmen/Spearmen.asset:148`; `Squires.asset:155` |
| El combate fuerza `Attack` y anula la correa | (verificado) con reacción de combate y orden distinta de Hold, la orden ganadora pasa a `Attack`; `CanPursueTarget` devuelve verdadero siempre para `Attack`, así que `unitLeashDistance` (6 m) no se aplica. La copia a `currentOrder` es de `SquadOrderSystem` (no reverificado) | `Squads/Systems/OrderResolution.System.cs:68-75, 104-109`; `UnitNavMesh.System.cs:378-384` |
| El avance a 2x depende solo de prisa o de tener objetivo, no del sprint del héroe | (verificado) | `Squads/Systems/UnitFollowFormation.System.cs:107` |
| Ningún aliado repele a otro | (verificado) `if (current.team == other.team) continue` | `UnitBodyblock.System.cs:229` |
| Con objetivo, la unidad en Waiting sigue persiguiendo | (verificado); sin objetivo o en Hold hace `ResetPath` cada frame | `UnitNavMesh.System.cs:249-254` |
| `UnitAttack` decide el alcance con distancia centro a centro | (verificado); el golpe real depende además del trigger del arma (tamaño no leído) | `Combat/UnitAttack.System.cs:99-106` |

### 1.2 Afirmaciones que no se sostuvieron tal cual

- El cooldown de cambio de formación está en `Formation.System.cs:136`, no en la línea 148 que citaban las notas (el valor, 1 s, es correcto).
- `baseSpeed` del héroe en `HeroEntity.Authoring.cs:18` es 2; las notas decían 3 m/s "de prefab" (el prefab lo sobrescribe, no reverificado). El diseño de abajo no depende de ese número: usa `v_i` leído de datos.
- La asignación de slots por índice es cierta (`Formation.System.cs:100-122`, `unitIndex = i`), pero solo ocurre en un cambio explícito de formación. Los supervivientes conservan slot tras una baja.
- "El centro de rejilla va por delante del héroe" depende del valor de escena (`followForwardOffset` 2, 3 en la escena de pruebas desfasada).

### 1.3 Síntoma visible, causa y ubicación

| Síntoma visible | Causa | Fichero:línea |
|---|---|---|
| Acordeón al arrancar: unas filas salen y otras esperan hasta ~1 s | Retardo aleatorio por unidad, sin relación con la fila | `UnitFormationState.System.cs:126` |
| Parpadeo Moving/Formed/Waiting al llegar; paradas de 0,5-1,5 s tras un empujón | Mismo umbral de 0,2 m para entrar y salir | `UnitFormationState.System.cs:58, 121, 149` |
| Con el héroe en marcha, la unidad en slot sigue "Moving" y no vuelve a Formed | Regla `heroMovingForSquad` mantiene el estado | `UnitFormationState.System.cs:149-162` |
| Avance a tirones, frenadas cortas | Destino a saltos de 0,25 m con `autoBraking` y `stoppingDistance` 0 | `UnitNavMesh.System.cs:306-309`; prefab del agente |
| Abanico/espiral al girar el héroe; caminos que se cruzan | Rejilla rotada 1:1 con el yaw instantáneo; un soldado a 3 m del centro necesitaría 3 m/s por cada rad/s de giro | `SquadAnchor.System.cs:88` |
| Aliados que se funden entre sí | Sin evitación, sin repulsión aliada, rejilla de 1 m con radio 0,5 sin margen | `UnitNavMesh.System.cs:118`; `UnitBodyblock.System.cs:229`; `FormationGrid.System.cs:9-10` |
| Cambio de formación con cruces y filas escalonadas | Slot i del patrón nuevo para la unidad i; cada una con su retardo | `Formation.System.cs:100-122` |
| Lanceros clavados dentro del enemigo | Parada 0,75 m < suma de radios; el empuje en contacto no actúa hasta 0,35 m | `UnitNavMesh.System.cs:71, 183`; `UnitBodyblock.System.cs:242-247` |
| Varios atacantes apilados en un punto | Cupo 2 por escuadra (contador, no geometría), abanico ±45° pseudoaleatorio, empuje de contacto casi nulo | `UnitNavMesh.System.cs:187-200`; `UnitTargeting.System.cs:136-184` (no reverificado) |
| Temblor en contacto (arqueros especialmente) | Parada exacta en `range` sin histéresis; `destination = unitPos` tras empuje re-emite `SetDestination` | `UnitNavMesh.System.cs:183, 203-208, 258-261` [hip] |
| Se pierde la formación en combate y la vuelta es individual y tardía | Correa inactiva en `Attack`; retorno por Waiting aleatorio | `UnitNavMesh.System.cs:382`; `UnitFormationState.System.cs:126` |
| Rezagados que nunca alcanzan al héroe en sprint | No hay recuperación con tope propio; `x2` solo con prisa/objetivo | `UnitFollowFormation.System.cs:107` |
| Giro de 180 grados en un frame al entrar en combate | `UnitRotationResolution` asigna la rotación sin suavizar (no reverificado) | `Squads/Systems/UnitRotationResolution.System.cs:~36`; `UnitNavMesh.System.cs:223-231` |

---

## 2. Modelo de movimiento objetivo

Principio rector: **un único punto de verdad por magnitud y un solo sistema por responsabilidad** (CLAUDE.md, modelo híbrido). El ECS decide ancla, slots y velocidad deseada; el `NavMeshAgent` solo integra; la capa GameObject no decide nada. Cada ley es una función pura (`Unity.Mathematics`, Burst) para poder probarla en EditMode.

### L-1 Movimiento del ancla (un solo sistema: `SquadAnchorSystem`)

La ancla no copia al héroe: lo sigue con velocidad y giro acotados por lo que la tropa puede ejecutar.

- Velocidad máxima de la ancla: `v_A = k_A · min_i(v_i)`, con `k_A ≈ 0,8`. Deja a cada soldado un 20 % de margen para recuperar su slot sin correr por encima de su marcha.
- Posición objetivo: `P_obj = héroe + forward_héroe · offset`.
- Llegada tipo *arrive* (Reynolds, "Steering Behaviors For Autonomous Characters", GDC 1999): `v = v_A · clamp(|P_obj − A| / r_frenado, 0, 1)`, con `r_frenado = v_A²/(2·a_A)` y `a_A` del orden de la aceleración del agente (8 m/s²).
- `A ← A + dir · v · dt`. Si el héroe corre más que `v_A`, la ancla se queda atrás: ver duda D1 (¿el héroe puede dejar atrás a su escuadra?).
- Dos velocidades ya medidas: `SquadAnchorMovingTag` (>0,1 m/s) sigue valiendo para animación y estados.

### L-2 Asignación y reasignación de slots (`FormationSystem`)

- Los slots son relativos a la ancla: `p_k = A + R(θ)·(g_k · s)`, con `g_k` en celdas enteras y `s` el espaciado derivado (§3).
- Al cambiar de formación o al caer el número de vivos, el reparto unidad→slot minimiza el recorrido total en lugar de usar el índice: asignación voraz por distancia (para `N ≤ 30` basta; no hace falta algoritmo húngaro). Coste `N²`, solo en el evento.
- Estabilidad: una unidad conserva su slot mientras no haya un evento (cambio de formación, baja, validación de malla fallida). Prohibido reasignar "por quién llega antes" en marcha: genera cruces continuos.
- Conflicto de slot (casilla ocupada o fuera de malla): se reubica en la celda libre más cercana de la misma isla (BFS de cuatro vecinos acotado, técnica estándar de relleno por inundación). Es L3-01.
- El orden de la fila decide el escalonado de salida (L-4), no el azar.

### L-3 Seguimiento del slot con recuperación y frenado (`UnitFollowFormationSystem`)

Sustituye el `x2` fijo. Cada unidad calcula una velocidad deseada con:

```
e      = slot_predicho − pos            (error de seguimiento, plano XZ)
d      = |e|
v_ff   = velocidad lineal del slot (traslación del ancla + ω × r, con ω = giro de la ancla)
v_rec  = clamp((d − h) / T_rec , 0 , v_i·k_rec − |v_ff|)       (recuperación)
v_cmd  = clamp(|v_ff| + v_rec , v_min , v_i·k_rec)
```

- `h` (holgura) = radio de llegada; `T_rec` ≈ 0,8-1,2 s (tiempo en cerrar la brecha); `k_rec` ≈ 1,0-1,25 (tope de recuperación; 1,0 respeta la marcha, más de 1,0 es "correr para alcanzar" y debe verse como tal en animación).
- Es persecución proporcional con tope, con alimentación hacia delante (*feedforward*) del movimiento del slot. Cerca del slot el término de recuperación se anula y la unidad iguala la velocidad del slot en lugar de frenar a cero.
- Frenado si va adelantada: si `e · forward_ancla < −h`, `v_cmd = v_min` (no sobrepasa).
- Predicción de destino: `slot_predicho = slot + v_ff · t_lead`, con `t_lead ≈ 0,3-0,5 s`. Con destino siempre por delante, el agente no frena contra un punto que se aleja a saltos (corrige los dientes de sierra, §1.3).
- La velocidad se escribe en `agent.speed`; la animación sigue leyendo la velocidad confirmada del agente (no cambia).
- Dato nuevo: `MovementGear` (L5-2) para que `k_rec > 1` sea una marcha explícita y no un multiplicador suelto.

### L-4 Llegada con histéresis (`UnitFormationStateSystem`)

Máquina Formed/Waiting/Moving con **dos radios** (disparador de Schmitt, histéresis estándar):

- Entrar en Formed: `d < r_in` y ancla parada durante `t_calma ≈ 0,15 s`.
- Salir de Formed: `d > r_out`, con `r_out ≈ 2,5·r_in` y siempre `r_out ≤ s/2`.
- Mientras la ancla se mueve no existe Waiting: toda unidad fuera de slot persigue (L-3). Waiting solo vale para arrancar desde reposo.
- Retardo de arranque determinista por fila: `t_i = fila_i · τ_fila + jitter(hash(unidad)) · τ_jit`, con `τ_fila ≈ 0,06-0,10 s` y `τ_jit ≈ 0,1 s`. Da la onda de arranque de una tropa disciplinada (la fila delantera sale primero) sin acordeón. `jitter` por hash de identidad: reproducible y apto para red.
- Llegada: al entrar en Formed, la unidad gira al rumbo de formación (L-5) y `ResetPath` solo una vez.

### L-5 Giro de la formación (`SquadAnchorSystem` + `GridFormationUpdateSystem`)

- La ancla gira con velocidad angular acotada: `ω_max = clamp(0,8 · v_min / R , ω_lo , ω_hi)`. Así el soldado exterior no necesita más del 80 % de su marcha para sostener el giro. Con `v_min = 3` y `R = 3,5` sale ~0,7 rad/s; `ω_lo ≈ 0,6` y `ω_hi ≈ 3 rad/s` acotan casos extremos.
- Giro grande (> `θ_vuelta` ≈ 120°): en lugar de rotar la rejilla entera (barrido de 7 m por el exterior), se invierte el orden de filas y se reasignan los slots por distancia (media vuelta). Variante a decidir: duda D5.
- La rotación de cada soldado hacia el rumbo de formación usa velocidad angular limitada (L5-4), no `slerp(dt·k)` dependiente de dt.
- Fuente: giro proporcional con tope es técnica pública de *steering*; la cota por radio es cinemática elemental (`v = ω·r`).

### L-6 Separación entre aliados

Objetivo visual: ningún aliado atraviesa a otro en marcha estable; en contacto denso se tolera una compresión visible (hasta `0,8·2rc`), nunca fusión.

- Nivel 1 (barato, fase inicial): extender el bodyblock a aliados como corrección posicional suave y mutua. `dist < 2rc + m/2` ⇒ desplazamiento hacia fuera de magnitud `(2rc − dist) · k_sep · dt`, repartido a medias entre ambos. Excluye a las dos unidades `Formed` entre sí (la rejilla ya las separa) y al héroe local.
- Nivel 2 (si el 1 no basta): evitación recíproca ORCA, van den Berg, Guy, Lin, Manocha, "Reciprocal n-body collision avoidance" (2011; implementación RVO2, Apache-2.0). Un semiplano por vecino y un programa lineal 2D sobre la velocidad preferida. Responsabilidad `ρ = 0,5` entre iguales; `1,0` hacia un obstáculo estático; `0` hacia el héroe local (que no entra en el solver). Horizonte `τ ≈ 1,5-2,5 s` (en RVO2 se ajusta a escala del problema); radio combinado `2rc`; máx. 8 vecinos.
- Alternativa a medir antes de escribir ORCA propio: activar la evitación nativa del `NavMeshAgent` en aliados (`obstacleAvoidanceType` baja o media y `avoidancePriority` por rango de fila). Coste a 900 agentes sin medir [hip]; ahora está forzada a desactivada, quizá por rendimiento (duda abierta de L5).
- Orden de ejecución: tras `UnitNavMeshSystem` y antes de `NavMeshPositionSyncSystem`, con un solo escritor del desplazamiento.

### L-7 Contacto con el enemigo: anillo y distancia de parada

**Distancia de parada derivada, no número suelto.**

```
reach_arma      = alcance útil del arma más allá del cuerpo (dato por arma)
attackRange_cc  = rcA + rcB + reach_arma                    (centro a centro, máximo para golpear)
d_parada        = rcA + rcB + k_stop · reach_arma           con 0,4 ≤ k_stop ≤ 0,7
d_reanudar      = attackRange_cc                            (si se aleja de ahí, vuelve a acercarse)
```

- Con `d_parada < attackRange_cc` queda una histéresis natural (parar antes de lo que se puede golpear) y siempre `d_parada ≥ rcA + rcB + ε`: nunca hay solape en reposo. Hoy ocurre al revés en lanceros: 0,75 m < 0,9 m.
- Arqueros: `d_parada = k_arq · range`, `k_arq ≈ 0,9-0,95`; reanudan a `range`. Evita el temblor en el límite.
- Validación en el baker: `attackRange_cc ≥ rcA + rcB + 0,2`, o aviso de edición.

**Anillo de posiciones (patrón público de "fichas de ataque" / *attack slots* en IA de multitudes).**

- Cada objetivo tiene un anillo de radio `d_parada` con `N_anillo = ⌊2π · d_parada / s_arco⌋` posiciones, `s_arco = 2rc + m`.
- Cupo duro: `cupo = min(N_anillo, cupo_diseño)`; es global por objetivo, no por escuadra.
- Cada atacante reserva la posición libre cuyo ángulo está más cerca del que ya tiene respecto al objetivo; la libera al morir, perder el objetivo o alejarse de `d_reanudar`. Si no hay hueco, va a la cola (espera a `d_reanudar + s`) o cambia de objetivo: **no** apila.
- Si ya está en su posición, el empuje de contacto no lo mueve (ancla suave). Solo se empuja a quien aún no tiene posición.
- Radio de contacto de empuje entre unidades enemigas: `2rc`, no 0,35.
- Ejemplo: `rc = 0,45`, `m = 0,2`, lanza `reach = 0,9`, `k_stop = 0,6` ⇒ `d_parada = 1,44`, `N_anillo = 8`.

### L-8 Cohesión y correa en combate

- Un combate de reacción no es una orden `Attack` del jugador: la correa debe regir cuando el origen es la reacción de combate.
- Correa derivada: `L_correa = R + 2·d_parada` (≈ 6 m para `R = 3`, `d_parada = 1,5`: coincide con el valor actual, pero ahora cambia con la formación).
- Dentro de la correa persigue; en el borde, la unidad deja de avanzar y mantiene su sitio; fuera, `v_cmd` de L-3 y el objetivo se descarta.
- Cohesión como consulta, no como física: la escuadra expone `rezagados = nº de unidades con d > r_out` y `aislados`. La IA y la ancla deciden (esperar, frenar la ancla un factor `1 − rezagados/N`).

### L-9 Vuelta a la formación

- Al soltar objetivo, la unidad vuelve con L-3 hacia el slot predicho, sin Waiting aleatorio.
- Escalonado por fila (L-4) solo si la ancla estaba parada.
- Entra en Formed al cumplir la histéresis de L-4. Reorienta con el giro limitado.
- Si hay `rezagados` y no hay enemigos, la ancla frena hasta `rezagados/N < 10 %` o `t_espera` de 2 s.

---

## 3. Tabla de parámetros

Valores de arranque en negrita donde el diseño propone cambiar. "Vive en": **blob** = componente singleton horneado desde un ScriptableObject `MovementTuningSO`; **SO** = ScriptableObject de la escuadra/arma. Ejemplo numérico con `rc = 0,45`, `m = 0,2`, `s = 1,1`.

| Parámetro | Hoy | Propuesto | De qué depende | Vive en | Efecto visible |
|---|---|---|---|---|---|
| Radio de cuerpo `rc` | no existe (agente 0,5; cápsula 0,7: `Spearmen_GO_Squeleton.prefab`) | **0,40-0,50** | Silueta del modelo | SO de unidad / `UnitBodyRadius` (L5-5) | Base de todas las distancias |
| Margen `m` | no existe | **0,15-0,25** | Gusto de densidad | blob | Aire visible entre cuerpos |
| Espaciado `s` (celda) | 1,0 (`FormationGrid.System.cs:9-10`) | `2rc + m` ≈ **1,1** | `rc`, `m` | blob (o SO de formación) | Densidad de la formación |
| Radio de llegada `r_in` | 0,2 (`Authoring.cs:10`) | **< s/4** ⇒ 0,2-0,27 | `s` | blob | Precisión al parar |
| Radio de salida `r_out` | 0,2 (mismo valor) | **2,5·r_in ≤ s/2** ⇒ 0,5-0,55 | `r_in`, `s` | blob | Fin del parpadeo |
| Tiempo de calma `t_calma` | no existe | **0,15 s** | Ruido del ancla | blob | Evita Formed prematuro |
| Reforma en Hold | 1,0 (`Authoring.cs:11`) | `r_out_hold = 2·s` ⇒ **2,2** | `s` | blob | Cuánto se desordena en Hold |
| Retardo de salida | 0,5-1,5 s aleatorio (`:39-40`); Hold 0,5-1,0 (`:37-38`) | **`fila·0,08 s + hash·0,1 s`** | Fila, identidad | blob | Onda de arranque sin acordeón |
| Cooldown de cambio de formación | 1 s (`Formation.System.cs:136`) | 0,5-1 s | Duración de la reasignación | blob | Cuánto tarda en aceptar el cambio |
| Umbral de nuevo destino | 0,25 (`Authoring.cs:25`) | **`max(0,25, v·0,05)`** + `t_lead` | Velocidad del slot | blob | Elimina dientes de sierra |
| Predicción `t_lead` | no existe | **0,3-0,5 s** | Reacción del agente | blob | Destino siempre por delante |
| Offset adelante del ancla | 2 (3 en escena de pruebas) (`Authoring.cs:34`) | **≥ R_filaDelantera + rc** | Formación | SO de formación | Distancia héroe-fila delantera |
| Velocidad de la ancla `v_A` | = héroe (sin filtro) | **0,8·min(v_i)** | Marchas de la escuadra | blob (`k_A`) | Tropa que no corre para alcanzar |
| Aceleración de la ancla `a_A` | no existe | **6-10 m/s²** | Aceleración del agente (8) | blob | Arranque y parada coherentes |
| Giro de la ancla `ω_max` | instantáneo | **`clamp(0,8·v_min/R, 0,6, 3)` rad/s** | `v_min`, `R` | blob | Giro "de rueda" sin abanico |
| Umbral media vuelta `θ_vuelta` | no existe | **120°** | Gusto (D5) | blob | Inversión de filas |
| Velocidad por unidad `v_i` | 3-5 por curva de nivel (`UnitSpeedCalculator.cs`) | igual, como marcha | Clase, nivel | SO de unidad | Ritmo base |
| Tope de recuperación `k_rec` | `x2` con prisa u objetivo (`UnitFollowFormation.System.cs:107`) | **1,0-1,25** (marcha "rápida" explícita) | Marcha | blob + `MovementGear` | Cuánto corre el rezagado |
| Tiempo de recuperación `T_rec` | no existe | **0,8-1,2 s** | Distancia típica de brecha | blob | Rapidez de reagrupar |
| Velocidad mínima `v_min` | 0 (`ResetPath`) | **0,3·v_i** | `v_i` | blob | Sin paradas secas cerca del slot |
| Aceleración del agente | 8 (prefab) | 8-10 | Peso visual | prefab | Frenada ~0,4 s |
| Giro del agente | 120°/s (prefab) | **180-360°/s** en formado, 120 en marcha | Clip de giro | prefab / blob | Pivote de pies |
| Giro hacia rumbo de formación | `slerp(dt·5)` (`UnitFollowFormation.System.cs:160-166`) | **180-360°/s limitado** | Animación | blob | Sin dependencia de dt |
| Distancia de parada melee | `0,75·attackRange` (`UnitNavMesh.System.cs:71`) | **`rcA + rcB + 0,6·reach`** | Radios, alcance | blob (`k_stop`) + SO de arma | Sin solape en reposo |
| Alcance de golpe `attackRange` | 1,0 lanza / 1,5 escudo (`Spearmen.asset:148`, `Squires.asset:155`) | **`rcA + rcB + reach`** (lanza ≈ 1,8, escudo ≈ 1,3) | Radios, arma | SO de arma (`reach`) | Alcance visible coherente con el arma |
| Parada arqueros | `range` exacto (`:183`) | **0,9-0,95·range**, reanudar a `range` | Alcance | blob (`k_arq`) | Fin del temblor |
| Radio de empuje enemigo en contacto | 0,35 (`Authoring.cs:22`) | **`2rc` ≈ 0,9** | `rc` | blob | Cuerpos que no se funden |
| Radio de empuje en marcha | 0,8 (`Authoring.cs:15`) | `2rc + m` ≈ **1,1** | `rc`, `m` | blob | Coincide con `s` |
| Fuerza de empuje | 8 / 60 pared / 3 engaging / máx. 18 m/s (`Authoring.cs:19-23`) | Reducir pared tras ORCA; resto igual | Pruebas | blob | Menos rebote |
| Separación aliada `k_sep` | no existe (dato muerto `unitRepelForce` 1) | **4-8** | `dt`, `s` | blob | Aliados sin fusión |
| Radio del anillo | no existe (abanico ±45°) | `d_parada` | Radios, alcance | derivado | Reparto del cerco |
| Posiciones del anillo | no existe | `⌊2π·d_parada / s_arco⌋` ≈ **8** | `d_parada`, `s_arco` | derivado | Máximo de atacantes cuerpo a cuerpo |
| Cupo por objetivo | 2 por escuadra (`Authoring.cs:36`) | `min(N_anillo, 3-5)` global | Diseño | blob | Cuántos pegan a la vez |
| Correa `L_correa` | 6 (`Authoring.cs:35`), inactiva en `Attack` | **`R + 2·d_parada`**, activa para reacción | `R`, `d_parada` | blob | Cuánto sale a perseguir |
| Rango de giro hacia objetivo | 3,5 (`UnitNavMesh.System.cs:74`) | **`d_parada + 2s`** | `d_parada`, `s` | blob | Cuándo empieza a mirar al enemigo |
| Giro de combate | instantáneo (`:223-231`) | **360-540°/s limitado** | Animación | blob | Sin giro en un frame |
| Radio de muestreo NavMesh | 2 (`Authoring.cs:24`) | **min(1,5·s, 1,5)** (L3-04) | `s` | SO de formación | Slot no cae a la celda vecina |
| Reintento de destino fallido | 1 m / 1 s | 1 m / 0,5-1 s | NavMesh | blob | Menos tiempo parado |
| Radio de evitación ORCA | no existe | `2rc` | `rc` | blob | Separación en marcha |
| Horizonte ORCA `τ` | no existe | **1,5-2,5 s** | Velocidad típica | blob | Esquiva temprana y suave |
| Vecinos ORCA | no existe | 8 | Coste | blob | Techo de coste a 900 agentes |
| Responsabilidad ORCA | no existe | 0,5 entre iguales; 1,0 obstáculo; 0 héroe local | Rol | blob | Quién cede |
| Histéresis de objetivo | no existe | +15-25 % a la puntuación del actual | `UnitTargeting` (L4-04) | blob | Menos cambios de blanco |

Reglas de coherencia (se validan en el baker o en un test de EditMode):
1. `s ≥ 2·rc + m`.
2. `r_in < s/4`; `r_in < r_out ≤ s/2`.
3. `d_parada ≥ rcA + rcB + ε` y `d_parada < attackRange_cc`.
4. `N_anillo ≥ cupo`.
5. `L_correa ≥ R + d_parada`.
6. `v_A ≤ 0,85·min(v_i)`; `k_rec·v_i > v_A`.
7. `offset ≥ distancia de la fila delantera al centro + rc`.

---

## 4. Cambios concretos en el proyecto

Orden: impacto visible / coste. Cada cambio respeta "un sistema, una responsabilidad": el componente nuevo lo escribe un solo sistema.

| # | Cambio | Sistemas y componentes | Encaje híbrido | Riesgo | Cómo comprobarlo |
|---|---|---|---|---|---|
| C1 | **Histéresis de llegada** (L-4): `r_in`, `r_out`, `t_calma` | `UnitFormationStateSystem` (cambia umbrales); `SquadSpawnConfig` (campos nuevos) | Solo estado; sin tocar agente | Bajo. Unidades que antes oscilaban ahora se quedan | EditMode sobre la función de transición; PlayMode, oscilaciones al llegar = 0 |
| C2 | **Retardo determinista por fila** y sin Waiting en marcha (L-4) | `UnitFormationStateSystem`; dato de fila en `UnitGridSlotComponent` (derivado de `gridPosition`) | ECS puro; hash de identidad | Bajo. Pierde algo de "aspecto orgánico": compensado por `jitter` | Test: orden de salida por fila; tiempo de arranque < 1 s |
| C3 | **Destino con predicción y umbral dinámico** (L-3) | `UnitNavMeshSystem.ResolveFormationDestination` (:296-323); lee velocidad del slot | Mismo sistema, mismo escritor de `SetDestination` | Bajo-medio. Destino fuera de malla más lejos | Contar `SetDestination`/unidad/s y velocidad media sin dientes de sierra |
| C4 | **`MovementTuningSO` → blob**: mover constantes (`StopDistanceFactor`, `EngagementRange`, `rotSpeed 5f`, `x2`, 1 s de cooldown) a datos | Authoring + baker nuevo; `UnitNavMesh`, `UnitFollowFormation`, `Formation` leen el blob | Datos, no lógica | Bajo. Hay que unificar escena `BattleTestScene` desfasada | Test de coherencia §3 en EditMode |
| C5 | **Distancia de parada derivada** y `reach` por arma (L-7); validación en el baker | `UnitNavMeshSystem:71,183`; `SquadDefinitionBaker`; SO de arma | Datos + una fórmula | Medio: cambia balance (alcance real de lanza) | `MeleeContactRegressionTests` ampliado: sin solape en reposo y golpe conecta |
| C6 | **Correa activa en combate de reacción** (L-8) | `CanPursueTarget` (:378-384) lee `SquadResolvedOrderComponent.source` | Mismo sistema; un dato más | Medio. Cambia cuánto persiguen; probar con arqueros | Escena de combate: unidades fuera de correa = 0 |
| C7 | **Giro de combate y de formación con velocidad angular limitada** (L5-4) | `UnitRotationResolutionSystem` | Un solo escritor ya existe | Bajo | Giros de 180° duran > 0,3 s |
| C8 | **Separación aliada nivel 1** (L-6) | `UnitBodyblockSystem` (quitar `continue` aliado con regla propia); `bodyblockAllySeparation` | Mismo job Burst, mismo escritor de `agent.Move` | Medio. Empuja formaciones en pasillo; excluir Formed-Formed | Solapes aliados por frame < umbral en la escena S5 |
| C9 | **Ley de seguimiento con recuperación** (L-3) y `MovementGear` | `UnitFollowFormationSystem` (:96-108); `UnitSpeedCalculator`; L5-2 | Un sistema fija `agent.speed` | Medio. Interacción con C3 y con la animación (marchas 3/5/12) | Dispersión durante la marcha; tiempo hasta formar |
| C10 | **Ancla con velocidad, aceleración y giro limitados** (L-1, L-5) | `SquadAnchorSystem` (:85-89) | Es el único escritor del ancla | Medio-alto: cambia la sensación de mando; héroe y tropa se separan | Escenas S2/S3; métricas de dispersión y de giro |
| C11 | **Anillo de contacto con reserva** (L-7) | Sistema nuevo `MeleeSlotReservationSystem` entre `UnitTargeting` y `UnitNavMesh`; buffer `MeleeSlotBuffer` en el objetivo; `UnitNavMesh` solo lee el destino | Un sistema, un componente de reserva | Medio. Casos de objetivo móvil; liberar reservas al morir | Escena S6: 0 unidades apiladas; máx. atacantes = cupo |
| C12 | **Asignación voraz por distancia y compactado** (L-2; L3-02) | `FormationSystem`; `FormationCompactSystem` nuevo | Eventos, no por frame | Bajo-medio. Cruces durante la reasignación | Escena S4: recorrido total y cruces vs. asignación por índice |
| C13 | **Validación de slots en NavMesh** (L3-01/04) | `FormationSlotResolveSystem` nuevo, `UnitResolvedSlotComponent` | Consulta una vez por cambio de ancla | Medio (NavMeshQuery) | Escena S7: dos unidades no comparten celda |
| C14 | **ORCA / evitación avanzada** (L-6 nivel 2) | `UnitAvoidanceSystem` Burst (L5-1); `UnitDesiredVelocity` | Entre `UnitNavMesh` y `NavMeshPositionSync` | Alto. Solo si C8 no basta | Comparar solapes y ms con y sin él |
| C15 | **Limpiar datos muertos**: cablear `UnitSpacingComponent` a `s`/`k_sep` o eliminarlo | `UnitSpacing.Component.cs`, authoring, builder | Quita ruido | Bajo | Grep: 0 campos sin lector |

Notas de encaje:
- C1-C4 no cambian arquitectura; son datos y umbrales. Se pueden hacer en un solo lote y medir antes de seguir.
- C8 y C14 no deben coexistir con fuerzas sin coordinar: se pasa a un solo escritor de desplazamiento (`agent.Move`/velocidad), como ya exige `tasks/lessons.md` (orden de `agent.Move` antes del sync).
- C10 rompe la identidad héroe = ancla; se justifica porque el héroe local es físico y la ancla es la referencia cinemática de la tropa. Debe quedar en el GDD.
- Ningún cambio toca la capa GameObject salvo valores de prefab (giro, aceleración). `EntityVisualSync` no cambia.

---

## 5. Plan de validación en Unity

### 5.1 Métricas medibles (se registran por frame y se agregan por escena)

| Métrica | Definición | Objetivo de partida |
|---|---|---|
| Solapes por frame (aliados) | Pares aliados con `dist < 0,9·2rc` | 0 en marcha estable; < 1 % de los pares durante giro o cruce |
| Solapes por frame (enemigos) | Pares enemigos con `dist < 0,9·2rc` durante más de 0,3 s | 0 |
| Tiempo hasta formado | Desde parada del ancla hasta 95 % de unidades dentro de `r_in` | ≤ 2,0 s |
| Oscilaciones al llegar | Transiciones Formed↔Waiting↔Moving por unidad en 3 s tras la llegada | ≤ 1 (hoy [hip]: 2-4) |
| Cambios de signo de velocidad radial | Alrededor del slot, 3 s tras llegar | ≤ 1 |
| Dispersión en marcha | Error medio y p95 a su slot durante la marcha | media ≤ 0,35·`s`; p95 ≤ 0,8·`s` |
| Dispersión en giro | Error p95 durante un giro de 90° | ≤ 1,5·`s` |
| Unidades sin objetivo en contacto | Unidades con enemigo a `< d_parada + 0,3` y `target == Null` más de 0,5 s | 0 |
| Apilado en contacto | Atacantes por objetivo con `dist < 0,8·d_parada` | ≤ `cupo` |
| Frenadas espurias | `agent.velocity` baja de 0,5·`v_cmd` sin llegar | < 5 % del tiempo |
| `SetDestination` por unidad y segundo | Contador por unidad | ≤ 4 en marcha |
| Coste de sistemas de movimiento | ms por frame con 900 agentes (Profiler / `ProfilerMarker`) | `UnitBodyblock` ≤ 1,0 ms caliente (hoy 0,9); sistemas nuevos ≤ 2 ms en total |

### 5.2 Escenas de prueba (PlayMode, paso fijo, semilla fija)

| Escena | Contenido | Qué mide |
|---|---|---|
| S1 Marcha recta | 1 escuadra de 12, héroe 20 s a velocidad constante | Dispersión, dientes de sierra, `SetDestination`/s |
| S2 Giros | Giro de 45°, 90° y 180° a distintos yaw/s | Dispersión en giro, cruces de caminos |
| S3 Arranque y parada | 10 ciclos parada-marcha-parada | Tiempo hasta formado, oscilaciones, acordeón |
| S4 Cambio de formación | Línea→Cuña→Testudo→Línea, 12 y 8 unidades | Recorrido total, cruces |
| S5 Cruce aliado | Dos escuadras aliadas en pasillo de 4 m, direcciones opuestas | Solapes aliados, bloqueos |
| S6 Choque | 12 contra 12 melé; luego 12 lanceros contra 12 escuderos | Apilado, solape en contacto, unidades sin objetivo, vuelta a formar |
| S7 Cuello de botella | Pasillo de 2 m con giro | Slots fuera de malla, relocalizados |
| S8 Estrés | 900 agentes (caso denso de `Arquitectura/11`) | ms por frame y asignaciones de GC |
| S9 Vuelta tras combate | Fin de S6 | Tiempo hasta formado, ausencia de Waiting aleatorio |

### 5.3 Pruebas automáticas

EditMode (puro, sin escena), ampliando `Tests/EditMode/FormationGeometryRegressionTests.cs`, `BodyblockRegressionTests.cs` y `MeleeContactRegressionTests.cs`:
- `ArrivalHysteresisTests`: sin cruces espurios con ruido de ±`r_in` alrededor de `r_in` y `r_out`.
- `SlotFollowSpeedTests`: `v_cmd` monótona en `d`, nunca por encima de `k_rec·v_i`, igual a `|v_ff|` con `d = 0`.
- `StopDistanceTests`: `d_parada ≥ rcA + rcB + ε` y `d_parada < attackRange_cc` para todas las escuadras horneadas.
- `RingSlotsTests`: sin dos reservas al mismo ángulo, liberación al morir, cupo respetado.
- `AnchorTurnLimitTests`: `|v_exterior| ≤ 0,8·v_min` para cualquier `R`.
- `TuningCoherenceTests`: las siete reglas de §3 sobre el `MovementTuningSO` por defecto.
- Determinismo: mismo `hash(unidad)` ⇒ mismo retardo (preparación para red).

PlayMode, ampliando `Tests/PlayMode/BattleSceneIntegrationTests.cs`:
- Un `MovementMetricsRecorder` (sistema solo en Editor/Development) vuelca CSV por escena (solapes, error de slot, estados). Cada escena afirma sus umbrales de §5.1.
- S5 y S6 con `Assert` de "0 pares solapados > 0,3 s".
- S8 con `ProfilerRecorder`; falla si los ms superan el presupuesto en una máquina de referencia (documentar CPU).

### 5.4 Protocolo antes/después

1. Grabar la línea base en las escenas S1-S9 con el código actual (confirma o descarta las hipótesis marcadas `[hip]`).
2. Aplicar C1-C4; repetir. Aplicar C5-C9; repetir. Un lote por commit para atribuir mejoras.
3. Captura de vídeo con cámara fija, 10 s por escena, para revisión visual de los fenómenos que no se miden (pies deslizando, ritmo de la onda de arranque).

---

## 6. Dudas de diseño para el usuario

1. **Aliados: ¿se bloquean o se atraviesan?** Hoy se atraviesan (a veces por accidente). Opciones: (a) bloqueo suave: nunca se funden, se rodean (C8/C14); (b) permiso de cruce solo en movimiento y no en reposo (a Formed nadie lo atraviesa); (c) atravesarse libre para un feel arcade. El diseño de C8 y C14 cambia por completo.
2. **Enemigos: ¿se atraviesan o no?** El cuerpo a cuerpo con cuerpos duros da un frente nítido y la sensación de empuje; el cuerpo a cuerpo suave permite penetrar huecos. ¿Un escuadrón enemigo puede romper una línea formada de escudos empujando? Hoy dos muros Formed no se empujan nunca.
3. **Formaciones estrictas.** ¿Testudo, Muro de Escudos y Schiltron deben mantener la forma aunque haya obstáculos (rechazar el cambio si no cabe) o degradarse a Línea? Ya preguntado en L3; aquí decide si C13 devuelve "no cabe".
4. **Disciplina rígida o melé orgánica.** Dos puntos de partida:
   - Disciplinado: retardo por fila corto, `k_rec` 1,0, `r_in` 0,2, `ω_max` baja (giro de rueda).
   - Orgánico: `jitter` mayor, `k_rec` 1,25, anillo con ángulos algo ruidosos, `m` mayor.
   ¿Debe depender de la formación (Línea rígida, Dispersa orgánica) o de la clase de unidad?
5. **Media vuelta.** Girar 180° rotando la rejilla, o invertir filas? Lo segundo es más rápido y menos "de rueda", pero las filas traseras pasan a ser delanteras de golpe.
6. **El héroe y su escuadra.** Si el héroe corre más que sus soldados, ¿la tropa se queda atrás (hoy), el héroe se frena (ancla con tope) o la tropa corre más (`k_rec > 1`)? Define L-1 y el sentido del sprint.
7. **Cupo y presión en melé.** ¿Cuántos atacantes por objetivo (2 hoy, el anillo permite 8)? ¿Se quiere que los que no caben esperen en segunda fila o cambien de objetivo?
8. **Escala y rendimiento.** ¿Es objetivo real el caso de 900 agentes (15v15 y más) o es solo prueba de estrés? Decide si ORCA tiene presupuesto o se queda en separación posicional.
9. **Radio de cuerpo.** ¿El `rc` visual (cápsula 0,7) es el correcto o se quiere más densidad (0,4-0,45)? Cambia `s`, el anillo y toda la tabla de parámetros.
10. **Escena de pruebas desfasada.** `BattleTestScene` usa `followForwardOffset` 3 y retardos 0,3-0,6: ¿se actualiza o se retira para evitar medir contra valores que no son los del juego?

---

## 7. Decisiones del usuario (2026-10-19)

Criterio general: **la sensación de referencia es la del juego estudiado**, que las notas `game-re/notes/mounts-movement.md`
§5-6, `formations-orders.md` §7.5 y `squad-ai.md` describen en estático. Se reproduce el comportamiento con código propio;
lo que allí vive en scripts o datos cifrados (distancia de contacto, reparto alrededor del objetivo) se diseña aquí.

| # | Duda | Decisión | Cambio en el modelo |
|---|---|---|---|
| D1 | Aliados | Evitación por velocidad (ORCA), sin empuje | L-6: ORCA pasa a ser el mecanismo principal (C14 sube al lote 2). Responsabilidad 0,5 entre soldados; 1,0 del soldado frente al héroe y a obstáculos (el soldado se aparta, el héroe no cede). Vecinos a ≤ 4 m, radio combinado 1,0 m, horizonte `τ` largo (~5 s) al arrancar y corto (~1 s) en ajustes. C8 (separación posicional) queda solo como paso intermedio si ORCA tarda. |
| D2 | Enemigos | Sin evitación entre enemigos por defecto; interruptor de colisión por escuadra; sin empuje ni masa | Los combatientes no se separan por ORCA: la separación en contacto la dan la distancia de parada derivada (L-7). Un flag por escuadra (`EnemyCollisionEnabled`, p. ej. para Muro de escudos) activa la evitación o el bloqueo frente a enemigos. La carga es un bono de daño por distancia proyectada en la dirección de avance; derribo y aturdimiento son estados de golpe por datos; el retroceso sigue una curva del golpe. |
| D3 | Sensación | Según la formación | Perfiles de ajuste por formación: cerradas disciplinadas (arranque por filas corto, poco jitter), Dispersa orgánica. |
| D4 | Héroe y tropa | Persecución con tope de la marcha; marcha de carrera para recuperar; el héroe no se frena | L-3: `k_rec = 1` (nunca por encima de la marcha); si `d` supera un umbral, la unidad pasa a la marcha de carrera (L5-2). Si el héroe va más rápido que esa carrera, la tropa se queda atrás. C10 no limita al héroe. |
| D5 | Giro | Rotar la rejilla entera alrededor del centro y recolocar los slots | L-5: sin `ω_max` ni media vuelta; la suavidad sale de la ley de seguimiento (L-3). C10 se reduce a la llegada del ancla (sin limitar el giro). |
| D6 | Cupo en melé | Saturación blanda | En la puntuación de objetivo (L4-04): `p' = p · (1 − n·k)` con `n` atacantes ya fijados (mínimo 1, sigue elegible), más histéresis del objetivo actual. Cupo duro solo en un modo de ataque libre. El anillo (C11) coloca a los atacantes sin cupo duro; los que no tienen hueco esperan a `d_reanudar + s`. |
| D7 | Espaciado | Por datos de formación | `s` = escala de la formación × base (en el SO de formación), con mínimo 0,3. La regla `s ≥ 2rc + m` pasa a aviso (Testudo puede comprimir); el radio de evitación va aparte. |
| D8 | Escala | 900 agentes es objetivo real | ORCA en Burst con ventana de vecinos de 4 m sobre la rejilla espacial del bodyblock; presupuesto de los sistemas nuevos ≤ 2 ms con 900 agentes (S8 obligatoria). |
| D9 | Formaciones estrictas | Mantener plantilla y reubicar localmente | C13: slot ocupado o fuera de malla → celda libre más cercana por BFS; nunca se rechaza ni degrada la orden. |
| D10 | Radio del cuerpo | Radio combinado 1 m | `rc = 0,5` para evitación y contacto; la cápsula visual de 0,7 m solo para el aspecto. |
| D11 | Escena de pruebas | Actualizar `BattleTestScene` | Se alinean sus valores con los del juego y sirve de base para S1-S9. |

Orden de lotes resultante: **lote 1** C1-C4 (sin cambios); **lote 2** C5, C6, C7 (solo giro de combate), C9 (con marcha de
carrera) y **C14 ORCA**; **lote 3** C10 (llegada del ancla), C11 (anillo sin cupo duro), C12, C13, C15 y el flag de
colisión por escuadra. C8 solo si ORCA se retrasa.

---

## 8. Línea base medida (2026-10-19, antes del lote 1)

Medida con los escenarios PlayMode explícitos `MovementBaselineS1StraightMarch` y `MovementBaselineS3StopAndGo`
(`Assets/Tests/PlayMode/MovementBaselineScenarios.cs`), en `BattleTestScene` alineada con producción. Escuadra local
de 12 unidades en FollowHero, marchando hacia −z para alejarse del héroe remoto. Grabación con
`MovementMetricsRecorderSystem` (CSV en `Logs/MovementMetrics/`) y vídeo MP4 a 30 fps constantes con Unity Recorder
(`Recordings/MovementBaseline/`, fuera de git).

| Métrica | S1 (marcha recta de 10 s) | S3 (6 ciclos de parar, marchar 2 s y parar 3 s) |
|---|---|---|
| Transiciones extra en los 3 s tras llegar (media / máx.) | 0,00 / 0 | **0,67 / 2** |
| Tiempo hasta quedar formado al 95 % (media / máx.) | 1,77 s | **1,83 s / 1,90 s** |
| Diferencia entre la primera y la última unidad en arrancar | **0,87 s** | **0,82 s** |
| Última unidad en arrancar, desde que se mueve el ancla | 1,47 s | 1,50 s |
| Error respecto al slot en marcha (media / p95) | **3,59 m / 5,18 m** | 2,65 m / 4,77 m |
| `SetDestination` por unidad y segundo | 8,5-12,1 (varía entre ejecuciones) | 2,7 |
| ms `UnitFormationState` / `UnitNavMesh` | 0,11 / 0,25 | 0,13 / 0,23 |

Lectura:
- Se confirma el acordeón al arrancar: las unidades salen repartidas en unos 0,85 s, y la última 1,5 s después del ancla.
- Se confirma el parpadeo al llegar en arranques y paradas repetidos: hasta 2 transiciones extra por unidad.
- Lo más grande es el retraso en marcha: de media, las unidades van 2,6-3,6 m por detrás de su slot. No hay ley de
  recuperación (C9, lote 2).
- `SetDestination` por segundo es ruidoso entre ejecuciones; para comparar hay que usar varias ejecuciones.

Notas del entorno:
- `BattleSceneIntegrationTests` falla en SetUp en esta máquina, por un error registrado: `HitImpactEffectManager`
  apunta a un prefab del paquete "Travis Game Assets", que está en `.gitignore` y no está importado. Solo los escenarios
  `MovementBaseline` ignoran los errores registrados.
- Con el editor manejado por MCP no hay foco en la ventana Game. Los escenarios usan una copia temporal de
  `InputSettings` para que el teclado virtual llegue al juego.

## 9. Resultado del lote 1 (C1-C4, 2026-10-20)

C1-C4 están hechos en la rama `feat/movement-batch1` (commits `bc167bfc` C4, `d14b87f0` C1, `48ce814f` C2, `47e78e2e` C3).
Mismas escenas y grabador que en §8.

| Métrica | S1 antes | S1 después | S3 antes | S3 después | Objetivo §5.1 |
|---|---|---|---|---|---|
| Transiciones extra tras llegar (media / máx.) | 0,00 / 0 | 0,00 / 0 | 0,67 / 2 | **0,00 / 0** | ≤ 1 |
| Formado al 95 % | 1,77 s | **0,67 s** | 1,83 s | **0,61 s** | ≤ 2 s |
| Arranque repartido (primera a última unidad) | 0,87 s | **0,23 s** | 0,82 s | **0,18 s** | — |
| Última unidad en arrancar | 1,47 s | **0,40 s** | 1,50 s | **0,37 s** | < 0,5 s |
| Error al slot en marcha (media / p95) | 3,59 / 5,18 m | **1,30 / 1,57 m** | 2,65 / 4,77 m | **1,09 / 1,54 m** | media ≤ 0,35·s |
| `SetDestination` por unidad y segundo en marcha | — | — | 5,4 | **4,7** | ≤ 4 |
| Proyecciones de NavMesh por unidad y segundo en marcha | — | — | 10,6 | 11,1 | sin regresión |
| ms `UnitFormationState` / `UnitNavMesh` | 0,11 / 0,25 | 0,13 / 0,21 | 0,13 / 0,23 | 0,14 / 0,22 | sin regresión |

Lectura:
- **Se acabaron el parpadeo y el acordeón.** No queda ninguna transición extra al llegar, y la tropa arranca entera en
  menos de 0,4 s, con la fila delantera primero.
- **El error en marcha baja a la mitad o menos**, pero sigue lejos del objetivo (≈ 0,35 m). Lo que falta es la ley de
  seguimiento con recuperación (C9, lote 2): hoy los rezagados no aceleran.
- **`SetDestination` en marcha queda en 4,7**, por debajo de la línea base, aunque ahora toda la tropa se mueve.
  Las proyecciones no crecen: el adelanto se suma al slot ya proyectado.
- Ajuste fuera del plan: el factor de reenvío pasó de 0,05 s a **0,25 s**. Con 0,05 s se mandaban unos 11 comandos por
  segundo en marcha. Ahora la validación exige que ese factor sea menor que el tiempo de adelanto.

Pendiente de medir: el caso de 900 agentes en escena real (S8). Las regresiones `NineHundredBodyblockAgents…` siguen en
verde, pero no cubren el coste de C3 a esa escala.

## 10. Resultado del lote 2 (C5, C6, C7, C9, C14, 2026-10-20)

Hechos en la rama `feat/movement-batch2`: `9e902f7d` (paso 0, métricas y escenarios S4 y S6), `855cb964` C7,
`f9877397` C5, `48278bda` C6, `ac5ae209` C9 y `7e32714b` C14. Mismos escenarios y grabador que en §8 y §9.
La línea base es la del paso 0, medida antes de C7.

| Métrica | Línea base | Después | Objetivo §5.1 |
|---|---|---|---|
| S1 error al slot en marcha (media) | 1,25 m | **0,57 m** | ≤ 0,5 m en este lote |
| S3 error al slot en marcha (media) | 1,02 m | **0,60 m** | ≤ 0,5 m en este lote |
| S4 error al slot en marcha (media) | 1,99 m | **1,12 m** | — |
| Formado al 95 % (S1 / S3 / S4) | 0,63 / 0,58 / 1,17 s | **0,43 / 0,51 / 0,93 s** | ≤ 2 s |
| Solapes aliados por frame en marcha (S1 / S3 / S4) | 3,8 / 3,4 / 11,5 | **0,04 / 0,13 / 0,64** | 0 |
| S6 frames de giro de combate por encima del tope | 27 | **2-5** | 0 |
| S6 pares enemigos solapados más de 0,3 s (máx.) | 16-32 | **5-11** | 0 |
| S6 distancia de contacto atacando (media / mín.) | 1,22 / 0,11 m | 1,25-1,55 / 0,12-1,23 m | ≥ 2·rc |
| S6 unidades en reacción fuera de la correa | 0-20 | 0 (S6 antiguo), 1-6 (S6 nuevo) | 0 |
| Coste S6 (`UnitFormationState` + `UnitNavMesh` + `UnitFollowFormation` + `UnitAvoidance`) | — | **1,16 ms** | ≤ 2 ms |
| Benchmark ORCA, 900 agentes (mediana) | — | **0,65 ms** | ≤ 1 ms |

S6 cambia de un run a otro; las cifras con rango son de dos o más corridas. Desde C9, S6 marcha hasta el primer golpe
en lugar de parar al empezar la reacción: con la correa de C6 la escuadra ya no persigue al enemigo desde la distancia
de reacción (8-9 m), y el escenario antiguo dejaba de llegar al cuerpo a cuerpo.

Lectura:
- **Los aliados ya no se atraviesan en marcha.** ORCA baja los solapes entre 30 y 100 veces sin empujes.
- **El error en marcha baja a la mitad**, pero queda en 0,6 m, por encima del 0,5 m del lote. La causa es D4: el tope de la
  marcha Walk es la velocidad del héroe, así que el retraso que se acumula al arrancar solo se recupera en Run. La tropa
  corre una vez por arranque (de 0,9 m a 0,45 m del slot) y luego camina con ese pequeño retraso.
- **El giro de combate respeta el tope** (C7) y **la parada deja los cuerpos separados** casi siempre (C5). Los solapes
  enemigos persistentes bajan, pero no llegan a 0: sigue habiendo empujes en la melé que la parada no controla.
- **S4 tarda 0,6 s más en formar que solo con C9** (0,30 → 0,93 s). Sigue dentro del objetivo de 2 s, pero supera el
  margen de +0,3 s que pedía el plan para C14.

Ajustes fuera de las decisiones por defecto del plan:
- **Marchas (C9):** se pedía Run por encima de 1,5 m y Walk por debajo de 0,6 m, con 1 s de recuperación. Con esos valores
  el retraso de arranque (≈ 1 m) caía dentro de la banda y la tropa nunca corría: error de 1,03 m. Quedan en
  **0,9 m / 0,45 m y 0,5 s**. Con el slot quieto (< 0,5 m/s), la unidad llega a la velocidad de la marcha, como antes; con
  solo la ley de recuperación, la llegada pasaba de 0,6 a 1 s.
- **Radio entre compañeros de escuadra (C14):** D1 fija un radio combinado de 1 m, pero el espaciado de la rejilla también
  es 1 m. Con ese radio, cada slot queda en contacto con sus vecinos y una fila formada es un muro que ORCA no sabe rodear
  (S4 no llegaba a formar). Dentro de la escuadra solo se esquivan las unidades que se mueven, con radio combinado de
  **0,9 m** (0,9 del espaciado). Frente a otras escuadras y a los héroes se mantiene 1 m.
- **Separación de solapes en ORCA:** se usa un tiempo de resolución de 0,25 s (`avoidanceCollisionResolveTime`) en lugar del
  paso de simulación del paper, para no meter tirones de varios m/s cuando dos cuerpos ya se tocan.

Pendiente: S8 (900 agentes en escena real, necesita el paquete de VFX ausente), el error en marcha ≤ 0,5 m, los solapes
enemigos persistentes en S6 y el coste de llegada en S4.

## 11. Formaciones por datos y reparación del movimiento (2026-10-20)

Rama `feat/formation-layout` (desde `feat/movement-batch2`): `9c78b418` (modelo y disposición), `0130321c` (proyección del
slot, tests y métrica), `e0aa54a1` (aceleración, frenado y reparto por cercanía) y `3c91f408` (posturas). Plan en
`tasks/todo.md`; lo que queda fuera, en `Docs/Por_Abordar/`.

### Modelo nuevo (patrón de la referencia, `game-re/notes/formations-orders.md` y `formation-postures.md`)
- `FormationDefinition` sustituye a la rejilla de celdas de 1 m: disposición Template o Scatter, **espaciado propio** (suelo
  0,3 m), ruido orgánico determinista por escuadra y ranura, **plantillas por soldados vivos**, y por ranura fila, **papel**
  (`Front`, `Flank`, `Interior`, `Rear`) y **rumbo** local. Tabla de **posturas** por papel.
- `FormationSystem` es el único escritor de la disposición y de los slots. Al rehacerla (cambio de formación, de plantilla
  por bajas o de espaciado) cada unidad toma la **ranura libre más cercana** (L-2); si no, conserva la suya.
- `GridFormationUpdateSystem` solo gira el desplazamiento local con el ancla; el slot se proyecta en la malla con
  `min(espaciado, 0,65)` antes del radio ancho (L3-04).
- La fila deja de salir invertida: `BraceWeaponSystem` y la animación recibían `gridPosition.y`, con el frente en la y más alta.

### Medida (mismos escenarios; la métrica de solapes entre compañeros pasa a ser relativa a su espaciado)
| Métrica | Cierre del lote 2 (§10) | Después |
|---|---|---|
| Aceleración > 20 m/s² (S1 / S3 / S4) | 0,7 / 3,3 / 6,7 % (máx. 197 m/s²) | **0,2 / 0,6 / 0,7 %** (máx. 37 m/s²) |
| Atascos al cambiar de formación (S4, > 0,5 s parado lejos del slot) | 6-7 | **0** |
| Formado al 95 % (S1 / S3 / S4) | 0,43 / 0,51 / 0,93 s | 0,43 / 0,83 / 1,97-2,43 s |
| Error al slot en marcha (S1 / S3 / S4) | 0,57 / 0,60 / 1,12 m | 0,59 / 0,76 / 0,99 m |
| Solapes aliados en marcha (S1 / S3 / S4) | 0,04 / 0,13 / 0,64 | 0,00 / 0,00 / 0,78 |
| S6 aceleración > 20 m/s² | — | 2,0 % (máx. 181 m/s²) |

Lectura:
- **Se acabaron los tirones** fuera de la melé. Venían de ORCA, que escribía `agent.velocity` saltándose la aceleración del
  agente, y de C9, que bajaba `agent.speed` de golpe. Ahora la velocidad cambia como mucho a la aceleración del agente y
  frena hasta `brakingFactor` (2,5) veces más rápido, como una persona.
- **La tropa ya no se atasca al cambiar de formación.** Con "ranura k = soldado k", las plantillas de Línea y Testudo no se
  corresponden en el espacio y media tropa atravesaba a la otra; el reparto por cercanía lo resuelve.
- **La llegada tarda más en S3 y S4** que al cierre del lote 2: el frenado ya no es instantáneo (antes la unidad paraba en un
  frame, que era el tirón) y el espaciado de Línea pasa de 1,0 a 1,1 m. S4 forma en unos 2 s, en el límite de §5.1.
- **En la melé (S6) quedan picos** que vienen de los empujes del bodyblock entre enemigos (M12 en `Docs/Por_Abordar`).

### Desviaciones respecto al plan
- Los assets se migraron **en el sitio** (mismo script con otro nombre, mismo GUID) en lugar de crear assets nuevos: no hubo
  que repuntar escuadras, interfaz ni prefabs.
- Scatter usa el espaciado como tamaño de celda y el ruido como desplazamiento, sin campos propios.
- Los radios de llegada se validan contra el espaciado de referencia (1 m); la Testudo (0,8 m) aprieta a propósito.
- El reparto por cercanía (C12) entró aquí: sin él, S4 se atascaba.
- La defensa por postura no usa `ShieldOrientation`: ese campo no lo lee nadie y el bloqueo es el collider del escudo, que
  cuelga de la mano. La defensa saldrá de los clips de postura, que no hay en el proyecto (`Docs/Mechanics/FormationPostures.md`).

## 12. Frenado de llegada: picos en la melé y llegada al slot (M12, M13, 2026-10-21)

### Causa (medida, no supuesta)
El §11 atribuía los picos de S6 al bodyblock. Al clasificar los picos del CSV y registrar en el juego cada caída brusca de
`agent.velocity`, el origen resultó ser otro: **el propio `NavMeshAgent` parando en un frame** al llegar a un destino quieto
yendo deprisa.
- Melé: al entrar en la distancia de parada (1,6 m) a 3,5-4,5 m/s, `UnitNavMeshSystem` mandaba "quédate donde estás"
  (destino = posición actual) y el agente pasaba de 4 m/s a 0. Eran 185 de 195 paradas bruscas registradas.
- Slot: cuando el héroe para, el slot queda a 0,2-0,5 m de la unidad que aún va a 3 m/s, y llega en uno o dos frames.

Se probó también la hipótesis del §11 (empuje del bodyblock como velocidad con aceleración limitada): no mejoró los picos,
subió los solapes y hacía oscilar el empuje (es un muelle; con retardo se pasa y sigue empujando). Se descartó.

### Cambio
- **Tope de llegada** (`UnitFollowFormationSystem`, `FormationFollowSpeed.ArrivalSpeed`): si el destino está quieto (slot
  más lento que 0,5 m/s, o cualquier destino de combate), la velocidad no pasa de √(2·a·d), con d la distancia al último
  destino mandado y a = aceleración × `brakingFactor` × `arrivalDecelerationFraction` (0,6 → 12 m/s²). La unidad frena por el
  camino y llega casi parada.
- **Punto de parada en la melé** (`UnitNavMeshSystem`, `MeleeReach.BrakingStopPoint`): al entrar en alcance, el destino es
  el punto donde termina de frenar (v²/2a por delante), sin acercarse al objetivo más de dos radios de cuerpo + holgura.

### Medida (S6: 3 corridas base y 6 con el cambio; S1-S4: 2 corridas)
| Métrica | Antes | Después |
|---|---|---|
| S6 aceleración > 20 m/s² | 1,9-3,5 % (media 2,6), máx. 198-282 | 1,0-2,4 % (media 1,5), máx. 110-144 |
| S6 pares enemigos solapados > 0,3 s | 8 / 11 / 15 | 8-13 (media 10,7) |
| Formado al 95 % S3 (media / peor parada) | 0,83 / 1,10 s | 0,75 / 1,43 s |
| Formado al 95 % S4 | 1,97-2,43 s | **1,03-1,07 s** |
| S1 / S3 / S4 aceleración > 20 m/s² | 0,1-0,2 / 0,6 / 0,7 % | 0,1 / 0,7 / 0,5-0,7 % |

Lectura:
- Los picos grandes de la melé bajan a la mitad y los solapes no empeoran (S6 varía mucho entre corridas, M10).
- S4 forma en la mitad de tiempo. En S3 mejora la media, pero la primera parada (unidades aún corriendo para recuperar)
  tarda más porque ahora frena en el último metro y medio. Con 0,8 en lugar de 0,6 S3 y S4 empeoraban (más rodeo).
- Quedan picos en la melé: unidades que entran en alcance ya pegadas al objetivo (sin sitio para frenar) y los empujes del
  bodyblock. Siguen en `Docs/Por_Abordar/Movimiento.md` (M12).

## 13. Anillo de contacto (C11, M6) y escenarios reproducibles (M10, 2026-10-21)

### Reproducibilidad (M10)
Los escenarios fijaban 30 fps solo mientras grababan; la carga y la preparación corrían al ritmo real del editor. Ahora el
paso es fijo desde el `SetUp` y `Random` se vuelve a sembrar antes del escenario: tres corridas de S6 son idénticas frame a
frame durante toda la aproximación. Divergen desde los primeros contactos de la melé (hipótesis: disparadores de PhysX sin
*Enhanced Determinism*). Cada corrida guarda su resumen en JSON y *Tools > Movement > Median Of Last 3 Runs* da la mediana.

### Anillo (L-7, D6)
- `MeleeContactRingSystem` (entre `UnitTargetingSystem` y `UnitNavMeshSystem`, único escritor de
  `MeleeContactSlotComponent`): cada objetivo de melé tiene un anillo de radio = la menor distancia de parada de sus
  atacantes, con ⌊2π·r / (2·rc + m)⌋ posiciones en ángulos fijos del mundo (8 para los lanceros, `m` =
  `contactRingMargin` 0,2 m).
- Cada atacante reserva la posición libre más cercana al lado del que viene y la conserva mientras su objetivo no cambie; los
  más cercanos eligen primero. Solo dentro de ±`contactRingMaxArcDegrees` (60°) de su lado: las posiciones de detrás del
  objetivo caen entre sus compañeros, y la primera versión (sin ese límite) subió los solapes. Sin hueco, espera a
  `d_reanudar + s_arco` (sin cupo duro: el cupo sigue en el targeting).
- Se reconstruye cada frame desde los atacantes vivos: muerte o cambio de objetivo liberan la posición sin eventos.
- `UnitNavMeshSystem` va a la posición reservada en lugar del abanico de ±45° por índice áureo (que queda como respaldo para
  unidades sin el componente).

### Medida (S6, 3 corridas por variante, mediana; paso fijo)
| Métrica | Sin anillo | Anillo completo | Anillo ±60° (queda) |
|---|---|---|---|
| Pares enemigos solapados > 0,3 s | 9 (9-10) | 13 (9-15) | 10 (9-21) |
| Aceleración > 20 m/s² | 2,0 % | 1,4 % | 2,0 % |
| Máx. aceleración | 206 m/s² | 150 m/s² | 172 m/s² |
| Máx. atacantes por objetivo | 5 | 3 | 4 |

Lectura: dentro del ruido de la melé no hay mejora ni empeoramiento claros. En S6 (una escuadra por bando, con el cupo de 2
por escuadra del targeting) el anillo casi nunca se llena, así que su efecto es pequeño. Se mantiene porque sustituye un
reparto implícito por posiciones explícitas y probadas; su prueba de verdad es una melé con varias escuadras contra una.

## 14. Llegada del ancla (C10, M5, 2026-10-21)

### Cambio
El ancla deja de copiar al héroe (L-1, reducido por D5 a la posición). `SquadAnchorSystem` sigue calculando el punto objetivo
(héroe + desplazamiento, punto de mantener posición, de retirada o de tiro), pero el ancla viaja hasta él con
`AnchorArrival`:
- Acelera como mucho a `anchorAcceleration` (8 m/s²).
- Frena con antelación: la velocidad no pasa de √(2·a·d), así que se detiene en el punto sin pasarse. Para frenar admite
  hasta `brakingFactor` × a.
- Más allá de `anchorSnapDistance` (10 m: aparición, reaparición) salta.
- La rotación sigue directa (D5). En marcha el ancla va v²/(2a) por detrás del punto: unos 0,56 m a 3 m/s.
- Con `anchorAcceleration` = 0 vuelve el comportamiento anterior (ancla = héroe), para comparar.

### Medida (paso fijo; S1-S4 son reproducibles con una corrida; S6 dos corridas válidas)
| Métrica | Sin C10 | Con C10 |
|---|---|---|
| Formado al 95 % S1 | 0,43 s | **0,27 s** |
| Formado al 95 % S3 (media / peor parada) | 0,93 / 2,27 s | **0,81 / 1,70 s** |
| Formado al 95 % S4 | 1,37 s | **0,60 s** |
| Error al slot en marcha S1 / S3 / S4 | 0,69 / 0,88 / 1,15 m | **0,61 / 0,71 / 1,04 m** |
| Aceleración > 20 m/s² S1 / S3 / S4 | 0,1 / 0,8 / 0,7 % | 0,2 / 0,4 / 0,6 % |
| S6 aceleración > 20 m/s² | 1,3-2,9 % | 0,7-0,9 % |
| S6 pares enemigos solapados > 0,3 s | 9-21 | 14-18 |

Lectura:
- Mejoran todas las llegadas, también la primera parada de S3 (la que empeoró en §12): la tropa frena con el ancla en vez de
  encontrarse el slot parado de golpe. S4 forma en menos de la mitad de tiempo.
- El error al slot en marcha baja, pero S1 sigue en 0,61 m frente al objetivo de 0,5 m (M1).
- Coste para el jugador: al arrancar y al parar, la escuadra se separa del héroe hasta medio metro más. Queda anotado en el
  GDD (§4.3).
- Una corrida de S6 falló por el arnés ("Scripted input did not move the hero", ya visto antes); no cuenta en la tabla.
