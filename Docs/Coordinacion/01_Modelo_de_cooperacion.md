# Modelo de cooperación entre repositorios y agentes

**Estado:** ACEPTADO PARA CONQUEST  
**Fecha:** 2026-09-10  
**Rama de trabajo:** `codex/bronzeage-unity-integration`

## 1. Propósito

Permitir que Codex desarrolle `Conquest_prototype` y Claude desarrolle `BronzeAgeFase0` sin editar a la vez
la misma fuente de verdad, sin contratos divergentes y sin depender de mensajes de conversación que no
queden registrados en Git.

`BronzeAgeClient` deja de ser un producto objetivo. Se conserva temporalmente como referencia funcional y
visual del cliente estratégico, y no debe recibir funciones nuevas salvo correcciones necesarias para
comparar comportamiento durante la migración.

## 2. Propiedad de repositorios

| Repositorio | Agente propietario | Puede modificar | No debe decidir unilateralmente |
|---|---|---|---|
| `Conquest_prototype` | Codex | Cliente Unity, presentación 3D, gameplay de batalla, adaptadores de red C#, ejecución ECS y herramientas Unity | Estado persistente, autorización estratégica y consecuencias finales de una guerra |
| `BronzeAgeFase0` | Claude | Backend, dominio persistente, API, autorización, reloj, reserva de tropas, ciclo de batalla y aplicación del resultado | Cómo se representa o controla la batalla dentro de Unity |
| `BronzeAgeClient` | Claude | Cliente de referencia, correcciones y apoyo a la migración mientras siga activo | Nuevas reglas o un segundo contrato independiente |

Los agentes pueden leer los tres repositorios. La única excepción de escritura cruzada es la carpeta de
propuestas del repositorio destino:

- Codex puede escribir todo `Conquest_prototype` y únicamente
  `BronzeAgeFase0/Docs/Coordinacion/propuestas/` dentro de BronzeAge.
- Claude puede escribir todo `BronzeAgeFase0` y únicamente
  `Conquest_prototype/Docs/Coordinacion/propuestas/` dentro de Conquest.
- Claude puede escribir `BronzeAgeClient`; Codex lo usa en modo de solo lectura como referencia de migración.

La propuesta se escribe donde debe ser ejecutada: `BA-*` dentro de BronzeAge y `CQ-*` dentro de Conquest.
Esto permite que el agente propietario la encuentre junto a su código y responda sin interpretar notas
copiadas entre repositorios.

Antes de trabajar, ambos agentes leen `Conquest_prototype/Docs/Coordinacion/Notas_de_integracion.md`. Es un
documento de autoría humana: tiene prioridad sobre documentos generados y no se modifica sin petición expresa.

## 3. Propiedad por concepto

| Concepto | Fuente de verdad | Consumidores |
|---|---|---|
| Cuenta, sesión y permisos | BronzeAge | Unity |
| Jugador en una partida, facción y ubicación | BronzeAge | Unity |
| Héroe y escuadras persistentes | BronzeAge, una vez migrados | Unity |
| Catálogos y balance estratégico | BronzeAge | Unity |
| Contratos HTTP/WS y DTO de batalla | BronzeAge | Unity |
| Estado temporal de una batalla en curso | Servidor autoritativo de batalla Unity | Clientes Unity; resumen final a BronzeAge |
| Controles, cámaras, UI y presentación 3D | Conquest | Ninguno |
| Reglas tácticas de combate, formaciones y captura | Conquest | Servidor y clientes Unity según autoridad de red |
| Resultado estratégico aplicado y persistido | BronzeAge | Unity |
| Geografía estratégica persistente | BronzeAge | Unity |
| Construcción visual del mundo 3D | Conquest | Cliente Unity |

`BronzeAgeFase0` es el propietario del contrato de red porque es quien debe validar y persistirlo. Conquest
puede proponer el contrato y mantener fixtures de compatibilidad, pero cuando el backend lo acepte su esquema
versionado pasa a ser la fuente de verdad.

## 4. Flujo de una propuesta

Cada propuesta usa un identificador estable:

- `BA-nnn`: cambio solicitado a BronzeAge.
- `CQ-nnn`: cambio solicitado a Conquest.
- `DEC-nnn`: decisión que afecta a más de un repositorio.

Estados permitidos:

1. `BORRADOR`: aún puede cambiar sin coordinación.
2. `LISTO_PARA_REVISION`: contiene contrato, motivos y criterios de aceptación completos.
3. `ACEPTADO`: el propietario confirma la solución y versión objetivo.
4. `IMPLEMENTADO`: incluye commit y comprobaciones realizadas.
5. `RECHAZADO`: conserva el motivo para no reabrir la misma discusión sin datos nuevos.
6. `SUSTITUIDO`: apunta al documento que lo reemplaza.

Una propuesta debe contener como mínimo:

- problema observable;
- comportamiento deseado, sin imponer detalles internos innecesarios;
- forma de datos propuesta o casos de ejemplo;
- invariantes de seguridad y autoridad;
- compatibilidad y migración;
- criterios verificables de aceptación;
- dudas que requieren una decisión del propietario.

Claude recoge las propuestas `BA-*` desde la carpeta de BronzeAge, actualiza su estado y añade el commit que
las implementa. Codex revisa después la compatibilidad desde Conquest. Para solicitudes en sentido contrario
se usa el mismo flujo con `CQ-*` en la carpeta de Conquest.

## 5. Reglas para contratos compartidos

1. Los contratos cruzan repositorios como JSON versionado y documentado, no como clases copiadas a mano.
2. Un ID es opaco, estable y distinto del nombre visible.
3. Las cantidades persistentes nunca se deducen contando entidades ECS vivas en un cliente.
4. Unity no puede enviar autoridad persistente: solicita acciones y reporta resultados desde un servidor de
   batalla autenticado.
5. Todo comando repetible por reconexión lleva una clave de idempotencia.
6. Los DTO incluyen `schemaVersion`; las reglas relevantes incluyen además su versión de balance.
7. Cambios incompatibles crean una versión nueva. No se cambia silenciosamente el significado de un campo.
8. Cada contrato publicado incluye al menos un fixture JSON válido y una prueba que lo lea en ambos lados.
9. Los nombres TypeScript y C# pueden adaptarse a sus convenciones; los nombres serializados no cambian.
10. Los modelos ECS son internos de Conquest y nunca forman parte del contrato de red.

## 6. Política de ramas y commits

- Codex trabaja la integración en `codex/bronzeage-unity-integration`.
- Claude usa una rama propia en BronzeAge, elegida por su propietario.
- Una propuesta y su implementación no se mezclan en el mismo commit entre repositorios.
- Los commits de adopción mencionan el ID de propuesta, por ejemplo `BA-001`.
- Ningún agente limpia, revierte ni incluye cambios locales ajenos para obtener un árbol limpio.
- Los cambios de assets generados por Unity se separan de cambios de contratos y código siempre que sea
  posible.

## 7. Sincronización y definición de terminado

Un cambio que cruce repositorios solo está terminado cuando:

- la propuesta está `IMPLEMENTADO` y enlaza ambos commits;
- el backend valida el contrato y rechaza versiones incompatibles;
- Conquest puede deserializar el fixture publicado;
- existe una prueba de reintento/idempotencia cuando el flujo modifica estado;
- se documenta quién puede producir cada mensaje;
- los nombres y los IDs no se usan como sustitutos entre sí;
- la compatibilidad se prueba sobre red o con fixtures, no mediante imports entre repositorios.

## 8. Orden de trabajo acordado

1. Establecer este modelo de cooperación.
2. Inventariar y clasificar todas las entidades de los tres repositorios.
3. Resolver identidad y propiedad de jugador, héroe, escuadra y unidad.
4. Publicar la primera versión de contratos compartidos.
5. Conectar Unity al login y a una proyección estratégica mínima.
6. Implementar el ciclo persistente de batalla sin reemplazar todavía el resolver numérico.
7. Conectar una partida real de Conquest como resolución autoritativa.
8. Migrar mapa de mundo y asentamiento a una presentación 3D con un worldgen revisado.
9. Retirar el cliente Vite como producto ejecutable cuando Unity cubra sus flujos aceptados.

## 9. Jerarquía de fuentes

Ante una contradicción se resuelve en este orden:

1. indicación actual del autor del proyecto;
2. `Notas_de_integracion.md`;
3. decisión conjunta marcada como aceptada;
4. contrato publicado por su repositorio propietario;
5. propuesta todavía no aceptada;
6. documentación histórica y comportamiento accidental del prototipo.
