# Créditos de los emblemas del sigilo y de los iconos del mapa

Los emblemas (`emblemas.json`) son iconos de [game-icons.net](https://game-icons.net), con licencia
[Creative Commons BY 3.0](https://creativecommons.org/licenses/by/3.0/). La licencia obliga a citar a sus autores en los
créditos del juego:

- **Lorc** — https://lorcblog.blogspot.com
- **Delapouite** — https://delapouite.com
- **Caro Asercion**
- **Cathelineau**
- **Skoll**
- **Willdabeast**
- **Faithtoken**

Qué icono de quién va en cada emblema está en el propio `emblemas.json` (`autor`, `icono`). Los trazados salen tal cual del
repositorio https://github.com/game-icons/icons (`<autor>/<icono>.svg`, lienzo de 512×512, sin el cuadrado de fondo).

Para cambiar un icono: elige otro en game-icons.net, copia el `d` de sus `<path>` (salvo el fondo `M0 0h512v512H0z`) en la
entrada del emblema y actualiza `autor` e `icono`. Si el autor es nuevo, añádelo a esta lista. Si se añade un emblema, tiene que
existir antes en el catálogo del backend (`CATALOGO_SIGILO` en BronzeAgeFase0) y en `catalogoSigilos.json`.

Los iconos del mapa del mundo (`src/mundo/iconosMapa.json`: campamentos de bandidos y de mercenarios, columnas, nodos de
recurso, alijos, pueblos, avanzadas y la Caravana de Fundación) salen del mismo repositorio y se cambian igual; cada entrada lleva también su `autor` e `icono`.
