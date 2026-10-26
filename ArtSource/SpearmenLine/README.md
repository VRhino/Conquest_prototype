# Lanceros: fuentes de la formación Line

La guía de proceso, integración y aprendizajes está en
[Crear animaciones de formación](../../Docs/Guides/Crear_Animaciones_De_Formacion.md).

## Archivos conservados

- `spearmen_line_pose_review.blend`: revisión editable de seis soldados, fotograma 1.
- `preview_settings.json`: ajustes de pose, agarre, separación y presentación.
- `inspect_source.py`, `build_preview.py`, `pose_preview.py`: reconstrucción y exportación.
- `spearman_source.blend`, `spearman_assembled.blend`, `assembly_report.json`: entradas
  de las etapas de ensamblado y pose. El informe contiene matrices necesarias.
- `unity_pose_source.json`: poses exportadas que consume el importador de Unity.
- `unity_integration_settings.json`: rutas y tiempos de la integración.

## Regenerar

Desde la raíz del repositorio:

```text
blender.exe --background --factory-startup --python ArtSource/SpearmenLine/inspect_source.py
blender.exe --background --factory-startup --python ArtSource/SpearmenLine/build_preview.py --python ArtSource/SpearmenLine/pose_preview.py
```

Después, fuera de Play, ejecutar **Conquest → Spearmen → Rebuild Line Formation
Animations** en Unity. Guarda aparte las ediciones manuales antes de regenerar:
se sustituyen los archivos generados y los estados/clips de formación. Blender
no recarga automáticamente un archivo abierto que ha cambiado en disco.

Los renders, informes de diagnóstico y copias `.blend1` son salidas temporales:
se han eliminado de esta entrega y se ignoran localmente al regenerar.
Las pruebas permanentes siguen en `Assets/Tests/EditMode/`.

La separación del estudio y de Line en el juego es 0,67 m entre columnas y
0,65 m entre filas, sin ruido de posición o giro. Las seis referencias corresponden a Line.
