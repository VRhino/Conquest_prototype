# Plan: Fase 2 de `Docs/Design/Aprendizajes_Referencia.md` — perks y lanzador de habilidades

Estado: en curso (2026-10-21), rama `feat/reference-phase0` (sigue sobre las Fases 0 y 1, sin subir).
Petición del usuario: "ve con la fase 2". Plan anterior archivado en `tasks/reference-phase1-plan.md`.
Referencia (solo patrones): `game-re/notes/skill-lifecycle.md` (puerta, fases, `interrupt`/`yield`, carga),
`preconditions.md` (condiciones de efectos), `effects-catalog.md` (efectos como modificadores), `mechanics.md` §7e.

## Decisiones del usuario (2026-10-21)
- **Teclas:** como el GDD. Q/E/R habilidades de clase, **F definitiva**; interactuar pasa a **G**.
- **Alcance:** infraestructura completa (puerta, enfriamientos por grupo, fases, interrupción, carga) **más efectos
  genéricos** reutilizables (golpe en área o cono, buff temporal a héroe o escuadra, embestida, escudo absorbente) para
  que las 16 habilidades de clase hagan algo ya, con la animación de ataque como provisional.
- **Perks como la referencia:** pasivos = modificadores de la pila con condiciones simples; activos = buff temporal con
  enfriamiento lanzado por la misma puerta. Se eligen 5 + 2 en el loadout existente; el árbol de talentos y su UI quedan
  para después.
- **Ataque básico con combo:** cadena de 3 golpes con ventana de encadenado y memoria de entrada (~0,2 s); el tercero
  cierra la cadena.

## Diseño
- **Habilidad = lista de fases** (ref.): cada fase con duración (negativa = sin fin), flags interrumpible / final / carga
  y eventos con instante (abrir/cerrar golpe, aplicar efecto, disparar, disparo de animación). Datos en el SO
  `HeroAbility` (ampliado) y convertidos a blob al inicializar al héroe; el ataque básico es también una habilidad.
- **Instancia actual + aparcada** (`SkillInstanceComponent`, `ParkedSkillInstanceComponent`): `interrupt` sustituye,
  `yield` aparca y restaura; la puerta solo deja pasar si la fase actual es interrumpible.
- **Puerta única** `SkillGate.Evaluate` (pura, motivo de rechazo en un enum, orden fijo): vivo y en batalla →
  control (inmovilizado) → fase actual interrumpible → cadena → enfriamiento → aguante. Jugador e IA piden con
  `SkillRequestComponent` (enableable); un solo sistema arranca la habilidad.
- **Enfriamiento por grupo y tiempo absoluto** (L2-5): `DynamicBuffer<CooldownGroupElement>{groupId, endTime}`; grupo 0 =
  sin enfriamiento; la duración se escala con `StatId.CooldownScale` de la pila (con tope). Sustituye a
  `CooldownSystem`, que itera cada frame.
- **Efectos genéricos** como eventos de fase: daño en círculo/cono (por `PendingDamageEvent`), modificador temporal a
  héroe / escuadra / aliados en radio (por la pila, fuente `Effect`), embestida (desplazamiento del héroe) y escudo
  absorbente (L1-06).
- **Perks** (L7-4): el SO `HeroPerk` lleva sus modificadores `{stat, op, valor, objetivo, condición}`; un sistema aplica los
  pasivos con fuente `Perk` y activa/desactiva los condicionados; los activos son habilidades de un solo evento
  (aplicar buff) con su enfriamiento.

## Pasos (un commit cada uno; EditMode verde y regresiones de PlayMode antes de cada commit)
- [x] **1. Habilidades del héroe en batalla.** Las cuatro habilidades salen del SO de clase (`HeroClassManager`), no de la
  entidad de clase horneada (vacía, P2 de `Docs/Por_Abordar/Pila_Modificadores.md`). Blob de habilidad con fases y
  eventos; el SO `HeroAbility` gana fases, grupo de enfriamiento y efectos (con valores por defecto equivalentes).
- [ ] **2. Enfriamientos por grupo (L2-5).** Buffer de grupos, `CooldownUtility` y `StatId.CooldownScale`; fuera
  `CooldownComponent`/`CooldownSystem` (y el HUD lee los grupos).
- [ ] **3. Puerta y petición (L2-1).** `SkillGate` con motivos, `SkillRequestComponent`, teclas Q/E/R/F (interactuar a G) e IA
  por la misma petición.
- [ ] **4. Instancia por fases (L2-2).** `SkillInstanceComponent` y el sistema de fases; el ataque básico pasa a ser una
  habilidad de fases y desaparece el trío fijo de ventanas de `HeroCombatComponent` y la lógica duplicada jugador/IA.
- [ ] **5. Interrupción (L2-4).** `interrupt`/`yield`, aparcada, fases no interrumpibles; fin al morir, al acabar la batalla y
  al cambiar de escuadra.
- [ ] **6. Efectos genéricos.** Daño en área/cono, modificador temporal, embestida y escudo absorbente; datos para las 16
  habilidades de clase según el GDD.
- [ ] **7. Combo del ataque básico (L2-3).** Cadena de 3 con ventana de encadenado y memoria de entrada.
- [ ] **8. Carga (L2-6).** Fase sin fin + soltar avanza a la fase final.
- [ ] **9. Multiproyectil y tope (L2-7).** Esquema de lanzador (modo, cuenta, ángulo) y tope de proyectiles vivos que descarta
  el más antiguo.
- [ ] **10. Perks pasivos (L7-4).** Modificadores en el SO, carga del loadout (5 pasivos) y condiciones simples.
- [ ] **11. Perks activos.** 2 huecos con tecla propia, por la puerta y con su enfriamiento.
- [ ] **12. Documentación.** Mecánica de habilidades y perks, `SystemResponsibilities.md`, Por_Abordar y lecciones.

## Verificación
EditMode completo en cada paso; PlayMode S1/S6 cuando el paso toca combate o movimiento; en `BattleTestScene` (Play) que
Q/E/R/F lanzan, respetan aguante y enfriamiento, y que el combo de 3 encadena; el héroe IA usa la misma puerta.
