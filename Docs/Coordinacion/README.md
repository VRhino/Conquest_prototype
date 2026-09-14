# Coordinación BronzeAge + Conquest

Este directorio es el punto de intercambio entre el trabajo realizado en `Conquest_prototype` y el realizado
en `BronzeAgeFase0`. No convierte a Conquest en fuente de verdad del backend: contiene decisiones conjuntas,
propuestas para el propietario de cada repositorio y el estado de su adopción.

`Notas_de_integracion.md` es de autoría humana y tiene prioridad sobre análisis, propuestas o decisiones
generadas por agentes. Los agentes deben leerlo antes de diseñar o implementar trabajo de integración y no
deben modificarlo salvo petición explícita del autor.

## Documentos vigentes

- [`../Arquitectura/1_Arquitectura_Actual.md`](../Arquitectura/1_Arquitectura_Actual.md): línea base verificada
  del modelo y los flujos que existen hoy en Conquest; es la referencia de estado actual frente a las
  propuestas de esta carpeta.
- [`01_Modelo_de_cooperacion.md`](01_Modelo_de_cooperacion.md): propiedad, flujo de propuestas y reglas de trabajo.
- [`02_Arquitectura_objetivo.md`](02_Arquitectura_objetivo.md): división del juego unificado y flujo real de batalla.
- [`03_Modelo_compartido_entidades_v0.md`](03_Modelo_compartido_entidades_v0.md): inventario y propuesta de entidades compartidas.
- [`Notas_de_integracion.md`](Notas_de_integracion.md): notas y decisiones del autor del proyecto.

Las propuestas para BronzeAge no viven aquí. Codex las deposita directamente en
`BronzeAgeFase0/Docs/Coordinacion/propuestas/`, la única carpeta de ese repositorio que puede modificar.
Esta carpeta `propuestas/` queda reservada para propuestas entrantes dirigidas a Conquest y escritas por Claude.

## Regla principal

Cada dato tiene un único propietario autoritativo. El resto de repositorios consume contratos versionados; no
copia reglas de negocio ni mantiene una segunda versión editable del mismo modelo.

## Próximo hito

Crear el catálogo compartido de entidades. No será una clase universal compartida entre TypeScript y C#,
sino tres capas explícitas:

1. entidad persistente de BronzeAge;
2. DTO de red independiente del lenguaje;
3. representación de ejecución ECS/GameObject de Conquest.
