# El motor ecfb23d en Conquest (análisis)

2026-11-16. Solo texto, sin código.
- **Motor:** main del motor, merge `ecfb23d` (PR #28): contrato 0.28.0, núcleo 0.44.0, combate 0.11.0. Sin partida nueva.
- **Ya hecho:**
  - `../BronzeAgeMotorPruebas` está en `ecfb23d`, con el servidor y el lanzador compilados.
  - El contrato está regenerado: `Assets/Plugins/BronzeAge/BronzeAge.Contrato.dll`, sin commit, compila igual que antes porque solo añade campos.

## 1. Elegir las escuadras al entrar (`OrdenUnirseABatalla.escuadron_ids`)

**Lo que dice el motor** (`Batalla_Unity_Ciclo.md` §7):
- La lista solo va en **unirse**. Atacar no lleva lista.
- **En su base** (el residente que defiende su plaza): elige del campamento lo que quepa en su Liderazgo, contando lo que ya lleva fuera.
  Vale solo para esa batalla y el loadout no cambia.
- **Fuera de su base:** entra todo lo que lleva. Una lista distinta se rechaza (`batalla.sin_elegir_escuadras`).
- Lista vacía: lo de antes (la columna entera, o el loadout activo del residente).
- Quien se baja no vuelve a entrar. La lista no se puede cambiar después de unirse.

**Lo que cambia en Conquest:**
- **Elegir solo tiene sentido en un caso: defender desde dentro de tu base.** En todos los demás, disponibles = elegidas = lo que llevas, y no hay
  nada que elegir.
- **La elección tiene que ir antes de «Unirse»:**
  - hoy la preparación (`PantallasDeMundo.Preparacion.cs`) se abre después de unirse y solo cambia el orden;
  - el motor no deja cambiar la lista una vez dentro;
  - así que «Defender» abre primero un panel con dos columnas, **disponibles** (las del campamento con soldados, fuera de la guarnición) y
    **elegidas**, con el contador de Liderazgo (lo que llevas fuera más lo elegido, sobre el total);
  - lo que no cabe no se puede añadir: no sale ni el botón ni el aviso;
  - «Defender» manda `unirseABatalla` con la lista.
- **La preparación**, después, enseña solo las elegidas (como hoy, con el orden).
- **Rechazos:** los de la orden (`liderazgo.superado`, `escuadra.*`) salen tal cual, como cualquier rechazo del motor.
- **Dónde está «Defender» hoy:** solo en las fichas del mapa (`CosasDelMapa.cs:444-494`, `Unirse`), y desde dentro de un sitio el mapa no hace
  nada. No hay botón de defender dentro de la plaza: hay que ponerlo (duda 1).

## 2. Bajarse de una batalla (`OrdenBajarseDeBatalla`)

- **Solo antes de empezar** (convocando o asignada). La batalla sigue. Si se baja quien la inició, la hereda otro atacante. Si no queda ningún
  humano, se cancela.
- **En Conquest:** la preparación lleva «Bajarse de la batalla» para todos los que combaten.
  - Hoy solo hay «Cancelar la batalla», para quien la inició (`PantallasDeMundo.Preparacion.cs:180-195`). Para el iniciador, duda 2.
  - Al bajarse: desconectar del servidor de batalla (como al cancelar) y volver a MundoScene con la columna tal cual.
  - Ya en curso, el botón no sale: se sale con «Abandonar» desde la batalla, como hoy.

## 3. La salida propia (`Batalla.salida_propia`)

- **El motor:** quien sale (abandona, cae, se desconecta o se baja) sigue viendo la batalla entera hasta que se retira, y con ella el resultado.
- **En Conquest:**
  - **El informe tras abandonar** dice hoy que el motor ya no cuenta cómo acabó (`PantallasDeMundo.Informe.cs:189-192`). Pasa a esperar el
    resultado como el de los demás, con una línea arriba: «Abandonaste la batalla» (o «Caíste», «Te desconectaste»), sacada de `motivo`.
  - **Bajarse antes de empezar** no tiene informe: vuelves al mapa.
  - **El pedido 3 al motor queda resuelto.**

## 4. El informe con el hecho de cierre (`heroe.cierre_batalla`)

- **Hoy:** el informe calcula lo que ganas comparando el héroe y su columna de antes y de después (`PantallasDeMundo.Informe.cs:32-57`, `356+`).
  Si el informe se abre sin lo de antes (desde el aviso, tras reiniciar), esa parte no sale.
- **Con el hecho:**
  - el recuadro «Lo que ganas o pierdes» sale del hecho: monedas, experiencia, botín, lo perdido del carro, los objetos ganados y los que no
    cupieron, y la herida;
  - la tabla «Tus escuadras» añade la experiencia que ganó cada una (hoy no se ve);
  - se borra lo de antes y después (`Antes`, `antesDeLaBatalla`, `LoQueCambia`);
  - funciona también desde el aviso y después de reiniciar, porque el hecho llega con los demás (`Almacen.Hechos`);
  - las cantidades vienen en milésimas: se dividen al pintar.
- **El pedido 5 al motor queda resuelto.**

## 5. El id de la tropa (`EscuadraQueLleva.tropa_id`)

- Se borra `IdDeTropa` (`PantallasDeMundo.Ejercito.cs:380-390`), que pasaba nombre a id, y se usa `TropaId` tal cual en sus tres sitios:
  - `Ejercito.cs:377`;
  - `Mercenarios.cs:557`;
  - `Taberna.cs:358`.
- Las pruebas que lo usan (`TuEjercitoEnElMapa`, `TuTablonDeMercenarios`, `TuTaberna`) siguen igual: miran el dibujo y el nombre, no el id.

## 6. Las pruebas que faltaban del bloque 4, con las órdenes de administración

- **Se pueden hacer:**
  - **Los mercenarios de la región** (paso 12): `dar_monedas` al héroe y `dar_fondo_monedas` a la plaza. Contratar desde la Taberna, con el
    precio en monedas y el fondo.
  - **Los Aedas** (paso 13): `colocar_aeda`.
    - Sin `residente`: un itinerante en la plaza, con su ficha en el mapa.
    - Con `residente`: uno residente, con sus épicas.
    - Comprar a un itinerante solo si con eso trae algo que vender. Si no, la compra sigue sin prueba (duda 3).
- **Siguen sin prueba**, porque estas órdenes no las tocan:
  - el rastreador de misiones en el mapa;
  - «Más antiguas» y la entrada en vivo de la crónica;
  - «Entregar» de una caravana del ejército;
  - «Reponer» en la Base.

## Tandas

1. **Tanda 1, lo pequeño:**
   - el id de la tropa (5);
   - «Bajarse» en la preparación (2);
   - la salida propia en el informe (3);
   - las pruebas de mercenarios y Aedas con las órdenes de administración (6).
2. **Tanda 2, el informe con el hecho de cierre (4):** fuera la comparación de antes y después, y la experiencia por escuadra.
3. **Tanda 3, elegir las escuadras al defender desde tu base (1):**
   - el panel de disponibles y elegidas con el Liderazgo;
   - el botón de defender dentro de la plaza;
   - la prueba con un asedio a tu plaza y tres tropas en el campamento, como tu ejemplo: granjeros y mineros dentro, 14 de 14, y la milicia
     fuera, que no sale en la batalla.

Cada tanda, con su prueba con ratón y teclado, la suite, las dos batallas y la Hoja. Sin commit hasta que lo digas.

## Dudas

1. **Dónde defiendes desde dentro de tu plaza.** Hoy no hay botón: «Unirse» solo está en las fichas del mapa, y desde dentro el mapa no hace
   nada.
   - a) Un aviso arriba en la plaza, «Asedio a tu plaza · Defender», que abre el panel de elegir y manda la orden. Sale solo si te toca
     defender y aún no estás dentro. **(propuesta)**
   - b) En la ventana de la batalla del riel de la plaza.
   - c) Copiar lo que tenga el prototipo. Hoy `panelBatalla.ts` solo une sin lista; habría que pedírselo a su hilo.
2. **Quien inició la batalla, ¿«Bajarse», «Cancelar» o los dos?**
   - a) Los dos: «Cancelar» la acaba para todos y «Bajarse» te saca y la hereda otro atacante. Con un solo humano, solo «Cancelar», porque
     bajarse la cancela igual. **(propuesta)**
   - b) Solo «Bajarse», que con un solo humano ya cancela.
3. **Comprar a un Aeda itinerante** en la prueba. `colocar_aeda` lo pone, pero en una partida nueva puede no tener nada revelado que vender.
   - a) Probar lo que haya. Si no vende nada, la compra se queda sin prueba y lo apunto en la Hoja. **(propuesta)**
   - b) Pedir al motor que `colocar_aeda` pueda traer algo que vender.
4. **El residente que ya está dentro cuando se abre el asedio.** El motor lo mete solo, con su loadout activo (`DefensoresDeDentro`,
   `combates.rs`), sin pasar por `unirseABatalla`, así que no elige.
   - a) Preguntarlo al hilo del motor antes de la tanda 3: si entra solo, que pueda elegir en la preparación (una orden de elegir mientras se
     convoca). **(propuesta)**
   - b) Aceptarlo: quien ya está dentro defiende con su loadout activo, y solo elige quien se une después.

## Respuestas de Víctor (2026-11-16)

- **1a:** un aviso arriba en la plaza, «Asedio a tu plaza · Defender», que abre el panel de elegir (tanda 3).
- **2a:** quien inició tiene «Cancelar» y «Bajarse»; si es el único humano, solo «Cancelar». Va en la tanda 1.
- **3a:** el Aeda, probar lo que haya. Hecho: en una partida nueva no vende nada y la compra queda sin prueba, apuntada en la Hoja.
- **4, abierta:** el residente que ya está dentro al abrirse el asedio. Le he explicado el porqué (`defensa_de_plaza` lo mete con su loadout
  activo) y propuesto pedir al motor una orden para cambiar las elegidas mientras se convoca.
- **Red:** los 10 s y los 60 s se quedan. La predicción del héroe sigue abierta: a) la malla, b) Unity Physics o c) la malla más el choque con
  los soldados.
