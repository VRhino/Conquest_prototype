# Clips Humanoid del Modular Hero hechos en Blender

Clips para héroes y tropas con el esqueleto de `ModularCharacters.fbx` (Synty). Se generan con Blender por línea de
comandos desde `C:\Users\VRINO\Desarrollo\blender-tools\conquest-anims`. Allí están los scripts, el README y los vídeos de
revisión. Para cambiar un clip, se regenera allí y se copia aquí el FBX: los `.meta` guardan la configuración.

## Contenido
- `Hero_Rig.fbx`: solo el esqueleto, en T-pose y sin animación. Da el avatar **`Hero_RigAvatar`**, con el mismo mapeo de
  huesos que `ModularCharacters.fbx`. En Blender los dedos de la mano derecha llevan el sufijo `.001`, porque Blender no
  admite nombres repetidos.
- `Hero_Death.fbx`: muerte a pie, 1,8 s y sin bucle. Es el `motion` del estado `Death` de `AC_Polygon_Masculine`.
  1. Recibe un golpe en el pecho y retrocede.
  2. Se le doblan las rodillas y cae de rodillas.
  3. Se desploma de frente y queda boca abajo, con la cara de lado y el arma tendida junto a la cadera.

## Importación
- Todos los clips son **Humanoid** con *Copy From Other* de `Hero_RigAvatar`.
  - No se crea el avatar desde el propio clip: Unity tomaría como T-pose el primer fotograma y todo el retargeting
    saldría descuadrado, hasta 15 cm en manos y pies.
  - Los clips de jinete de `Assets/Art/Mounts/Horse/Animations/Riders` usan el mismo avatar.
- La raíz va integrada en la pose: rotación, altura con *Original* y XZ. El visual del héroe tiene `applyRootMotion`
  apagado, así que el desplazamiento de la caída se ve igual.
- El avatar de Synty asigna `RightToes` a `Toes_R`, la punta, y no a `Ball_R`. Por eso los dedos del pie derecho no se
  doblan en Unity, y en los clips el pie derecho se anima rígido.
