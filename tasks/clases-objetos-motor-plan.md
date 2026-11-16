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
