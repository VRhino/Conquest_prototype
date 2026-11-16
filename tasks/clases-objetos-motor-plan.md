# Las clases y los objetos del motor en la batalla (análisis)

2026-11-16. Solo texto, sin código. Es el punto 3 del orden de Víctor.
- Motor: `c94a960`.
- Las cifras salen de los bloques de parámetros de `Docs/Mechanics/Objetos_Del_Heroe.md` y `Heroe_Y_Tropa.md` del motor. Conquest las tiene
  en `GET /v1/datos` (el mundo) y, para la batalla, tendría que importarlas, como las tropas.

## Lo que hay hoy

- **La clase.**
  - El motor tiene 5 (`CLASES_HEROE`, `bronzeage-clases-1`): `lanza_escudo_redondo`, `espada_escudo_mediano`, `arco_lanza`,
    `hacha_dos_manos` y `dagas`. Cada una tiene sus atributos base (suman 10) y sus armaduras.
  - La clase es la del arma que lleva el héroe.
  - Conquest tiene 4 clases de relleno (`Resources/Data/HeroClasses`). Las traduce con `VestidoDelMotor.ClaseDeConquest`, y
    `espada_escudo_mediano` y `dagas` caen en `SwordAndShield`.
- **Los atributos.**
  - El ticket trae `atributos_efectivos`: la base de la clase y los puntos repartidos, sin el equipo. El equipo lo suma Conquest.
  - **Conquest no los usa.** Saca los atributos del asset de la clase de relleno.
- **Los objetos.**
  - El catálogo `OBJETOS` (`bronzeage-objetos-1`) tiene 136 piezas.
  - Las armas tienen atributos, daño y penetración por tipo; las armaduras, atributos, defensa por tipo y salud; las monturas, vida, trote,
    galope y aguante.
  - En el ticket, una pieza forjada trae sus cifras ya multiplicadas por su calidad (`estadisticas`). Una de tienda o de botín llega sin
    cifras: se sacan de su ficha del catálogo.
  - **Conquest pone en su lugar una pieza de relleno** del mismo hueco y peso (`PiezaDeConquest`), con cifras **al azar** de sus
    generadores. De esas cifras solo usa los cuatro atributos: el daño, la defensa y la salud de la pieza no cuentan, y la montura tampoco.
- **Las cifras de combate del héroe** salen de la fórmula de Conquest (`HeroStatFormula`): la base del asset de la clase, más cada atributo por
  su coeficiente.
  - La escala no es la del motor: Conquest usa daño base 5 más atributos; el motor, 91-155 por arma.
  - Las tropas ya van con las cifras del motor.
- **Los carros de guerra** no se importaron: no hay carros en Conquest, y un ticket con ellos se rechaza entero.
- **Lo que se rechaza:** solo otra versión del catálogo de tropas o una tropa desconocida (`BatallaDeTicketSystems.Preparar`). Las versiones
  de los catálogos de clases y objetos llegan en el ticket (`Reglas.VersionCatalogoHeroe` y `VersionCatalogoObjetos`), pero no se miran.
  Una clase desconocida entra sin aviso como espada y escudo; un objeto desconocido deja el hueco vacío sin aviso.
- **El relleno del héroe:**
  - las 4 clases y unos 24 objetos (no 19);
  - los usan las pantallas de un jugador (selector, creador, tienda, inventario, preparación vieja) y sus pruebas;
  - **el aspecto del héroe** (`visualPartId` de cada pieza) sale de esos objetos.
- **`terrain-tools`:** nada en Assets lo usa (ni código ni assets; queda por comprobar que ningún asset apunta por GUID a sus recursos).

## Lo que haría

1. **Importar los dos catálogos**, como las tropas (`Editor/TropasDelMotor.cs`), a `Resources/Heroe/BronzeAge`:
   - `CLASES_HEROE`, con su versión;
   - `OBJETOS`, con su versión: tipo, hueco, peso, metal y las cifras de `combate`.
2. **Rechazar como con las tropas:** otra versión de un catálogo, una clase o un objeto que no está, y la batalla se rechaza con su motivo
   (duda 3).
3. **El héroe, desde el ticket:**
   - la clase del motor (las 5);
   - los atributos de `atributos_efectivos`;
   - cada pieza con sus cifras: las de `estadisticas` si es forjada; si no, las de su ficha del catálogo;
   - las cuentas de combate con esas cifras (duda 1);
   - la montura, con su vida y su paso.
4. **Los carros de guerra:** su dato, con sus cifras del motor, para que el ticket no se rechace. El aspecto, prestado (duda 2).
5. **Borrar el relleno del héroe** (duda 4).
6. **Quitar `terrain-tools`** de `Packages/manifest.json` tras comprobar los GUID, y la suite.

## Tandas

1. **Los catálogos y el rechazo:**
   - importar `CLASES_HEROE` y `OBJETOS`;
   - comprobar versiones y clases u objetos desconocidos;
   - los carros de guerra como dato;
   - fuera `terrain-tools`.
2. **El héroe del ticket en la batalla:**
   - las 5 clases y `atributos_efectivos`;
   - las cifras de cada pieza (forjada o de ficha) y la montura;
   - las cuentas de combate en la escala del motor.
   - Prueba: un héroe con pieza forjada y otro con pieza de tienda llegan a la batalla con sus cifras.
3. **Borrar el relleno del héroe** y lo que solo él usa, con el aspecto ya sacado del catálogo.

## Dudas

1. **Las cuentas de combate del héroe.** Hoy van en la escala de Conquest (daño base 5 más atributos) y las de las tropas, en la del motor
   (daño 91-155 por arma, vida 1650 un carro).
   - a) La escala del motor: el daño, la penetración, la defensa y la salud de las piezas, como el motor las da, y los atributos sumando con
     los coeficientes de Conquest, recalibrados para esa escala. Calibrar contra las tropas en una batalla de prueba. **(propuesta)**
   - b) Pedir al motor la fórmula del héroe en combate (cómo los atributos se vuelven daño, vida y defensa) y aplicarla tal cual.
   - c) Dejar las cifras de Conquest y usar del motor solo los atributos.
2. **El aspecto de los carros de guerra**, hasta que haya arte.
   - a) Como la caballería (jinetes a caballo), con las cifras de carro. **(propuesta)**
   - b) Como infantería pesada.
3. **Lo desconocido en el ticket** (otra versión, o una clase u objeto que Conquest no tiene).
   - a) Se rechaza la batalla entera, con su motivo, como con las tropas: nunca se juega con cifras que no son las del motor. **(propuesta)**
   - b) Se avisa y se juega: una clase desconocida como espada y escudo, un objeto desconocido con el hueco vacío.
4. **Hasta dónde borrar el relleno del héroe.** Las 4 clases y los objetos los usan también las pantallas de un jugador de antes y sus
   pruebas: tienda, inventario, la preparación vieja y el creador.
   - a) Borrar las clases y los objetos de relleno y lo que solo vive de ellos (las pantallas viejas de un jugador y sus pruebas). Las que
     siguen en uso (el creador de personaje, el vestido del héroe) pasan a leer el catálogo del motor. **(propuesta)**
   - b) Solo dejar de usarlos en la batalla en red y en el mundo, y borrarlos cuando se borren las pantallas viejas.
5. **`dagas`**, la quinta clase. Conquest no tiene arma ni animaciones de dagas.
   - a) Con las cifras de dagas y el aspecto y las animaciones de espada y escudo, hasta que haya arte. **(propuesta)**
   - b) Rechazar una batalla con un héroe de dagas hasta que haya arte.

## Respuestas de Víctor (2026-11-16): la a en todas

1. Las cuentas del héroe, en la escala del motor: las piezas tal cual y los atributos recalibrados contra las tropas.
2. Los carros de guerra, con el aspecto de la caballería hasta que haya arte.
3. Una clase o un objeto desconocidos, u otra versión de su catálogo: se rechaza la batalla con su motivo, como con las tropas.
4. Se borran las clases y los objetos de relleno y las pantallas viejas de un jugador que solo viven de ellos, con sus pruebas. El creador y el
   vestido pasan al catálogo del motor. Va en la tanda 3, y antes de borrar se manda la lista exacta.
5. Las dagas, con sus cifras y el aspecto de espada y escudo hasta que haya arte.

## Tanda 2, hecha (2026-11-16): el héroe del ticket en la escala del motor

- **Las cifras de cada héroe** (`Net/CifrasDelHeroe.cs`, `HeroEngineStatsComponent`):
  - sus `atributos_efectivos`;
  - lo que suman sus piezas: las cifras de `estadisticas` si es forjada, las de su ficha si no;
  - el daño y la penetración del arma, y la defensa y la salud de la armadura.
- **Las cuentas** son las reglas del héroe que el autor del motor propuso a Conquest (`Trabajo_En_Conquest.md` §3.5). Con ellas están
  ajustadas las cifras de `OBJETOS`. Están en `HeroGameplayConfig` (`HeroEngineRules`):
  - vida = 1000 + 30 por punto de vitalidad + la salud de la armadura;
  - alivio de un tipo = 0,25 % por punto de defensa de ese tipo + 0,25 % por punto del atributo armadura, con el tope del 75 %. Sin
    defensa plana;
  - golpe de un tipo = el del arma × (1 + 1 % por punto del atributo de daño de la clase): la fuerza en la lanza y el hacha, la destreza en
    el arco y las dagas, la media de las dos en la espada. La penetración, la del arma;
  - el aguante, el de la clase de Conquest.
- **Comprobado contra las referencias del autor** (tier I, armadura inicial, sin puntos; vida efectiva / golpe):
  - lanza 2394/96 (2381/96);
  - espada 2249/99 (2243/99);
  - arco 1984/111 (1984/110);
  - hacha 2269/102 (2269/101);
  - dagas 1979/112 (1979/111).
- **La montura** del ticket da la vida, el trote, el galope y el aguante del caballo de ese héroe (`HeroHorseStatsComponent`), en el servidor
  y en el cliente.
- **Sin montura en el ticket, a pie** (Víctor, 22a): el servidor no le deja llamar al caballo (`HeroOnFootTag`). El capitán conserva el
  caballo de siempre.
- **El capitán** de una escuadra sin héroe va con las cifras de un héroe de espada y escudo recién nacido en el motor.
- Las habilidades, las animaciones y el aspecto siguen siendo los de la clase de Conquest pariente; las dagas, los de espada y escudo.

## Tanda 3: lo que decidió Víctor (2026-11-17, 23a y 24a)

- **23a:** se borra lo que solo vive de sí mismo (la preparación vieja, los sueltos sin uso y sus pruebas) y pasa al catálogo del motor lo
  que sigue vivo:
  - el aspecto y el icono de cada pieza, por su id;
  - las clases;
  - el creador, el selector y `VestidoDelMotor`.
- Los paneles viejos del campamento (inventario, ficha, tienda y cuartel) quedan para cuando se rehaga el campamento.
- **24a:** las clases de Conquest son 5 assets, uno por clase del motor y con su id, con sus habilidades, rasgos de golpe, resiliencia y
  aguante. Las dagas y la lanza con escudo empiezan copiando a sus parientes.
