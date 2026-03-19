---
name: no-magic-params
description: >
  Detecta y previene magic numbers/strings hardcodeados en codigo.
  AUTO-INVOCAR cuando: (1) Claude acaba de escribir o modificar codigo con literales
  numericos como 3f, 0.5f, 100, 2f en logica de sistemas, (2) el usuario pregunta
  "donde configuro este valor", "como hago este valor editable", "necesito tunear esto",
  (3) un sistema ECS nuevo fue creado (siempre verificar despues de /new-ecs-system),
  (4) se escribe cualquier duracion, threshold, multiplicador o radio sin una config component.
  Aplica a sistemas ECS, MonoBehaviours, UI controllers y servicios.
  NO invocar para valores 0, 1, -1, math.PI ni constantes matematicas.
disable-model-invocation: false
allowed-tools: Read, Grep, Glob
---

# No Magic Params — Prohibir valores hardcodeados

## Regla principal

**NUNCA hardcodear valores numericos, durations, thresholds, multipliers, o strings de configuracion directamente en logica.** Todo valor que pueda cambiar durante balance/tuning debe ser parametrizable.

## Que detectar como violacion

- Literales numericos en logica de sistemas: `3f`, `0.5f`, `100`, `2f`
- Durations/timers: `stateTimer < 3f`, `cooldown = 10f`
- Thresholds: `distance < 0.04f`, `radius = 100f`
- Multipliers: `speed *= 2f`, `xpGain = 50f`
- Strings de configuracion: tags, layer names hardcodeados (ej: `"Enemy"`, `"PlayerLayer"`)

## Excepciones (NO son magic numbers)

- `0`, `1`, `-1` como identidades matematicas o indices
- `0f` como inicializacion o reset
- `math.PI`, `math.EPSILON` y constantes matematicas conocidas
- Valores en tests unitarios
- Enum conversions: `(int)MyEnum.Value`
- Operadores bit a bit y mascaras estandar
- `float3.zero`, `quaternion.identity` y similares de Unity.Mathematics

## Procedimiento de deteccion

### Paso 1: Identificar el archivo a revisar

Si se invoca manualmente con un archivo:
```
/no-magic-params Assets/Scripts/Squads/Systems/SquadFSM.System.cs
```

Si se auto-invoca: revisar el codigo que Claude acaba de escribir o modificar.

### Paso 2: Buscar violaciones

Leer el archivo completo y buscar:
1. Literales numericos que no sean 0, 1, -1 dentro de logica (no declaraciones de campo)
2. Literales `float` como `2f`, `3f`, `0.5f`, `100f` en comparaciones, asignaciones o calculos
3. Strings literales usados como identificadores de configuracion
4. `new float3(x, y, z)` donde x/y/z no son 0 o 1

### Paso 3: Clasificar cada hallazgo

Para cada literal encontrado, determinar:
- **Es violacion**: valor de balance/tuning/configuracion hardcodeado en logica
- **Es excepcion**: identidad matematica, inicializacion, test, o constante conocida

### Paso 4: Proponer solucion segun contexto

## Patron correcto del proyecto

### Para ECS Systems (patron canonico)

Referencia: `Assets/Scripts/Squads/SquadSpawnConfig.Component.cs` + `Assets/Scripts/Squads/SquadSpawnConfig.Authoring.cs`

1. Crear o extender un `*Config.Component.cs` con los campos necesarios
2. Crear o extender un `*Config.Authoring.cs` (editable en Inspector)
3. En el sistema: `var config = SystemAPI.GetSingleton<ConfigComponent>();`

```csharp
// Config Component
public struct SquadFSMConfig : IComponentData
{
    public float MinCombatDuration;
    public float RetreatThreshold;
}

// En el sistema
var config = SystemAPI.GetSingleton<SquadFSMConfig>();
if (stateTimer < config.MinCombatDuration) { ... }
```

### Para MonoBehaviours / UI Controllers

Usar `[SerializeField]` para exponer en Inspector:
```csharp
[SerializeField] private float fadeDuration = 0.3f;
[SerializeField] private float updateInterval = 0.5f;
```

O usar ScriptableObject si el valor se comparte entre multiples objetos.

### Para Services

Recibir valores como parametros de metodo, no como constantes internas:
```csharp
// MAL
public void ApplyDamage(float base) => total = base * 1.5f;

// BIEN
public void ApplyDamage(float base, float multiplier) => total = base * multiplier;
```

## Output

Presentar resultados en formato tabla:

```
## Magic Numbers Detectados

**Archivo:** `{ruta del archivo}`
**Violaciones encontradas:** {count}

| Linea | Valor | Contexto | Solucion propuesta |
|-------|-------|----------|--------------------|
| 90 | `3f` | `stateTimer < 3f` | Mover a config component como `MinCombatDuration` |
| 145 | `0.04f` | `distance < 0.04f` | Mover a config component como `ArrivalThreshold` |
| 200 | `"Enemy"` | `CompareTag("Enemy")` | Usar constante o config para tag names |

### Accion requerida
{Descripcion de los cambios necesarios: crear/extender config component, agregar authoring, etc.}
```

Si no hay violaciones:
```
## Magic Numbers Check — PASS
**Archivo:** `{ruta}`
No se encontraron magic numbers. Todos los valores estan correctamente parametrizados.
```

## Referencias

- `Assets/Scripts/Squads/SquadSpawnConfig.Component.cs` — Ejemplo canonico de config component
- `Assets/Scripts/Squads/SquadSpawnConfig.Authoring.cs` — Ejemplo canonico de authoring
- `Docs/ScriptableObjects_Architecture.md` — Patron ScriptableObject para datos compartidos
