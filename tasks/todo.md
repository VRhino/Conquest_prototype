# Plan: bloque 3 de la referencia — L2-9, L2-15, H5, H1 + L4-08

Estado: en curso (2026-10-27), rama `main` local, sin subir. Plan anterior archivado en `tasks/l4-04-targeting-plan.md`.
Fuentes: `Docs/Design/Aprendizajes_Referencia.md` (L2-9, L2-15, L4-08), `Docs/Por_Abordar/Habilidades_Perks.md` (H1, H5),
`Docs/Mechanics/SkillsAndPerks.md`, GDD §4.6, §6.2, §12.3. Referencia (solo patrones, ningún código ni dato): las escuadras
lanzan con el mismo ciclo de vida que el héroe y enfrían por escuadra; las condiciones se combinan con Y, las listas son
"cualquiera de" y los triestados distinguen "no importa", "debe" y "no debe"; la IA de apoyo puntúa aliados por cercanía,
vida perdida y si es héroe, y mantiene el elegido mientras siga valiendo.

## Problema
- **L2-9.** Hay datos de habilidades de escuadra (`AbilityData`, `AbilityByLevelElement`, `SquadAbilityDefinitionComponent`,
  `UnlockedAbilityElement`, `abilityUnlockLevel`), pero `AbilityData` solo tiene nombre y enfriamiento, ninguna escuadra
  lleva habilidades (`abilitiesByLevel: []`) y nada las lanza.
- **L2-15.** `PerkCondition` solo mira el movimiento del héroe.
- **H5.** Nada rellena `perkIDs` del loadout: ningún héroe lleva perks a batalla.
- **H1 / L4-08.** La IA solo pide el ataque básico y no elige aliados.

## Diseño
### L2-9 — habilidades de escuadra por el mismo lanzador
- **Huecos** `SkillSlot.Squad1..Squad3` (una por desbloqueo, en el orden de `abilitiesByLevel`). `heldSlots` pasa de `byte`
  a `ushort` (ya no caben en 8 bits).
- **Datos:** `AbilityData` gana las mismas fases, eventos, `refresh` y grupo que `HeroAbility`.
  `SkillBlobBuilder.BuildSquadAbility` construye el blob; el baker lo guarda en `SquadAbilityDefinitionComponent.skill`.
- **Lanzador:** la escuadra es un lanzador más: `SkillRequestComponent`, `SkillInstanceComponent`, `HeroSkillElement` y
  `CooldownGroupElement` propios (enfriamiento por escuadra). Los pone `SquadSpawningSystem`; `UnitStatScalingSystem`, que
  ya lleva los desbloqueos, añade la habilidad al buffer al desbloquearla.
- `SkillExecutionSystem` recorre también las escuadras: misma `SkillGate` (viva = le queda una unidad viva; sin aguante),
  mismo `SkillTimeline`, mismo `CooldownUtility`. Los eventos de efecto salen del centroide de sus unidades vivas, mirando
  hacia donde mira su ancla, con su equipo. Sin aparcar (una escuadra no tiene ataque que proteger).
- `SkillEffectSystem`: para una escuadra, `Self`/`OwnSquad` = sus unidades, el daño en área usa su perfil de daño y no hay
  embestida.
- **Entrada:** teclas **3, 4 y 5** (el GDD §12.3 pide 1/2/3, pero la Fase 2 dio 1 y 2 a los perks activos; queda como duda
  para el usuario). `HeroSkillRequestSystem` dirige esas teclas a la escuadra activa del héroe.
- **Datos de juego:** una o dos habilidades por escuadra (Escuderos, Arqueros, Lanceros) con efectos genéricos (GDD §4.6:
  ofensiva, defensiva, táctica).

### L2-15 — condiciones ricas
- `PerkEffect` gana, combinadas con Y (valor por defecto = "no importa", así los assets actuales no cambian):
  - `chance` (0 = siempre), tirada sembrada con `CombatRandom`;
  - `targetKind` (cualquiera / héroe / soldado) y `targetSquadTypes` (máscara "cualquiera de");
  - `targetHasEffect` y `selfHasEffect`, triestados sobre "tiene un modificador temporal activo en `effectStat`";
  - `squadAliveMin` / `squadAliveMax`: fracción de soldados vivos de su escuadra.
- Lógica pura `PerkConditions.Met(effect, context)`; los sistemas reúnen el contexto. Tests EditMode de cada condición y
  de la combinación.

### H5 — panel de perks en el feudo
- uGUI (lo que usa el proyecto; skill `unity:ui`). Lista de perks de la clase desbloqueados, con contador 5 pasivos + 2
  activos (`PerkLoadout`), que escribe `perkIDs` del loadout activo y guarda. Si hace falta tocar `FeudoScene` (modificada
  por el usuario), preguntar antes.

### H1 + L4-08 — la IA usa habilidades y elige aliado
- `HeroAIDecision` gana el hueco a lanzar. Reglas sencillas y puras (EditMode): enemigos en el radio del efecto para las
  ofensivas, aliados heridos para las de apoyo, enfriamiento listo; el motivo de `SkillGate` evita insistir.
- L4-08: puntuación de aliado (cercanía, vida perdida, héroe) dentro de un radio, con histéresis sobre el elegido.

## Pasos (un commit cada uno; EditMode en verde antes de cada commit)
- [ ] 1. L2-9 datos: huecos, `AbilityData` con fases y eventos, blob horneado. EditMode.
- [ ] 2. L2-9 lanzador: componentes de escuadra, desbloqueo, ejecución, efectos, teclas. EditMode.
- [ ] 3. L2-9 datos de juego: habilidades de Escuderos, Arqueros y Lanceros. Comprobar en Play que se lanzan al nivel.
- [ ] 4. L2-15: condiciones nuevas con tests.
- [ ] 5. H5: panel de perks.
- [ ] 6. H1 + L4-08 (o documentarlos si no da tiempo).
- [ ] 7. Docs: `SkillsAndPerks.md`, `Docs/Por_Abordar/` (README y Habilidades_Perks), §5 del catálogo, lecciones.

## Verificación
- EditMode completo en verde (`node ut.js test editmode Conquest.Architecture.Tests asm`).
- No se toca el movimiento ni la melé: PlayMode S1/S6 solo si algún paso acaba tocándolos.
