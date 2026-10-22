# 🎮 Funcionalidades del Juego

Actualizado: 2026-10-22. Inventario por **presencia de código** en `Assets/Scripts` y de assets en `Assets/`; no es el resultado de una prueba jugable completa. Para el estado detallado de cada mecánica, ver los documentos enlazados. Leyenda: ✅ implementado en código, 🔄 parcial o sin validar de punta a punta, 📅 planificado (sin código), 💡 idea.

---

## 1. Implementado

### 🧙‍♂️ Control del héroe
- ✅ Movimiento en tercera persona (WASD) con intención ECS y motor físico local (`HeroMovementSystem` → `LocalHeroCharacterMotor`). Sin salto. Ver `ModeloHybrido.md`.
- ✅ Input con Unity Input System; stamina (`HeroStaminaSystem`) y sprint.
- ✅ Habilidades por fases: ataque básico (clic), Q/E/R, definitiva (F), perks activos (1/2), enfriamientos y carga. Ver `Mechanics/SkillsAndPerks.md`.
- ✅ Perks pasivos (auras y efectos al golpear) sobre la pila de modificadores de stats.
- ✅ Cuatro clases de héroe con definición (`Resources/Data/HeroClasses`): SwordAndShield, TwoHandedSword, Spear, Bow.
- ✅ Muerte, retirada del escuadrón y respawn del héroe local. Ver `Mechanics/HeroDeath.md`.

### 🛡️ Control de escuadras
- ✅ Órdenes C (seguir), X (mantener posición), V (atacar); doble C alterna «ir deprisa».
- ✅ Formaciones por F1–F4 y ciclo con doble X; seis tipos (Line, Dispersed, Testudo, Wedge, Column, Square) definidos por datos. Ver `Mechanics/Formations.md`.
- ✅ Posturas por papel de ranura y hitos de formación. Ver `Mechanics/FormationPostures.md`, `Mechanics/FormationMilestoneSystem.md`.
- ✅ Marcadores de destino en mantener posición (`DestinationMarkerSystem`).
- ✅ Cambio de escuadra en puntos de suministro. Ver `Mechanics/SquadSwap.md`.
- ✅ Cuatro tipos de escuadra: Squires, Archers, Pikemen, Spearmen (`SquadType`); liderazgo (`HeroLeadershipCalculator`).

### 🧠 Movimiento y combate de unidades
- ✅ Pipeline de movimiento con ancla, ranuras, histéresis de llegada, marcha/carrera, evitación ORCA y empuje entre enemigos. Ver `TroopMovementPipeline.md`, `Design/Movimiento_Formaciones.md`, `Mechanics/BodyblockSystem.md`.
- ✅ Detección, targeting y ataque cuerpo a cuerpo y a distancia (proyectiles); anillo de contacto. Ver `Mechanics/UnitEngageSystem.md`.
- ✅ Daño por tipo (Blunt, Slashing, Piercing) con penetración, defensa, escudo, crítico y pila de stats. Ver `Mechanics/DamageMechanics.md`.
- ✅ IA de héroes remotos (Rusher, Balanced) y de escuadras. Ver `RemoteHeroAI_Architecture.md`.

### 🏗️ Mapas y partida
- ✅ Puntos de captura en secuencia, captura final, reloj de batalla con extensión por captura. Ver `Mechanics/CapturePoints.md`, `Mechanics/MatchTimer.md`.
- ✅ Puntos de suministro con interacción.

### 🧬 Progresión y datos
- ✅ Nivel del héroe y progresión de escuadras (`HeroLevelSystem`, `SquadProgressionSystem`).
- ✅ Guardado local con JSON (`SaveSystem`/`LoadSystem`); inventario, equipo y tienda con transacciones. Ver `Arquitectura/1_Arquitectura_Actual.md`.

### 🎨 Interfaz y escenas
- ✅ HUD de batalla, minimapa (`MinimapController`), chat (`Chat.System`), preparación de batalla, panel de héroe e inventario.
- ✅ Escenas: Login, CharacterSelection, AvatarCreator, Feudo, Barracón, BattlePrep, Battle, PostBattle, Settlement, WorldMapCampaign.

---

## 2. Parcial o sin validar de punta a punta

- 🔄 Matchmaking: simulado, genera batallas locales de prueba.
- 🔄 Multijugador: no hay contrato de red ni Netcode en el proyecto; la autoridad remota actual es IA local. Ver `Arquitectura/1_Arquitectura_Actual.md` §13 y `Coordinacion/`.
- 🔄 Entrega autenticada de resultados de batalla (los eventos de XP son locales).
- 🔄 Flujo de partida completo con todos los sistemas visuales y de UI (sin validación automatizada; ver `Mechanics/HeroDeath.md`, «Límites actuales»).
- 🔄 Cámara espectador y HUD de enfriamiento de respawn.

---

## 3. Planificado (sin código)

- 📅 Posicionamiento manual de la formación con X mantenida + mouse (los sistemas propuestos `ManualPositioningSystem`, `FormationMarkerSystem` y `TerrainRaycastSystem` no existen).
- 📅 Elementos destructibles (no hay código de destructibles).
- 📅 Servidor dedicado e interpolación de movimiento remoto.
- 📅 Modos de juego adicionales: dominio territorial, escolta.

---

## 4. Ideas

- 💡 Modo táctico con cámara elevada, órdenes contextuales con clic derecho, menú radial.
- 💡 Flanqueo automático y coordinación entre escuadras.
- 💡 Replay, espectador avanzado, personalización de HUD, presets de formaciones.
- 💡 Maquinaria de asedio, clima dinámico, efectos de terreno, moral de tropas, eventos dinámicos.
- 💡 Ranked, temporadas, torneos, clanes, estadísticas avanzadas.

---

## Notas

- El sistema de salto del héroe fue eliminado del código y del diseño.
- Los porcentajes de avance y la lista de «próximas prioridades» de versiones anteriores se retiraron: no se podían verificar contra el código. Las prioridades vigentes están en `tasks/todo.md` y en `Por_Abordar/`.
- La lógica antigua «unidad más cercana al héroe» para Formed↔Moving ya no existe: ahora decide el error de cada unidad respecto a su ranura (`TroopMovementPipeline.md` §7).
