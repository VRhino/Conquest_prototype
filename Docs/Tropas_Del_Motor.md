# Las tropas del motor en Conquest

Cómo sale en Conquest cada una de las 37 tropas del motor (`TROPAS_RECLUTABLES` en
`BronzeAgeFase0/Docs/Mechanics/Heroe_Y_Tropa.md`). Bloque 3 de la [hoja de ruta](Hoja_De_Ruta.md); lo que el motor espera,
en `Trabajo_En_Conquest.md` §2.

## Qué viene del motor y qué pone Conquest

- **Del motor**, sin tocarlo en Conquest: el id (`tropaId`), el nombre, el tier (la rareza: I leva … V élite), los soldados
  por escuadra (`unidadesPorDefecto`) y las cifras de combate (`combate`: vida por soldado; daño, penetración, defensa y
  alivio en sus tres tipos; bloqueo; cadencia; alcance; y en las de distancia, precisión y munición). Las cifras son un
  borrador que el autor revisará tropa a tropa al probarlas. La versión del catálogo (`VERSION_CATALOGO_TROPAS`) viaja en
  el ticket: un ticket de otra versión se rechaza.
- **De Conquest:** el aspecto, las animaciones y el comportamiento (de una **base** de las que ya hay: escuderos, lanceros,
  piqueros, arqueros y las tres caballerías), qué **familias de formación** tiene cada tropa, cómo es cada formación, su
  formación de reposo y qué habilidad abre en los niveles 4, 7 y 10.

Las bases y sus modelos son de relleno (Synty): cada tropa irá teniendo lo suyo según llegue el arte, sin tocar su id.

## Cómo se importa

`Tools/BronzeAge/Importar tropas del motor` (`Assets/Scripts/Editor/TropasDelMotor.cs`; en batch,
`-executeMethod TropasDelMotor.Importar`) lee el catálogo del checkout del motor que está junto a este proyecto y crea o
actualiza cada tropa en `Assets/Resources/Squads/BronzeAge/<tropaId>/`, en la base de escuadras y fuera del reclutamiento.
Se vuelve a pasar cada vez que cambie el catálogo: las tropas que ya existen conservan sus assets y toman las cifras nuevas.
Escribe `catalogo.json` con la versión importada, y un ticket con otra versión se rechaza.

- **Cifras:** la vida, las defensas, el alivio (de % a fracción) y el bloqueo van a la tropa; el daño, la penetración, el
  alcance y la cadencia, a su módulo cuerpo a cuerpo; en las de distancia, al de tiro (con la precisión, la munición y un
  disparo cada `cadencia` segundos), y su cuerpo a cuerpo queda el de su base.
- **Formaciones:** la de cada familia es la de su base, o la de otra base si la suya no la tiene. Si tiene menos huecos que
  soldados la tropa, se hace una copia propia con más filas detrás, para que salgan todos: las falangitas sacan sus 32.
- **Formaciones del ticket:** la escuadra solo puede tomar las de `formacionesPermitidas` (el reposo, siempre) y empieza en
  la de `formacion` si la tropa la tiene, si no en la de reposo. Probado: unos arqueros con línea y reposo empiezan en línea
  y no pasan a dispersa aunque se pida.
- **Nivel y habilidades:** todas las tropas del motor comparten `Progresion_Del_Motor.asset`. El nivel del ticket, del 1
  al 10, suma un 1,5 % de vida y un 1,5 % de daño por nivel por encima del primero (decisión del autor del 2026-11-13); la
  defensa y la velocidad no cambian. Las habilidades se abren en los niveles 4, 7 y 10 (D8).
- **Carros de guerra:** no se importan (no hay carros en Conquest); un ticket con ellos se rechaza por tropa desconocida.

Las siete escuadras de relleno (`Resources/Squads/*` fuera de `BronzeAge/`) son las bases de las que se copian las tropas
del motor: no se pueden borrar hasta que cada base tenga su arte propio.

## Familias de formación

Las del motor, en su orden: `reposo`, `linea`, `columna`, `dispersa`, `muro_escudos`, `testudo`, `cuadro` y `cuna`. Toda
tropa tiene su **reposo** (con forma propia: los granjeros dispersos, los hoplitas juntos). En la batalla solo se usan las
de `formacionesPermitidas` del ticket; se empieza en la de `formacion` si la tropa la tiene y, si no, en la de reposo.
Conquest aún no tiene formaciones de **columna** ni de **cuadro**: ninguna tropa las lleva de momento.

## Por tropa (aprobado por el autor el 2026-11-13)

| Tropa | Nombre | Tier | Soldados | Base de Conquest | Familias (además de reposo) | Nota |
|---|---|---|---|---|---|---|
| `milicia_lanceros` | Milicia de lanceros | 2 | 25 | Lanceros | línea, dispersa, cuña |  |
| `lenadores` | Leñadores | 1 | 30 | Lanceros | dispersa | Leva sin formación: solo su reposo (dispersos) y dispersa. |
| `granjeros` | Granjeros | 1 | 30 | Lanceros | dispersa | Leva sin formación: solo su reposo (dispersos) y dispersa. |
| `mineros` | Mineros | 1 | 30 | Lanceros | dispersa | Leva sin formación: solo su reposo (dispersos) y dispersa. |
| `lanceros_mimbre` | Lanceros con escudo de mimbre | 3 | 25 | Lanceros | línea, dispersa, cuña |  |
| `espadachines_cobre` | Espadachines de espada corta de cobre | 3 | 20 | Escuderos | línea, dispersa, cuña | Sin escudo: el modelo de relleno lo lleva igual. |
| `hacheros_ligeros` | Hacheros ligeros | 3 | 18 | Escuderos | línea, dispersa, cuña | Sin escudo: el modelo de relleno lo lleva igual. |
| `espadachines_bronce` | Espadachines con espadas y escudos de bronce | 3 | 18 | Escuderos | muro de escudos, testudo, línea, dispersa |  |
| `lanceros_pesados` | Lanceros pesados micénicos | 4 | 15 | Lanceros | línea, dispersa, cuña, muro de escudos |  |
| `hacheros_armados` | Hacheros armados | 3 | 15 | Escuderos | línea, dispersa, cuña | Sin escudo: el modelo de relleno lo lleva igual. |
| `honderos` | Honderos | 2 | 20 | Arqueros | dispersa, línea | Dispara flechas hasta que haya honda. |
| `escaramuzadores_jabalina` | Escaramuzadores con jabalina | 3 | 20 | Arqueros | dispersa, línea | Dispara flechas hasta que haya jabalina. |
| `arqueros` | Arqueros | 3 | 20 | Arqueros | dispersa, línea |  |
| `arqueros_compuesto` | Arqueros con arco compuesto | 4 | 20 | Arqueros | dispersa, línea |  |
| `exploradores_caballo` | Exploradores a caballo | 2 | 20 | Caballería de lanza | línea, cuña, dispersa |  |
| `carros_guerra` | Carros de guerra | 4 | 15 | ninguna (carro) | — | No sale hasta que haya carros en Conquest: un ticket con carros se rechaza. |
| `jinetes_asirios` | Jinetes asirios | 3 | 20 | Caballería de lanza | línea, cuña, dispersa |  |
| `guerreros_filisteos` | Guerreros filisteos (Peleset) | 3 | 18 | Escuderos | muro de escudos, testudo, línea, dispersa |  |
| `shardana` | Shardana | 4 | 15 | Escuderos | muro de escudos, testudo, línea, dispersa |  |
| `hequetai` | Hequetai | 5 | 18 | Lanceros | línea, dispersa, cuña, muro de escudos |  |
| `honderos_rodios` | Honderos rodios | 3 | 20 | Arqueros | dispersa, línea | Dispara flechas hasta que haya honda. |
| `peltastas` | Peltastas | 4 | 20 | Arqueros | dispersa, línea | Dispara flechas hasta que haya jabalina. |
| `jinetes_escitas` | Jinetes escitas | 3 | 20 | Arqueros a caballo | línea, cuña, dispersa |  |
| `hoplitas_ciudadanos` | Hoplitas ciudadanos | 4 | 18 | Lanceros | línea, dispersa, cuña, muro de escudos |  |
| `caballeria_asiria` | Caballería asiria | 4 | 18 | Caballería de lanza | línea, cuña, dispersa |  |
| `arqueros_escitas` | Arqueros escitas | 5 | 20 | Arqueros | dispersa, línea |  |
| `espartiatas` | Espartiatas | 5 | 16 | Lanceros | línea, dispersa, cuña, muro de escudos |  |
| `epilektoi` | Epílektoi | 4 | 18 | Lanceros | línea, dispersa, cuña, muro de escudos |  |
| `inmortales` | Inmortales | 4 | 15 | Arqueros | dispersa, línea | Lanza y arco: de momento solo arqueros. |
| `peltastas_ificrates` | Peltastas de Ifícrates | 4 | 20 | Arqueros | dispersa, línea | Dispara flechas hasta que haya jabalina. |
| `caballeria_tesalia` | Caballería tesalia | 4 | 15 | Caballería de lanza | línea, cuña, dispersa |  |
| `caballeria_noble_persa` | Caballería noble persa | 5 | 16 | Caballería de lanza | línea, cuña, dispersa |  |
| `prodromos` | Pródromos | 3 | 18 | Caballería de lanza | línea, cuña, dispersa |  |
| `falangitas` | Falangitas | 4 | 32 | Piqueros | línea, dispersa |  |
| `hipaspistas` | Hipaspistas | 5 | 20 | Lanceros | línea, dispersa, cuña, muro de escudos |  |
| `companeros` | Compañeros | 5 | 16 | Caballería de lanza | línea, cuña, dispersa |  |
| `agrianos` | Agrianos | 5 | 20 | Arqueros | dispersa, línea | Dispara flechas hasta que haya jabalina. |

Lo que la tabla deja a medias (carros, hondas, jabalinas, la lanza de los inmortales, los espadachines sin escudo que
llevan uno) son huecos de arte, apuntados en el bloque 5 de la hoja de ruta: se completan cuando lleguen los modelos, sin
tocar los ids.

Criterio: la base sale del edificio y del arma (golpe dominante, alcance, escudo); con escudo grande
(bloqueo de 250 o más) se añade el muro de escudos; las levas del Centro Urbano, sin formación, solo van dispersas.

**Habilidades:** cada tropa abre en los niveles 4, 7 y 10 las de su base, en su orden (escuderos: aguantar con el escudo,
golpe de escudo, avance; lanceros: lanzas erizadas, carga coordinada; arqueros: andanada concentrada, paso ligero). Las
caballerías y los piqueros aún no tienen ninguna.
