# Animation and locomotion repair — 2026-10-08

- [x] Connect confirmed horizontal velocity and accepted sprint to the three animation drivers; map gameplay speed to the existing Synty blend coordinates.
- [x] Repair unit input transitions, local direction, animation sampling order, root-motion authority and moving combat layers.
- [x] Validate actual controllers and movement in BattleTestScene, run targeted regressions, capture lessons and update the architecture graph.

Reuse: extend EcsAnimationInputAdapter, UnitAnimationSystem, UnitAnimationAdapter, VisualSyncUtility and BattleSceneIntegrationTests. Core/inventory services and events do not own animation. No shared speed-to-blend conversion exists; add a small shared settings/utility file to prevent three independent implementations.
