# Inventory simplification

- [x] Add regressions for identity, slots, stacks, transactions, session and UI lifecycle.
- [x] Centralize session binding and atomic operations; publish/save completed changes once.
- [x] Route store and drag through services; repair cells, callbacks, rewards and tooltips.
- [x] Validate EditMode and real UI in Feudo/BattlePrep, run battle regressions, record lessons and update graph.

Reuse: InventoryManager, InventoryEventService, storage/equipment/consumable/store services, existing cell hierarchy, tooltip utilities, reward dialogs and SaveSystem. Preserve serialized field names and prefab identities. New tests have no existing inventory coverage to extend.

Validation (2026-10-09): Unity 6000.5.8f1; compilation clean; 98/98 EditMode and 27/27 PlayMode. Real UI in Feudo/BattlePrep; equipment presentation and existing locomotion/combat regressions in BattleTestScene. Graph updated. Historical analysis now includes implementation, results and limits.
