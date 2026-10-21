# Pila de modificadores: lo que queda fuera

Fecha: 2026-10-21. Sale del cierre de la Fase 1 de `Docs/Design/Aprendizajes_Referencia.md` (pila de modificadores, T1;
plan en `tasks/todo.md`). La regla y las fuentes están en `Docs/Mechanics/DamageMechanics.md` §4.1.

## Resumen
| # | Tema | Prioridad | Estado |
|---|---|---|---|
| P1 | Stats de objeto con `StatId` (L7-6) | Media | Pendiente (decisión del usuario: para después, documentado) |
| P2 | Referencia rota de `HeroClassDefinition.prefab` | Media | Pendiente |
| P3 | Penetración de los héroes en batalla | Baja | Pendiente (decisión de balance) |
| P4 | Atributos revalidados cada frame (resto de L7-5) | Baja | Pendiente |
| P5 | Suelo del crítico con multiplicadores menores que 1,2 | Baja | Pendiente (sin datos afectados hoy) |
| P6 | Prefab de VFX de impacto ausente: `BattleSceneIntegrationTests` falla en el SetUp | Media | Bloqueado (contenido) |

---

## P1. Stats de objeto con `StatId` (L7-6)
**Qué es.** Los ítems guardan sus stats como `Dictionary<string, float>` (claves como `"Vitality"` o `"PiercingDefense"`), que
se serializa en `InventoryItem.serializedStats` (`Data/Persistence/inventoryItem.Data.cs`). Los generadores
(`Inventory/statsGenerators/BasicArmorStatGenerator.cs`) escriben esas claves, y `DataCacheService.GetEquipmentBonuses` solo
lee cuatro (fuerza, destreza, armadura, vitalidad) y las pasa a la pila como planos `Equipment` sobre los atributos.

**Por qué queda fuera.** Cambiar la clave a `StatId` cambia el formato del guardado. El usuario decidió dejarlo para después,
documentado.

**A considerar.**
- La referencia guarda ids estables de efecto y la semilla de generación, no el resultado: el guardado es pequeño y el servidor
  puede validarlo regenerando.
- `StatId` es un `byte` estable pensado para esto: no reordenar sus valores, solo añadir al final.
- Los demás stats de los ítems (vida, defensas) hoy no llegan a la batalla. Con `StatId` podrían entrar en la pila directamente
  como planos sobre el stat, sin pasar por los atributos.

**Cómo abordarlo.** Una tabla nombre → `StatId` para leer los guardados actuales; migrar al cargar, guardar ya con id (y semilla
si se adopta) y conservar la lectura antigua un tiempo. Después, `GetEquipmentBonuses` deja de ser por nombre y cada stat
del ítem se convierte en un modificador.

## P2. Referencia rota de `HeroClassDefinition.prefab`
**Qué es.** `Assets/Prefabs/Hero/HeroClassDefinition.prefab` apunta a un `HeroClassDefinition` que no existe (guid
`e1db06423e48ce842bcbde49b3ea580f`), así que hornea una entidad de clase vacía. En batalla, `HeroInitializationSystem` nunca
inicializaba héroes: la vida del héroe local solo salía de una escritura en `HeroSpawnSystem`.

**Estado.** El paso 3 no depende de esa referencia: la clase sale del guardado (local) o de `HeroClassComponent`, que pone
`BattleSceneController` (remotos e IA). La referencia sigue rota, y con ella `HeroAbilityComponent` y los perks válidos que
se copian de la clase horneada.

**Cómo abordarlo.** Asignar el asset correcto en el prefab (o una clase por héroe) y comprobar que `HeroAttributesComponent`
y `HeroAbilityComponent` aparecen en batalla.

## P3. Penetración de los héroes en batalla
**Qué es.** `HeroCalculatedAttributes.Calculate` calcula penetración desde fuerza y destreza, pero nunca se aplicó: el perfil de
daño del héroe se creaba sin ella. `HeroStatFormula` la deja fuera a propósito para no cambiar el balance.

**Cómo abordarlo.** Si se quiere, añadir los tres casos de penetración a `HeroStatFormula.Derived` y revisar el balance contra
escuadras con defensa alta.

## P4. Atributos revalidados cada frame
**Qué es.** `HeroAttributeSystem` valida los atributos contra los límites de la clase en cada frame fuera de combate. L7-5
propone validarlos al asignarlos. El paso 7 solo movió la XP y los desbloqueos a datos.

## P5. Suelo del crítico
**Qué es.** El daño extra del crítico tiene un suelo de 0,2 (`CombatTuning.criticalExtraFloor`, como la referencia). Un arma
con multiplicador menor que 1,2 haría 1,2 en el crítico. Hoy todos los datos usan 1,5.

## P6. Prefab de VFX de impacto ausente
**Qué es.** `HitImpactEffectManager` de las escenas de batalla apunta a un prefab (guid
`ec3f674830402cc45bce805cf6493db7`) que no está en el repositorio. Registra un error en `Awake`, y el Test Runner falla el
SetUp de todos los `BattleSceneIntegrationTests` por ese mensaje. No lo causa la Fase 1.

**Cómo abordarlo.** Añadir el prefab al repositorio, o reasignar uno existente, y volver a pasar `BattleSceneIntegrationTests`.
Mientras tanto, la Fase 1 se verificó en `BattleTestScene` leyendo los componentes en Play.
