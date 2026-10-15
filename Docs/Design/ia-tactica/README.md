# Diseño vivo de IA táctica

Documento principal: [diseno-ia-tactica.html](diseno-ia-tactica.html). Versión 0.3, 2026-10-14. Estado: **ABIERTO**.

No implementar código de gameplay hasta cierre explícito del diseño por el usuario. Los controles del HTML son ilustraciones y utilidades de lectura del documento.

## Acuerdos

- D01: trabajar exclusivamente en diseño hasta cerrarlo.
- D02: compartir contactos observados por aliados, con memoria e incertidumbre.
- D03: diseño completo por fases; primer MVP con escudos y arqueros.
- D13: el cerebro coordina héroes y usa los mismos canales de control que el humano. Capacidades, reglas, cohesión y conducta individual pertenecen al gameplay compartido.

Las decisiones D04–D12 y D14 permanecen abiertas: D14 concreta el contrato común de acciones y resultados. Hay cuatro acuerdos y diez decisiones abiertas. Las recomendaciones del documento no equivalen a aprobación de esas decisiones.

## Evolución v0.3

El diagrama ahora muestra humano y bot convergiendo en control, validación y capacidades comunes. Las misiones y reservas permanecen dentro del cerebro; el resultado de gameplay alimenta su evaluación. Readiness observa cohesión real y commit coordina solicitudes sin activar daño ni conceder excepciones. Desarrollar capacidades de gameplay tiene un backlog distinto de enseñar al cerebro a utilizarlas.

## Cómo evolucionarlo

1. Mantener el mismo HTML como documento principal y aumentar su versión cuando cambien reglas o contratos.
2. Registrar el acuerdo explícito y actualizar el estado de su decisión; nunca inferir cierre a partir de una nota exportada.
3. Conservar separadas las evidencias del repo, propuestas de diseño y métricas previas.
4. Revisar el escenario de coordinación al cambiar reglas de preparación, interrupción o repliegue.
5. Si cambia arquitectura: crear la siguiente especificación candidata desde `arquitectura.v0.3-r1.json`, validar y entregar el diagrama con Archify, y sustituir el contenido escapado de `srcdoc` del iframe del documento principal por el HTML entregado. El documento sigue autocontenido. `arquitectura.json` conserva la especificación anterior y `arquitectura.v0.3.json` el primer candidato de esta revisión.
6. Verificar controles, escritorio/móvil, impresión y enlaces de evidencia. Mantener trazabilidad de la versión.

## Validación de esta versión

Documento principal SHA-256:

`cc2095d1d38300dbba846ef8b90835569ad4a88f422de06d1bed78087b58882c`

- Navegación: siete paneles en 1440×900, 1600×1000, 1920×1080 y 390×844; 28 comprobaciones sin desbordamiento horizontal.
- Controles: estados de coordinación, pérdida del ancla, apoyo bloqueado, modo oscuro y descarga de notas comprobados.
- Visor integrado: el iframe autocontenido carga el SVG.
- Impresión: siete secciones disponibles, detalles documentales abiertos durante impresión, colores claros incluso si se lee en modo oscuro.
- Enlaces relativos de evidencia: destinos presentes.
- Errores JavaScript observados: cero.
- Revisión visual: capturas de escritorio, móvil, esquema táctico y modo oscuro inspeccionadas. Scroll vertical esperado en los paneles documentales.
- Evidencia de QA del documento: `C:/Users/VRINO/.codex/visualizations/2026/10/14/01a139af-ee92-7cd3-82b4-605863f19fa1/design-qa.json` y capturas del mismo directorio.

### Recibo del diagrama Archify

```text
diagram_type: architecture
output: C:/Users/VRINO/Desarrollo/Conquest_prototype/Docs/Design/ia-tactica/arquitectura.html
specification_sha256: 56cc6665d961cfe05bd29143bc35a3838a7b43f8c80cc310c54b0ebc43d81a2e
artifact_sha256: 4c89793d73e0369989409d80718fdfa8c1bbb809d5694151317118fa38d334f1
validation: 9/9 showcase, 0 errors, 0 warnings
browser_evidence: passed
visual_review: passed
correction_rounds: 1
```

La revisión automatizada está vinculada al artefacto en [arquitectura.visual-check.json](arquitectura.visual-check.json). Comprueba el visor a 1440×900, 1600×1000, 1920×1080 y 2048×1320, con capturas claras/oscuras en los extremos. Se inspeccionaron imágenes claras y oscuras; el render no contiene cruces ambiguos ni solapamientos de etiquetas. Los controles fijos del visor y su idioma HTML usan inglés; el contenido redactado es español. El HTML principal es español.

Esta validación acredita el documento y los diagramas. No es una prueba de IA, combate, rendimiento ni compilación del juego.
