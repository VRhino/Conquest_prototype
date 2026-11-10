# Caballo de montura

Recursos de la caballería: el caballo, sus arreos y los clips de los tres tipos de jinete. Se generan con Blender por
línea de comandos desde `C:\Users\VRINO\Desarrollo\blender-tools\conquest-horse`. Allí están los scripts, su README y los
vídeos de revisión. Para cambiar algo, se regenera allí y se copian aquí los FBX: los `.meta` guardan la configuración.

## Contenido
- `Models/Horse_Mount.fbx`, con rig **Generic** y avatar propio. Mira a +Z.
  - `Horse`: malla del caballo, 759 vértices.
  - `Horse_Tack`: arreos sin riendas.
  - `Horse_Reins_Hand`: riendas al puño izquierdo, para lancero y espada.
  - `Horse_Reins_Neck`: riendas sueltas sobre el cuello, para el arquero.
  - Hay que **activar solo una de las dos variantes de riendas**.
  - Esqueleto `HorseRig` de 34 huesos. **`Saddle`** es el socket del jinete: la raíz del héroe o tropa se cuelga de él en
    posición local (0, 0, 0) y con la rotación del caballo.
- `Textures/Horse_Palette_<capa>.png`: paletas de 256×256, en filtro Point y sin compresión. Las capas son `bay`,
  `chestnut`, `pinto`, `black`, `grey` y `dun`.
- `Materials/Horse_<Capa>.mat`: URP Lit, uno por capa. Todas comparten las UV. La manta es la celda `CLOTH` de la paleta.
- `Animations/Horse/Horse_{Idle,Walk,Trot,Gallop}.fbx`: rig **Generic** con el avatar de `Horse_Mount`, en el sitio y en
  bucle.

  | Clip | Velocidad de suelo |
  |---|---|
  | `Horse_Walk` | ~1,1 m/s |
  | `Horse_Trot` | ~2,9 m/s |
  | `Horse_Gallop` | ~6,3 m/s |

  Si la unidad va a otra velocidad, se escala la del estado para que los cascos no patinen.
- `Animations/Riders/{Lancer,Sword,Archer}_{Idle,Walk,Trot,Gallop,Ready,Attack}.fbx`: rig **Humanoid**, con el mismo
  mapeo de huesos que `ModularCharacters.fbx`. La raíz va integrada en la pose (rotación, altura con *Original* y XZ).
  - Cada clip de jinete dura lo mismo que el andar del caballo que le corresponde. Se reproducen juntos según
    `CurrentGait`: 0 idle, 1 paso, 2 trote, 3 galope.
  - `*_Attack` no va en bucle: empieza y acaba en `*_Ready`.
  - `Archer_Attack` suelta la flecha en los fotogramas 19-20.
