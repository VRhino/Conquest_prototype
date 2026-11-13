// El mapa de mundo con cuadros (paso 2 del bloque 4): el suelo por teselas, el agua, los ríos y la niebla, la cámara de BronzeAgeClient, tu
// columna como caja con su posición al instante del reloj, su ruta y su destino, y el clic en el suelo para marchar. Es interfaz de fuera de
// la batalla: no usa el ECS de la batalla ni `Conquest.Net`; lee el almacén de la sesión y da órdenes del contrato.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BronzeAge.Contract.V1;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace ConquestTactics.Mundo.Mapa
{
    public sealed class MapaDeMundo : MonoBehaviour
    {
        const float ArrastreMinimoPx = 5, PausaRuedaSeg = 0.28f;

        Sesion sesion;
        Relieve relieve;
        DatosDeJuego datos;
        Func<string> heroeId;
        Func<Vector2, bool> sobreInterfaz;
        Action<string, bool> avisar;

        public readonly CamaraDelMapa Camara = new CamaraDelMapa();
        Camera camara;
        TerrenoDelMapa terreno;
        NieblaDelMapa niebla;
        GameObject rios, caja, destino;
        LineRenderer ruta;
        Material materialSuelo, materialPropio;
        Vector2? pulsado;
        Vector2 ultimoRaton;
        float ultimaRueda = -1;
        bool vistaPedida;

        /// <summary>Donde está el héroe ahora (u) y si lo que se sigue es su columna.</summary>
        public (double x, double y)? Centro { get; private set; }
        public Ejercito Columna { get; private set; }

        /// <summary>Monta el mapa. `sobreInterfaz`: si el puntero está sobre la interfaz (entonces el clic no es del mapa); `avisar`: un aviso
        /// flotante (texto, peligro).</summary>
        public void Iniciar(Sesion sesion, Relieve relieve, ArtefactoDeMundo artefacto, DatosDeJuego datos, Func<string> heroeId,
            Func<Vector2, bool> sobreInterfaz, Action<string, bool> avisar)
        {
            this.sesion = sesion;
            this.relieve = relieve;
            this.datos = datos;
            this.heroeId = heroeId;
            this.sobreInterfaz = sobreInterfaz;
            this.avisar = avisar;
            EscalaDelMapa.Fijar(artefacto.Manifiesto.FactorUnity);

            var sombreado = Resources.Load<Shader>("MapaMundo/MapaColor");
            materialSuelo = new Material(sombreado) { name = "suelo del mapa" };
            materialPropio = new Material(sombreado) { name = "lo propio" };
            materialPropio.SetFloat("_ConNiebla", 0);

            var nivelDelMar = (float)(datos.Numero("documentos.Generacion_Del_Mundo.ELEVACION.umbralAgua") * Relieve.MetrosAltura);
            terreno = new TerrenoDelMapa(relieve, new RasterDeMundo(artefacto.Terreno), nivelDelMar, materialSuelo, transform);
            rios = RiosDelMapa.Construir(artefacto, relieve, materialSuelo, transform);
            niebla = new NieblaDelMapa();
            sesion.AlCambiar += AlCambiar;
            foreach (var k in sesion.Almacen.Ambitos.Keys) AlCambiar(k);

            camara = Camera.main;
            if (camara == null)
            {
                camara = new GameObject("Camara del mapa").AddComponent<Camera>();
                camara.tag = "MainCamera";
                camara.transform.SetParent(transform, false);
            }
            camara.fieldOfView = (float)CamaraDelMapa.Apertura;
            camara.clearFlags = CameraClearFlags.SolidColor;
            camara.backgroundColor = PinturaDelMapa.Niebla;
            RenderSettings.fog = false;
            RenderSettings.skybox = null;
            if (FindAnyObjectByType<Light>() == null)
            {
                var sol = new GameObject("Sol").AddComponent<Light>();
                sol.type = LightType.Directional;
                sol.transform.SetParent(transform, false);
                sol.transform.rotation = Quaternion.Euler(50, 30, 0);
            }

            caja = Caja("Tu columna", PinturaDelMapa.Columna, new Vector3(PinturaDelMapa.LadoColumna, PinturaDelMapa.AltoColumna, PinturaDelMapa.LadoColumna));
            destino = Caja("Destino", PinturaDelMapa.Destino, new Vector3(0.5f, 1.5f, 0.5f));
            ruta = new GameObject("Ruta").AddComponent<LineRenderer>();
            ruta.transform.SetParent(transform, false);
            ruta.sharedMaterial = materialPropio;
            ruta.startColor = ruta.endColor = PinturaDelMapa.Ruta.linear;
            ruta.widthMultiplier = 0.25f * EscalaDelMapa.MetrosPorUnidad;
            ruta.shadowCastingMode = ShadowCastingMode.Off;
        }

        GameObject Caja(string nombre, Color color, Vector3 tamanoU)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = nombre;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localScale = tamanoU * EscalaDelMapa.MetrosPorUnidad;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = materialPropio;
            var p = new MaterialPropertyBlock();
            p.SetColor("_Color", color);
            r.SetPropertyBlock(p);
            r.shadowCastingMode = ShadowCastingMode.Off;
            go.SetActive(false);
            return go;
        }

        void AlCambiar(string ambito)
        {
            if (!ambito.StartsWith("VISTA:", StringComparison.Ordinal)) return;
            niebla.Poner(sesion.Almacen.Ambitos.TryGetValue(ambito, out var a) ? a.Niebla : null);
        }

        void OnDestroy()
        {
            if (sesion != null) sesion.AlCambiar -= AlCambiar;
            terreno?.Destruir();
            niebla?.Destruir();
            if (rios != null) Destroy(rios.GetComponent<MeshFilter>().sharedMesh);
            if (materialSuelo != null) Destroy(materialSuelo);
            if (materialPropio != null) Destroy(materialPropio);
        }

        void Update()
        {
            if (sesion == null) return;
            var ahora = sesion.AhoraMundo;
            Situar(ahora);
            if (Centro == null) return;
            var (cx, cy) = Centro.Value;
            Entrada();
            var objetivo = (cx, terreno.AlturaVisible(cx, cy) / EscalaDelMapa.MetrosPorUnidad, cy);
            var pos = Camara.Colocar(objetivo, camara.aspect, (x, y) => terreno.AlturaVisible(x, y) / EscalaDelMapa.MetrosPorUnidad, Time.deltaTime);
            var e = EscalaDelMapa.MetrosPorUnidad;
            camara.transform.position = EscalaDelMapa.AEscena(pos.x, pos.y, pos.h * e);
            camara.transform.LookAt(EscalaDelMapa.AEscena(cx, cy, objetivo.Item2 * e));
            camara.nearClipPlane = (float)Math.Max(0.05, Camara.Distancia * 0.05) * e;
            camara.farClipPlane = (float)Math.Max(600, Camara.Distancia * 20) * e;
            terreno.Actualizar(cx, cy, (float)Camara.Distancia * 7);
        }

        /// <summary>Dónde está el héroe en `ahora`: su columna (con su plan al instante), o el campamento donde está dentro. Y su ruta y destino.</summary>
        void Situar(double ahora)
        {
            var id = heroeId();
            var heroe = id == null ? null : sesion.Almacen.Buscar(TipoEntidad.Heroe, id)?.Heroe;
            Columna = null;
            Centro = null;
            if (heroe?.Ubicacion == null) { Mostrar(false); return; }
            if (heroe.Ubicacion.DondeCase == UbicacionHeroe.DondeOneofCase.EjercitoId)
                Columna = sesion.Almacen.Buscar(TipoEntidad.Ejercito, heroe.Ubicacion.EjercitoId)?.Ejercito;
            if (Columna != null)
            {
                var p = Instante.Posicion(Columna.Plan, Columna.Posicion, ahora);
                Centro = (p.x / 1000, p.y / 1000);
                Camara.RadioVision = RadioDeVision(Columna);
            }
            else if (heroe.Ubicacion.DondeCase == UbicacionHeroe.DondeOneofCase.CampamentoId)
            {
                var c = sesion.Almacen.Buscar(TipoEntidad.CampamentoMercenarios, heroe.Ubicacion.CampamentoId)?.CampamentoMercenarios;
                if (c != null) Centro = (c.Posicion.X / 1000.0, c.Posicion.Y / 1000.0);
                Camara.RadioVision = datos.Numero("documentos.Movimiento_Ejercitos.VISION.jugadorSolo");
            }
            if (!vistaPedida && Centro != null)
            {
                // La VISTA del mundo entero, como BronzeAgeClient: el servidor ya la filtra por la niebla. La sesión la vuelve a pedir al reconectar.
                vistaPedida = true;
                sesion.CambiarVista(new Rectangulo { Min = new Punto(), Max = new Punto { X = relieve.Columnas * (long)(relieve.Paso * 1000), Y = relieve.Filas * (long)(relieve.Paso * 1000) } });
            }
            Mostrar(Columna != null);
            if (Columna == null) return;
            var (x, y) = Centro.Value;
            var e = EscalaDelMapa.MetrosPorUnidad;
            caja.transform.localPosition = EscalaDelMapa.AEscena(x, y, terreno.AlturaVisible(x, y) + PinturaDelMapa.AltoColumna * e / 2);
            var resto = Instante.RutaRestante(Columna.Plan, Columna.Posicion, ahora);
            destino.SetActive(resto.Count > 1);
            ruta.enabled = resto.Count > 1;
            if (resto.Count <= 1) return;
            var (dx, dy) = (resto[resto.Count - 1].x / 1000, resto[resto.Count - 1].y / 1000);
            destino.transform.localPosition = EscalaDelMapa.AEscena(dx, dy, terreno.AlturaVisible(dx, dy) + 0.75f * e);
            var puntos = new List<Vector3>();
            for (int i = 0; i + 1 < resto.Count; i++)
            {
                double ax = resto[i].x / 1000, ay = resto[i].y / 1000, bx = resto[i + 1].x / 1000, by = resto[i + 1].y / 1000;
                int pasos = Math.Max(1, (int)Math.Ceiling(Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay)) / 2));
                for (int k = 0; k < pasos; k++)
                {
                    double t = k / (double)pasos, px = ax + (bx - ax) * t, py = ay + (by - ay) * t;
                    puntos.Add(EscalaDelMapa.AEscena(px, py, terreno.AlturaVisible(px, py) + 0.3f * e));
                }
            }
            puntos.Add(EscalaDelMapa.AEscena(dx, dy, terreno.AlturaVisible(dx, dy) + 0.3f * e));
            ruta.positionCount = puntos.Count;
            ruta.SetPositions(puntos.ToArray());
        }

        void Mostrar(bool columna)
        {
            caja.SetActive(columna);
            if (!columna) { destino.SetActive(false); ruta.enabled = false; }
        }

        /// <summary>La visión de la columna (`Movimiento_Ejercitos.VISION` de `/v1/datos`): con soldados, la de ejército; si no, la del héroe solo.</summary>
        double RadioDeVision(Ejercito columna)
        {
            foreach (var e in sesion.Almacen.Todas(Entidad.CuerpoOneofCase.Escuadron))
                if (e.Escuadron.Cantidad > 0 && e.Escuadron.Contenedor?.EjercitoId == columna.Id)
                    return datos.Numero("documentos.Movimiento_Ejercitos.VISION.ejercito");
            return datos.Numero("documentos.Movimiento_Ejercitos.VISION.jugadorSolo");
        }

        /// <summary>Arrastrar gira e inclina la cámara; un clic sin arrastre es un clic en el suelo; la rueda pasa de un zoom al siguiente; las
        /// flechas o WASD giran e inclinan (BronzeAgeClient: `src/mundo3d/entrada.ts`).</summary>
        void Entrada()
        {
            var raton = Mouse.current;
            var teclado = Keyboard.current;
            var dt = Time.deltaTime;
            if (teclado != null)
            {
                if (teclado.upArrowKey.isPressed || teclado.wKey.isPressed) Camara.Inclinar(40 * dt);
                if (teclado.downArrowKey.isPressed || teclado.sKey.isPressed) Camara.Inclinar(-40 * dt);
                if (teclado.leftArrowKey.isPressed || teclado.aKey.isPressed) Camara.Giro += 1.4 * dt;
                if (teclado.rightArrowKey.isPressed || teclado.dKey.isPressed) Camara.Giro -= 1.4 * dt;
            }
            if (raton == null) return;
            var p = raton.position.ReadValue();
            if (raton.leftButton.wasPressedThisFrame && !sobreInterfaz(p)) { pulsado = p; ultimoRaton = p; }
            if (pulsado != null && raton.leftButton.isPressed)
            {
                var d = p - ultimoRaton;
                Camara.Giro -= d.x * 0.006;
                Camara.Inclinar(-d.y * 0.25);
                ultimoRaton = p;
            }
            if (pulsado != null && raton.leftButton.wasReleasedThisFrame)
            {
                if (Vector2.Distance(pulsado.Value, p) <= ArrastreMinimoPx) _ = Clic(p);
                pulsado = null;
            }
            var rueda = raton.scroll.ReadValue().y;
            if (rueda != 0 && !sobreInterfaz(p) && Time.unscaledTime - ultimaRueda > PausaRuedaSeg)
            {
                ultimaRueda = Time.unscaledTime;
                Camara.PasoDeZoom(rueda < 0 ? 1 : -1);
            }
        }

        /// <summary>El punto del mapa (u) bajo la pantalla, o null.</summary>
        public (double x, double y)? PuntoBajo(Vector2 pantalla)
        {
            var rayo = camara.ScreenPointToRay(pantalla);
            var o = EscalaDelMapa.AlMapa(rayo.origin);
            var d = EscalaDelMapa.DireccionAlMapa(rayo.direction);
            return CamaraDelMapa.TocarRelieve(o, d, (x, y) => terreno.AlturaVisible(x, y) / EscalaDelMapa.MetrosPorUnidad);
        }

        /// <summary>Un clic en el suelo: la columna marcha allí; desde dentro de un campamento, se sale hacia allí.</summary>
        public async Task Clic(Vector2 pantalla)
        {
            var punto = PuntoBajo(pantalla);
            if (punto == null) return;
            await MarcharA(punto.Value.x, punto.Value.y);
        }

        public async Task<ResultadoOrden> MarcharA(double x, double y)
        {
            var id = heroeId();
            var heroe = id == null ? null : sesion.Almacen.Buscar(TipoEntidad.Heroe, id)?.Heroe;
            if (heroe?.Ubicacion == null) return null;
            var p = new Punto { X = (long)Math.Round(x * 1000), Y = (long)Math.Round(y * 1000) };
            // Desde dentro de un campamento se sale primero (el servidor aún no acepta salir con destino: `objetivo.no_soportado`) y luego se marcha.
            if (heroe.Ubicacion.DondeCase == UbicacionHeroe.DondeOneofCase.CampamentoId)
            {
                var salida = await Dar(new Orden { SalirDelCampamento = new OrdenSalirDelCampamento { CampamentoId = heroe.Ubicacion.CampamentoId, HeroeId = id } });
                if (salida?.Resultado != Resultado.OrdenAceptada) return salida;
            }
            else if (heroe.Ubicacion.DondeCase != UbicacionHeroe.DondeOneofCase.EjercitoId) return null;
            return await Dar(new Orden { MarcharA = new OrdenMarcharA { HeroeId = id, Objetivo = new OrdenMarcharA.Types.Objetivo { Punto = p } } });
        }

        /// <summary>Pone o quita la marcha forzada de tu columna (más deprisa, más víveres).</summary>
        public async Task<ResultadoOrden> AlternarMarchaForzada()
        {
            if (Columna == null) return null;
            return await Dar(new Orden { MarchaForzada = new OrdenMarchaForzada { EjercitoId = Columna.Id, Activa = !Columna.MarchaForzada } });
        }

        async Task<ResultadoOrden> Dar(Orden orden)
        {
            try
            {
                var r = await sesion.Ordenar(orden);
                if (r.Resultado != Resultado.OrdenAceptada) avisar(Textos.Explicar(r.Rechazo), false);
                return r;
            }
            catch (ErrorDeSesion e)
            {
                avisar(e.Message, false);
                return null;
            }
        }

        /// <summary>Teselas de suelo hechas y si ya está todo lo que se ve (para las pruebas y el diagnóstico).</summary>
        public int TeselasCargadas => terreno?.Cargadas ?? 0;
        public bool SueloCompleto => terreno != null && terreno.Completo;
        public Camera Vista => camara;
    }
}
