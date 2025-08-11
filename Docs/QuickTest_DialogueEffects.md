# Quick Test Guide: Dialogue Effects

## Setup Rápido (5 minutos)

### 1. Crear Efectos de Test
1. En Unity Project, ir a `Assets/Resources/`
2. Crear carpeta `DialogueEffects` (si no existe)
3. Right-click en `DialogueEffects/` → Create → Dialogue → Effects → Add Item
4. Nombrar: `TestAddItem`
5. Configurar:
   - Effect ID: `TestAddItem`
   - Item ID: `WeaS&SBasic` (usa un ítem que exista en tu ItemDatabase)
   - Quantity: `1`

6. Right-click en `DialogueEffects/` → Create → Dialogue → Effects → Give Experience  
7. Nombrar: `TestGiveXP`
8. Configurar:
   - Effect ID: `TestGiveXP`
   - Experience Amount: `100`

### 2. Crear Database
1. Right-click en `Assets/Resources/` → Create → Dialogue → Dialogue Effect Database
2. Nombrar: `DialogueEffectDatabase`
3. Arrastrar los efectos creados al array "Dialogue Effects"

### 3. Configurar NPC para Test
1. Abrir cualquier NpcDialogueData existente
2. Agregar una nueva opción:
   ```
   Option Text: "Get test reward"
   Option Type: ExecuteEffects
   Dialogue Effect Ids: ["TestAddItem", "TestGiveXP"]
   Effect Parameters:
     - String Parameter: "WeaS&SBasic"
     - Int Parameter: 100
   ```

### 4. Test en Juego
1. Ejecutar el juego
2. Interactuar con el NPC configurado
3. Seleccionar la opción "Get test reward"
4. Verificar en consola los logs de ejecución
5. Verificar que el héroe recibió el ítem y experiencia

## Verificación Rápida

Si agregaste el `DialogueEffectTester.cs` a un GameObject:
1. En Inspector, usar `Create Test Effects` (muestra instrucciones)
2. Usar `Test Dialogue Option` (ejecuta efectos directamente)
3. Usar `Show Hero Status` (muestra estado del héroe)

## Debug

Si algo no funciona:
1. Verificar consola para errores
2. Verificar que DialogueEffectDatabase esté en Resources/
3. Verificar que los Effect IDs coincidan exactamente
4. Verificar que el ítem usado exista en ItemDatabase

## Resultado Esperado

En consola deberías ver:
```
[NpcTriggerZone] Executing 2 dialogue effects...
[DialogueEffectSystem] Executing effect: TestAddItem
[DialogueEffectSystem] Executing effect: TestGiveXP
[NpcTriggerZone] All effects executed successfully
```

En el juego:
- Héroe gana 100 XP
- Héroe recibe 1x WeaS&SBasic en inventario
