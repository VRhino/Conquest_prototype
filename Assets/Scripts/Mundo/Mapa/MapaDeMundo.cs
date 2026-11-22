// El mapa de mundo con cuadros (paso 2 del bloque 4): el suelo por teselas, el agua, los ríos y la niebla, la cámara de BronzeAgeClient, tu
// columna como caja con su posición al instante del reloj, su ruta y su destino, y el clic en el suelo para marchar. Es interfaz de fuera de
// la batalla: no usa el ECS de la batalla ni `Conquest.Net`; lee el almacén de la sesión y da órdenes del contrato.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BronzeAge.Contract.V1;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

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
        VegetacionDelMapa vegetacion;
        NieblaDelMapa niebla;
        GameObject rios, caja, destino;
        LineRenderer ruta;
        Material materialSuelo, materialPropio, cielo;
        Vector2? pulsado;
        Vector2 ultimoRaton;
        float ultimaRueda = -1;
        bool vistaPedida;
        ArtefactoDeMundo artefacto;
        CosasEnElMapa pintor;
        TiempoDeMarcha tiempo;
        bool sucio = true;
        float proximasRayas;
        (double x, double y) seleccionVista;
        (string id, double x, double y, double coste)? tiempoGuardado;

        /// <summary>Todo lo que hay en el mapa este fotograma (sin tu columna).</summary>
        public List<CosaDelMapa> Cosas { get; private set; } = new List<CosaDelMapa>();
        public CosaDelMapa Seleccionada { get; private set; }
        /// <summary>Con dos o más cosas bajo el clic: la lista para elegir. Mientras está abierta, un clic en el mapa solo la cierra.</summary>
        public event Action<List<CosaDelMapa>> AlElegir;
        public event Action AlCerrarLista;
        public event Action<CosaDelMapa> AlSeleccionar;
        public bool ListaAbierta;
        /// <summary>Con una ventana a pantalla completa encima (la Base): ni ratón ni teclado mueven la cámara.</summary>
        public bool EntradaApagada;
        /// <summary>Capas: los nombres de los nodos de recursos y la fertilidad del suelo.</summary>
        public bool NombresDeNodos = true;
        /// <summary>Sin héroe (al crearlo): dónde mira el mapa, con qué visión, y lo que se pinta además de lo del almacén (los campamentos
        /// donde nacer, los bandidos del artefacto, los aros de alrededor). Tras cambiar las rayas, `RehacerRayas`.</summary>
        public (double x, double y)? Enfoque;
        public double? RadioVisionFijo;
        public readonly List<CosaDelMapa> Adicionales = new List<CosaDelMapa>();
        public readonly List<RayaDelMapa> RayasAdicionales = new List<RayaDelMapa>();
        public void RehacerRayas() => sucio = true;
        public bool Fertilidad { get => terreno.ConFertilidad; set => terreno.ConFertilidad = value; }

        /// <summary>Donde está el héroe ahora (u) y si lo que se sigue es su columna.</summary>
        public (double x, double y)? Centro { get; private set; }
        public Ejercito Columna { get; private set; }

        /// <summary>Monta el mapa. `sobreInterfaz`: si el puntero está sobre la interfaz (entonces el clic no es del mapa); `avisar`: un aviso
        /// flotante (texto, peligro).</summary>
        public void Iniciar(Sesion sesion, Relieve relieve, ArtefactoDeMundo artefacto, DatosDeJuego datos, Func<string> heroeId,
            Func<Vector2, bool> sobreInterfaz, Action<string, bool> avisar, VisualElement capaNombres)
        {
            this.artefacto = artefacto;
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

            float Umbral(string nombre) => (float)(datos.Numero("documentos.Generacion_Del_Mundo.ELEVACION." + nombre) * Relieve.MetrosAltura);
            terreno = new TerrenoDelMapa(relieve, Raster(artefacto.Fertilidad), Raster(artefacto.Humedad), Raster(artefacto.DensidadBosque), Umbral("umbralAgua"),
                new Vector4(Umbral("umbralCosta"), Umbral("umbralColina"), Umbral("umbralMontana"), Umbral("umbralCima")), transform);
            pintor = new CosasEnElMapa((x, y) => terreno.AlturaVisible(x, y), materialSuelo, materialPropio, transform, capaNombres);
            if (artefacto.Navegacion != null && artefacto.Navegacion.Nodos.Count > 0) tiempo = new TiempoDeMarcha(artefacto.Navegacion);
            terreno.Cauces = new CaucesDelMapa(artefacto, relieve);
            // La vegetación: donde el suelo pinta sotobosque, con los mismos umbrales (arena, cima, nieve) que el suelo.
            var umbrales = new Vector4(Umbral("umbralCosta"), Umbral("umbralColina"), Umbral("umbralMontana"), Umbral("umbralCima"));
            vegetacion = new VegetacionDelMapa(relieve, Raster(artefacto.Fertilidad), Raster(artefacto.Humedad), Raster(artefacto.DensidadBosque), umbrales,
                umbrales.w + PinturaDelMapa.NieveSobreCima * ((float)Relieve.MetrosAltura - umbrales.w)) { Cauces = terreno.Cauces, NivelDelAgua = Umbral("umbralAgua") };
            // Los ríos, con la orilla mojada pegada al suelo ya excavado por su cauce.
            var cauces = terreno.Cauces;
            rios = RiosDelMapa.Construir(artefacto, relieve, (x, y) =>
            {
                var h = (float)relieve.AlturaEn(x, y);
                return cauces.Fondo(x, y, 0.5f) is float f ? Mathf.Min(h, f) : h;
            }, transform, cauces);
            niebla = new NieblaDelMapa();
            // los nodos de recursos (tema 5): el agua, la del relieve y los cauces (el parche y los cascotes no van encima)
            nodos = new NodosDelMapa(terreno, artefacto.Nodos, (x, y) => relieve.AguaEn(x, y) || (cauces.ACauce(x, y, 2) < 0.15), terreno.Material);
            // las maquetas de los asentamientos (tema 6); la capa baja ralea junto a los nodos y junto a las miniaturas
            maquetas = new MaquetasDelMapa(terreno);
            // (los pueblos, sobre su nodo: dónde está y de qué tipo, y el color de su recurso para el mineral de las minas)
            var nodosPorId = artefacto.Nodos.Where(k => k.Posicion != null && !string.IsNullOrEmpty(k.Id)).GroupBy(k => k.Id).ToDictionary(g => g.Key, g => g.First());
            maquetas.NodoPorId = id => nodosPorId.TryGetValue(id, out var k) ? (k.Posicion.X / 1000.0, k.Posicion.Y / 1000.0, k.Tipo) : ((double, double, string)?)null;
            maquetas.ColorDeRecurso = tipo => nodos.ColorDe(tipo);
            maquetas.PosicionesDeNodos = artefacto.Nodos.Where(k => k.Posicion != null).Select(k => (k.Posicion.X / 1000.0, k.Posicion.Y / 1000.0)).ToList();
            var m_ = maquetas;
            vegetacion.Ralo = (x, y) => Math.Min(nodos.Ralo(x, y), m_.Ralo(x, y));
            if (PinturaDelMapa.CaminosYTerritorio)
            {
                trazos = new TrazosEnElMapa(transform, terreno, relieve);
                territorio = new TerritorioDelMapa();
            }
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
            // El cielo de BronzeAgeClient (`cielo.ts`) de fondo; la bruma lejana de los sombreados del mapa acaba en su horizonte.
            cielo = new Material(Resources.Load<Shader>("MapaMundo/CieloDelMapa")) { name = "cielo del mapa" };
            cielo.SetColor("_Cenit", PinturaDelMapa.Cenit);
            cielo.SetColor("_Horizonte", PinturaDelMapa.Horizonte);
            cielo.SetColor("_Suelo", PinturaDelMapa.BajoElHorizonte);
            cielo.SetVector("_Sol", PinturaDelMapa.Sol);
            // El mismo cielo, para el reflejo del agua (MapaComun.hlsl).
            Shader.SetGlobalVector("_CieloCenit", PinturaDelMapa.Cenit.linear);
            Shader.SetGlobalVector("_CieloHorizonte", PinturaDelMapa.Horizonte.linear);
            Shader.SetGlobalVector("_CieloSuelo", PinturaDelMapa.BajoElHorizonte.linear);
            Shader.SetGlobalVector("_CieloSol", PinturaDelMapa.Sol);
            camara.clearFlags = CameraClearFlags.Skybox;
            camara.backgroundColor = PinturaDelMapa.Horizonte;
            RenderSettings.fog = false;
            RenderSettings.skybox = cielo;
            if (FindAnyObjectByType<Light>() == null)
            {
                // El sol, donde lo pinta el cielo: la luz del suelo viene de ahí.
                sol = new GameObject("Sol").AddComponent<Light>();
                sol.type = LightType.Directional;
                sol.transform.SetParent(transform, false);
                sol.transform.rotation = Quaternion.LookRotation(-PinturaDelMapa.Sol);
                sol.color = new Color32(0xff, 0xf0, 0xd8, 255);
                sol.shadows = PinturaDelMapa.SombrasReales ? LightShadows.Soft : LightShadows.None;
            }
            Shader.SetGlobalFloat(SombrasReales, PinturaDelMapa.SombrasReales && sol != null ? 1 : 0);
            // Con el sol más bajo que el del prototipo, el llano recibe menos (N·L = seno de la elevación): la directa se compensa para que el
            // llano a pleno sol quede con la luminancia de siempre.
            Shader.SetGlobalFloat("_CompensaSol", PinturaDelMapa.SolDelPrototipo.y / PinturaDelMapa.Sol.y);
            if (PinturaDelMapa.SombrasReales && sol != null) PonerPipelineDelMapa();

            caja = Caja("Tu columna", PinturaDelMapa.Columna, new Vector3(PinturaDelMapa.LadoColumna, PinturaDelMapa.AltoColumna, PinturaDelMapa.LadoColumna));
            destino = Caja("Destino", PinturaDelMapa.Destino, new Vector3(0.5f, 1.5f, 0.5f));
            ruta = new GameObject("Ruta").AddComponent<LineRenderer>();
            ruta.transform.SetParent(transform, false);
            ruta.sharedMaterial = materialPropio;
            ruta.startColor = ruta.endColor = PinturaDelMapa.Ruta.linear;
            ruta.widthMultiplier = 0.25f * EscalaDelMapa.MetrosPorUnidad;
            ruta.shadowCastingMode = ShadowCastingMode.Off;
        }

        static RasterDeMundo Raster(Raster r) => r != null && r.Encoding == EncodingRaster.EncodingU8 ? new RasterDeMundo(r) : null;

        // ── la vista previa de fundar (duda 38, tanda 2; `fundar` de `mundo3d/marcas.ts` del prototipo) ──

        /// <summary>Lo que se pinta mientras el panel de fundar está abierto: la zona con que nacería la plaza (su aro y su centro), los sitios de aldea
        /// (cuadros del paso de la rejilla con que se buscaron: verdes los válidos, grises los que quedan demasiado cerca) y las parcelas de bosque de
        /// la zona (un aro de 2 u: verde libre, rojo ocupada o de otra plaza). En unidades del mapa.</summary>
        public sealed class FundacionEnVista
        {
            public double X, Y, Radio, Paso;
            public List<(double x, double y)> Validos = new List<(double, double)>(), Cerca = new List<(double, double)>();
            public List<(double x, double y, bool libre)> Parcelas = new List<(double, double, bool)>();
        }

        static readonly Color ColorFundacion = new Color32(0xf1, 0xd3, 0x8b, 0xff), ColorValido = new Color32(0x46, 0xc8, 0x5a, 0xff),
            ColorCerca = new Color32(0xa0, 0xa0, 0x96, 0xff), ColorLibre = new Color32(0x78, 0xdc, 0x6e, 0xff), ColorOcupada = new Color32(0xc8, 0x5a, 0x46, 0xff);
        FundacionEnVista fundacion;
        GameObject marcasDeFundar;

        /// <summary>La vista previa de fundar; null la quita. Se rehace solo al cambiar el objeto (quien la da hace uno nuevo si cambia el sitio).</summary>
        public FundacionEnVista Fundacion
        {
            get => fundacion;
            set { if (ReferenceEquals(value, fundacion)) return; fundacion = value; PintarFundacion(); }
        }

        void PintarFundacion()
        {
            if (marcasDeFundar != null) Destroy(marcasDeFundar);
            marcasDeFundar = null;
            var f = fundacion;
            if (f == null || terreno == null) return;
            marcasDeFundar = new GameObject("Fundar: vista previa");
            marcasDeFundar.transform.SetParent(transform, false);
            AroEnElSuelo(f.X, f.Y, f.Radio, 0.12f, ColorFundacion);
            AroEnElSuelo(f.X, f.Y, 0.25, 0.08f, ColorFundacion);
            CuadrosEnElSuelo("Sitios de aldea", f.Validos, f.Paso * 0.85, ColorValido);
            CuadrosEnElSuelo("Demasiado cerca", f.Cerca, f.Paso * 0.85, ColorCerca);
            foreach (var (x, y, libre) in f.Parcelas) AroEnElSuelo(x, y, 2, 0.12f, libre ? ColorLibre : ColorOcupada);
        }

        /// <summary>Un aro sobre el terreno, de radio `r` y ancho `ancho` (u).</summary>
        void AroEnElSuelo(double cx, double cy, double r, float ancho, Color color)
        {
            var e = EscalaDelMapa.MetrosPorUnidad;
            var l = new GameObject("Aro").AddComponent<LineRenderer>();
            l.transform.SetParent(marcasDeFundar.transform, false);
            l.sharedMaterial = materialPropio;
            l.startColor = l.endColor = color.linear;
            l.widthMultiplier = ancho * e;
            l.shadowCastingMode = ShadowCastingMode.Off;
            l.loop = true;
            l.useWorldSpace = false;
            var n = Math.Max(24, (int)(r * 6));
            var puntos = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                double x = cx + r * Math.Cos(2 * Math.PI * i / n), y = cy + r * Math.Sin(2 * Math.PI * i / n);
                puntos[i] = EscalaDelMapa.AEscena(x, y, terreno.AlturaVisible(x, y) + 0.15f * e);
            }
            l.positionCount = n;
            l.SetPositions(puntos);
        }

        /// <summary>Cuadros planos de lado `lado` (u) sobre el terreno, en una sola malla.</summary>
        void CuadrosEnElSuelo(string nombre, List<(double x, double y)> centros, double lado, Color color)
        {
            if (centros.Count == 0) return;
            var e = EscalaDelMapa.MetrosPorUnidad;
            var v = new List<Vector3>(centros.Count * 4);
            var t = new List<int>(centros.Count * 12);
            var cs = new List<Color>(centros.Count * 4);
            var h = lado / 2;
            foreach (var (x, y) in centros)
            {
                var alto = terreno.AlturaVisible(x, y) + 0.06f * e;
                var k = v.Count;
                v.Add(EscalaDelMapa.AEscena(x - h, y - h, alto));
                v.Add(EscalaDelMapa.AEscena(x - h, y + h, alto));
                v.Add(EscalaDelMapa.AEscena(x + h, y + h, alto));
                v.Add(EscalaDelMapa.AEscena(x + h, y - h, alto));
                // Por las dos caras (la y del mapa va invertida en la escena, z = −y, y el sombreado descarta las de detrás).
                t.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3, k, k + 2, k + 1, k, k + 3, k + 2 });
                for (int i = 0; i < 4; i++) cs.Add(color.linear);
            }
            var malla = new Mesh { name = nombre, indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            malla.SetVertices(v);
            malla.SetTriangles(t, 0);
            malla.SetColors(cs);
            malla.SetNormals(Enumerable.Repeat(Vector3.up, v.Count).ToList()); // hacia arriba: con las dos caras, recalcularlas las anularía
            var go = new GameObject(nombre);
            go.transform.SetParent(marcasDeFundar.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = malla;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = materialPropio;
            r.shadowCastingMode = ShadowCastingMode.Off;
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
            r.shadowCastingMode = nombre == "Tu columna" && PinturaDelMapa.SombrasReales ? ShadowCastingMode.On : ShadowCastingMode.Off;
            go.SetActive(false);
            return go;
        }

        void AlCambiar(string ambito)
        {
            sucio = true;
            if (!ambito.StartsWith("VISTA:", StringComparison.Ordinal)) return;
            niebla.Poner(sesion.Almacen.Ambitos.TryGetValue(ambito, out var a) ? a.Niebla : null);
        }

        Light sol;
        // El asset de URP es de todo el proyecto (la batalla y el feudo lo comparten): el mapa trabaja con una copia en memoria con sus sombras
        // (distancia según el zoom, 3 cascadas) y al irse devuelve el original. En disco no cambia nada.
        RenderPipelineAsset pipelineOriginal;
        UniversalRenderPipelineAsset pipelineDelMapa;
        static readonly int SombrasReales = Shader.PropertyToID("_SombrasReales"), SombraHasta = Shader.PropertyToID("_SombraHasta");

        void PonerPipelineDelMapa()
        {
            if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset activo) return;
            pipelineOriginal = QualitySettings.renderPipeline;
            pipelineDelMapa = Instantiate(activo);
            pipelineDelMapa.name = activo.name + " (mapa)";
            pipelineDelMapa.shadowCascadeCount = 3;
            // La cámara mira desde 1 vez su distancia: lo cercano de la pantalla empieza hacia el 60 % de la distancia de sombra (1,6 veces la de
            // la cámara). La primera cascada llega hasta el héroe y algo más (nítida de cerca); la segunda, a la mitad de arriba; la tercera, al fondo.
            pipelineDelMapa.cascade3Split = new Vector2(0.62f, 0.8f);
            pipelineDelMapa.shadowDepthBias = 1;
            pipelineDelMapa.shadowNormalBias = 1.5f; // contra el acné en las copas
            QualitySettings.renderPipeline = pipelineDelMapa;
        }

        void QuitarPipelineDelMapa()
        {
            if (pipelineDelMapa == null) return;
            if (QualitySettings.renderPipeline == pipelineDelMapa) QualitySettings.renderPipeline = pipelineOriginal;
            Destroy(pipelineDelMapa);
            pipelineDelMapa = null;
        }

        /// <summary>Para las pruebas: si el mapa trabaja con su copia del asset de URP.</summary>
        public bool ConPipelineDelMapa => pipelineDelMapa != null && QualitySettings.renderPipeline == pipelineDelMapa;

        void OnDisable() => QuitarPipelineDelMapa();

        void OnEnable()
        {
            if (sol != null && PinturaDelMapa.SombrasReales && pipelineDelMapa == null) PonerPipelineDelMapa();
        }

        void OnDestroy()
        {
            QuitarPipelineDelMapa();
            Shader.SetGlobalFloat(SombrasReales, 0);
            if (sesion != null) sesion.AlCambiar -= AlCambiar;
            terreno?.Destruir();
            vegetacion?.Destruir();
            trazos?.Destruir();
            nodos?.Destruir();
            maquetas?.Destruir();
            territorio?.Destruir();
            pintor?.Destruir();
            niebla?.Destruir();
            if (rios != null)
                foreach (var r in rios.GetComponentsInChildren<MeshRenderer>())
                {
                    Destroy(r.GetComponent<MeshFilter>().sharedMesh);
                    Destroy(r.sharedMaterial);
                }
            if (materialSuelo != null) Destroy(materialSuelo);
            if (materialPropio != null) Destroy(materialPropio);
            if (cielo != null)
            {
                if (RenderSettings.skybox == cielo) RenderSettings.skybox = null;
                Destroy(cielo);
            }
            Shader.SetGlobalVector(Bruma, Vector4.zero);
        }

        static readonly int Bruma = Shader.PropertyToID("_Bruma"), ColorBruma = Shader.PropertyToID("_BrumaColor");

        void Update()
        {
            if (sesion == null) return;
            var ahora = sesion.AhoraMundo;
            Situar(ahora);
            if (Centro == null) return;
            var (cx, cy) = Centro.Value;
            if (!EntradaApagada) Entrada();
            var objetivo = (cx, terreno.AlturaVisible(cx, cy) / EscalaDelMapa.MetrosPorUnidad, cy);
            var pos = Camara.Colocar(objetivo, camara.aspect, (x, y) => terreno.AlturaVisible(x, y) / EscalaDelMapa.MetrosPorUnidad, Time.deltaTime);
            var e = EscalaDelMapa.MetrosPorUnidad;
            camara.transform.position = EscalaDelMapa.AEscena(pos.x, pos.y, pos.h * e);
            camara.transform.LookAt(EscalaDelMapa.AEscena(cx, cy, objetivo.Item2 * e));
            camara.nearClipPlane = (float)Math.Max(0.05, Camara.Distancia * 0.05) * e;
            camara.farClipPlane = (float)Math.Max(600, Camara.Distancia * 20) * e;
            terreno.Actualizar(cx, cy, (float)Camara.Distancia * 7);
            terreno.HornearRelieve(1.0); // la sombra del relieve: 1 ms por fotograma como mucho
            vegetacion.Actualizar(cx, cy, Mathf.Min((float)Camara.Distancia * 7, 130));
            PintarCosas(ahora, cx, cy);
            // los nodos de recursos (tema 5), hasta donde se carga, como las cajas
            nodos.Actualizar(cx, cy, Math.Min(PinturaDelMapa.DistanciaCarga, Camara.Distancia * 7));
            nodos.Dibujar(camara);
            vegetacion.Claros(Claros());
            vegetacion.Aclarado(AclararCopas ? Aclarados(cx, cy) : SinAclarar);
            vegetacion.Dibujar(camara, cx, cy);
            if (trazos != null)
            {
                // la caja de las rayas: lo que se carga, con el tope del prototipo (240 u con la distancia de carga de 320)
                double alcance = Math.Min(PinturaDelMapa.DistanciaCarga, Camara.Distancia * 7);
                trazos.Actualizar(cx, cy, Math.Min(alcance, 240.0 * PinturaDelMapa.DistanciaCarga / 320));
                territorio.Actualizar(cx, cy);
            }
            if (pipelineDelMapa != null)
            {
                // La sombra llega a 1,6 veces la distancia de la cámara (el radio del prototipo), hasta 700 m.
                float hasta = Mathf.Min((float)(Camara.Distancia * e * 1.6), 700);
                pipelineDelMapa.shadowDistance = hasta;
                Shader.SetGlobalFloat(SombraHasta, hasta);
            }
        }

        /// <summary>Lo que hay en el mapa: las cajas cada fotograma (lo que anda, en su sitio del instante), las rayas cuando cambia algo (como
        /// mucho dos veces por segundo), los nombres a la distancia de la bruma de BronzeAgeClient y el aro de lo seleccionado.</summary>
        void PintarCosas(double ahora, double cx, double cy)
        {
            var radioInicial = datos.Numero("reglas.territorio.radio_inicial") / 1000;
            Cosas = CosasDelMapa.Reunir(sesion.Almacen, heroeId(), ahora, artefacto.Nodos, radioInicial);
            Cosas.AddRange(Adicionales);
            var d = Camara.Distancia;
            double lejosCarga = Math.Min(PinturaDelMapa.DistanciaCarga, d * 7), cerca = Math.Min(d * 2.2, lejosCarga * 0.4);
            // La bruma lejana (la `THREE.Fog` de BronzeAgeClient): del horizonte, empieza en `cerca` y acaba donde acaba lo cargado.
            var e = EscalaDelMapa.MetrosPorUnidad;
            Shader.SetGlobalVector(Bruma, new Vector4((float)(cerca * e), (float)(lejosCarga * e), 1, e));
            Shader.SetGlobalColor(ColorBruma, PinturaDelMapa.Horizonte);
            pintor.Pintar(Cosas, cx, cy, lejosCarga);
            maquetas.Actualizar(Cosas, heroeId(), radioInicial);
            nodos.GirosDePueblo = maquetas.GirosDeNodos;
            nodos.VersionGiros = maquetas.VersionGiros;
            maquetas.Dibujar(camara);
            pintor.Nombres(Cosas, camara, cx, cy, (cerca + lejosCarga) / 2, NombresDeNodos, Camara.EnPantalla * 0.75);
            if (sucio && Time.unscaledTime >= proximasRayas)
            {
                sucio = false;
                proximasRayas = Time.unscaledTime + 0.5f;
                var radioProteccion = datos.Numero("documentos.Campamentos_Mercenarios.MERCENARIOS.radioProteccion");
                var rayas = CosasDelMapa.Rayas(sesion.Almacen, heroeId(), radioProteccion, ahora);
                rayas.AddRange(RayasAdicionales);
                // (las plazas giran con la puerta hacia su camino)
                maquetas.Caminos = CosasDelMapa.TramosDeCamino(sesion.Almacen);
                if (trazos != null)
                {
                    // caminos, fronteras, zonas y el anillo de los mercenarios, como el prototipo (`TrazosEnElMapa`, `TerritorioDelMapa`); aquí quedan
                    // las demás rayas (las Miradas)
                    rayas.RemoveAll(r => r.Clase == ClaseDeRaya.Camino || r.Clase == ClaseDeRaya.ZonaDeFaccion || r.Clase == ClaseDeRaya.ZonaDePlaza
                                         || r.Color == CosasDelMapa.AnilloMercenarios);
                    var (lista, rellenos) = TrazosYTerritorio.Reunir(sesion.Almacen, heroeId(), radioProteccion, terreno.Cauces?.Vados);
                    trazos.Fijar(lista);
                    territorio.Fijar(rellenos);
                    vegetacion.Caminos(lista.Where(t => t.Tipo == TipoDeTrazo.Camino)
                        .Select(t => ((IReadOnlyList<(double x, double y)>)t.Puntos, t.Ancho)).ToList());
                }
                pintor.Rayas(rayas);
            }
            if (Seleccionada == null) return;
            var ahoraSel = Cosas.Find(c => c.Id == Seleccionada.Id && c.Clase == Seleccionada.Clase);
            if (ahoraSel == null) { Seleccionar(null); return; }
            Seleccionada = ahoraSel;
            if (CosasDelMapa.Distancia(ahoraSel.X, ahoraSel.Y, seleccionVista.x, seleccionVista.y) > 0.05)
            {
                seleccionVista = (ahoraSel.X, ahoraSel.Y);
                pintor.Seleccion(ahoraSel);
            }
        }

        /// <summary>Los sitios sin árboles alrededor (u): plazas, pueblos, aldeas, avanzadas, obras, campamentos y alijos (como el prototipo).</summary>
        readonly List<(double x, double y, double r)> claros = new List<(double, double, double)>();
        readonly List<Vector4> aclarados = new List<Vector4>();
        readonly List<(double d, Vector4 p)> candidatos = new List<(double, Vector4)>();

        /// <summary>Donde se aclaran las copas: el héroe (tu columna), con un radio que acompaña al zoom para que el hueco ocupe más o menos lo
        /// mismo en pantalla (1,6 u de cerca, 2,2 u normal, 3 u alejado), y las columnas que se ven más cerca de él, hasta 8 en total, a 1,4 u.</summary>
        /// <summary>Si las copas se aclaran alrededor del héroe y de las columnas (las capturas de revisión de las especies lo apagan).</summary>
        public static bool AclararCopas = true;
        static readonly List<Vector4> SinAclarar = new List<Vector4>();

        List<Vector4> Aclarados(double cx, double cy)
        {
            var e = EscalaDelMapa.MetrosPorUnidad;
            candidatos.Clear();
            Vector4 En(double x, double y, double radio) { var p = EscalaDelMapa.AEscena(x, y, terreno.AlturaVisible(x, y) + 4); return new Vector4(p.x, p.y, p.z, (float)(radio * e)); }
            double radioHeroe = Camara.Zoom == ZoomDelMapa.Cerca ? 1.6 : Camara.Zoom == ZoomDelMapa.Alejado ? 3 : 2.2;
            candidatos.Add((-1, En(cx, cy, radioHeroe)));
            foreach (var c in Cosas)
                switch (c.Clase)
                {
                    case ClaseDeCosa.Ejercito: case ClaseDeCosa.EjercitoAjeno: case ClaseDeCosa.Caravana: case ClaseDeCosa.CaravanaAjena:
                    case ClaseDeCosa.Formacion: case ClaseDeCosa.Batalla:
                        double d = (c.X - cx) * (c.X - cx) + (c.Y - cy) * (c.Y - cy);
                        if (d < Camara.EnPantalla * Camara.EnPantalla) candidatos.Add((d, En(c.X, c.Y, 1.4)));
                        break;
                }
            candidatos.Sort((a, b) => a.d.CompareTo(b.d));
            aclarados.Clear();
            for (int i = 0; i < Math.Min(8, candidatos.Count); i++) aclarados.Add(candidatos[i].p);
            return aclarados;
        }

        List<(double x, double y, double r)> Claros()
        {
            var r = claros;
            r.Clear();
            // las maquetas del prototipo (tema 6) dejan sus claros, los del prototipo; con las cajas de antes, los de abajo
            if (maquetas != null && MaquetasDelMapa.Estilo != EstiloDeAsentamientos.Cajas) r.AddRange(maquetas.Claros);
            foreach (var c in Cosas)
                switch (c.Clase)
                {
                    case ClaseDeCosa.Nodo:
                        // «Mejorado»: el parche del nodo, sin árboles ni matas (con la copa de los de fuera)
                        if (NodosDelMapa.Estilo == EstiloDeNodos.Mejorado && NodosDelMapa.ClaroEnLaVegetacion)
                            r.Add((c.X, c.Y, NodosDelMapa.Huella(c.Recurso) * NodosDelMapa.Parche + VegetacionDelMapa.Copa * 0.5));
                        break;
                    case ClaseDeCosa.Plaza: case ClaseDeCosa.Pueblo: case ClaseDeCosa.Aldea: case ClaseDeCosa.Avanzada: case ClaseDeCosa.Obra:
                    case ClaseDeCosa.Mercenarios: case ClaseDeCosa.Bandidos: case ClaseDeCosa.Alijo:
                        if (MaquetasDelMapa.Pinta(c.Clase)) break;
                        // (con las cajas de antes) Como el prototipo (su huella más 0,3 u; 0,25 las obras y los bandidos), pero `Radio` es medio lado de una caja cuadrada:
                        // hasta la esquina, √2 veces; y la copa de un árbol que crece justo fuera no tiene que caer encima.
                        r.Add((c.X, c.Y, Math.Max(c.Radio, 0.5) * 1.415 + (c.Clase == ClaseDeCosa.Obra || c.Clase == ClaseDeCosa.Bandidos ? 0.25 : 0.3)
                            + VegetacionDelMapa.Copa * 0.5));
                        break;
                }
            return r;
        }

        public void Seleccionar(CosaDelMapa c)
        {
            Seleccionada = c;
            if (c != null) seleccionVista = (c.X, c.Y);
            pintor.Seleccion(c);
            AlSeleccionar?.Invoke(c);
        }

        /// <summary>Elegir una cosa (de un clic o de la lista): se selecciona y, si tiene dónde ir, se marcha hacia ella (como BronzeAgeClient).</summary>
        public async Task Elegir(CosaDelMapa c)
        {
            ListaAbierta = false;
            Seleccionar(c);
            switch (c.Clase)
            {
                case ClaseDeCosa.Plaza: await MarcharAPlaza(c.Id); break;
                case ClaseDeCosa.Batalla: case ClaseDeCosa.Ejercito: case ClaseDeCosa.Formacion: case ClaseDeCosa.Alijo:
                case ClaseDeCosa.Mercenarios: case ClaseDeCosa.Bandidos: case ClaseDeCosa.Pueblo: case ClaseDeCosa.Aldea: case ClaseDeCosa.Avanzada:
                    if (Columna != null) await MarcharA(c.X, c.Y);
                    break;
                // Una obra de tu Facción sin terminar: a su puerta, para entregarle los materiales (`pueblosYObrasBajoElClic`).
                case ClaseDeCosa.Obra:
                    var o = c.Entidad?.Obra;
                    var madre = o == null ? null : sesion.Almacen.Buscar(TipoEntidad.Asentamiento, o.AsentamientoMadreId)?.Asentamiento;
                    var yo = heroeId() is string id ? sesion.Almacen.Buscar(TipoEntidad.Heroe, id)?.Heroe : null;
                    if (Columna != null && o != null && !o.HasTerminaEnMs && madre != null && yo != null && yo.HasFaccionId && madre.FaccionId == yo.FaccionId) await MarcharA(c.X, c.Y);
                    break;
            }
        }

        public async Task<ResultadoOrden> MarcharAPlaza(string asentamientoId)
        {
            var id = heroeId();
            if (Columna == null || id == null) return null;
            return await Dar(new Orden { MarcharA = new OrdenMarcharA { HeroeId = id, Objetivo = new OrdenMarcharA.Types.Objetivo { AsentamientoId = asentamientoId } } });
        }

        /// <summary>Lo que hace falta saber del jugador para decidir sus acciones (radios de `/v1/datos`).</summary>
        public CosasDelMapa.Contexto Contexto()
        {
            var id = heroeId();
            var heroe = id == null ? null : sesion.Almacen.Buscar(TipoEntidad.Heroe, id)?.Heroe;
            var j = new CosasDelMapa.Contexto
            {
                HeroeId = id, FaccionId = heroe != null && heroe.HasFaccionId ? heroe.FaccionId : null, ResidenciaId = heroe != null && heroe.HasResidenciaId ? heroe.ResidenciaId : null,
                Columna = Columna, Herido = heroe != null && heroe.HasHeridoHastaMs && heroe.HeridoHastaMs > sesion.AhoraMundo,
                RadioPuerta = datos.RadioPuerta, RadioEncuentro = datos.RadioEncuentro, Ahora = sesion.AhoraMundo,
                RadioInspeccion = datos.Numero("documentos.Movimiento_Ejercitos.MOVIMIENTO.radioInspeccion"),
                RadioProteccion = datos.Numero("documentos.Campamentos_Mercenarios.MERCENARIOS.radioProteccion"),
            };
            if (Columna != null && Centro != null)
            {
                (j.X, j.Y) = Centro.Value;
                j.ConSoldados = TropasDeLaColumna().Count > 0;
                j.Lider = Columna.Participantes.Count <= 1 || Columna.LiderId == id;
                j.Persiguiendo = Columna.Persiguiendo?.PresaId;
                j.EnFormacion = Columna.HasFormacionExpiraEnMs && Columna.FormacionExpiraEnMs > sesion.AhoraMundo;
                j.EnMarcha = Columna.Estado != EstadoColumna.ColumnaEstacionada || j.EnFormacion;
                j.EnBatalla = Columna.EnBatalla;
                j.Personal = Columna.Tipo == TipoColumna.ColumnaPersonal && Columna.Participantes.Count == 1 && !j.EnFormacion;
            }
            j.Batalla = bid => sesion.Almacen.Buscar(TipoEntidad.Batalla, bid)?.Batalla;
            foreach (var c in Cosas) if (c.Clase == ClaseDeCosa.Mercenarios) j.CampamentosDeMercenarios.Add((c.X, c.Y));
            return j;
        }

        List<string> TropasDeLaColumna()
        {
            var r = new List<string>();
            if (Columna == null) return r;
            foreach (var e in sesion.Almacen.Todas(Entidad.CuerpoOneofCase.Escuadron))
                if (e.Escuadron.Cantidad > 0 && e.Escuadron.Contenedor?.EjercitoId == Columna.Id) r.Add(e.Escuadron.TropaId);
            return r;
        }

        /// <summary>Distancia (u) y tiempo de marcha («~X» en tiempo real) de tu columna hasta `c`; null sin columna. El coste se guarda mientras
        /// tu columna no se mueva más de 2 u ni cambie el destino.</summary>
        public (double distancia, string tiempo)? HastaAlli(CosaDelMapa c)
        {
            if (Columna == null || Centro == null) return null;
            var (x, y) = Centro.Value;
            var recto = CosasDelMapa.Distancia(x, y, c.X, c.Y);
            double? coste = recto;
            if (tiempo != null)
            {
                var g = tiempoGuardado;
                if (g != null && g.Value.id == c.Id && CosasDelMapa.Distancia(g.Value.x, g.Value.y, x, y) < 2) coste = g.Value.coste;
                else
                {
                    coste = tiempo.Coste(x, y, c.X, c.Y);
                    tiempoGuardado = coste == null ? ((string, double, double, double)?)null : (c.Id, x, y, coste.Value);
                }
            }
            if (coste == null) return (recto, "sin camino por tierra");
            var v = TiempoDeMarcha.Velocidad(TropasDeLaColumna(), datos, Columna.MarchaForzada);
            return (recto, TiempoDeMarcha.Texto(coste.Value, v, sesion.RelojPausado ? 1 : sesion.FactorReloj));
        }

        /// <summary>Da una orden de la ficha (con el aviso si la rechazan).</summary>
        public Task<ResultadoOrden> Ordenar(Orden orden) => Dar(orden);

        /// <summary>Dónde está el héroe en `ahora`: su columna (con su plan al instante), o el campamento donde está dentro. Y su ruta y destino.</summary>
        void Situar(double ahora)
        {
            var id = heroeId();
            var heroe = id == null ? null : sesion.Almacen.Buscar(TipoEntidad.Heroe, id)?.Heroe;
            Columna = null;
            Centro = null;
            if (heroe?.Ubicacion == null)
            {
                if (Enfoque != null)
                {
                    Centro = Enfoque;
                    Camara.RadioVision = RadioVisionFijo ?? datos.Numero("documentos.Movimiento_Ejercitos.VISION.jugadorSolo");
                }
                Mostrar(false);
                return;
            }
            if (heroe.Ubicacion.DondeCase == UbicacionHeroe.DondeOneofCase.EjercitoId)
                Columna = sesion.Almacen.Buscar(TipoEntidad.Ejercito, heroe.Ubicacion.EjercitoId)?.Ejercito;
            if (Columna != null)
            {
                var p = Instante.Posicion(Columna.Plan, Columna.Posicion, ahora);
                Centro = (p.x / 1000, p.y / 1000);
                Camara.RadioVision = RadioDeVision(Columna);
            }
            else if (DondeEsta(heroe.Ubicacion) is (double, double) dentro)
            {
                Centro = dentro;
                Camara.RadioVision = datos.Numero("documentos.Movimiento_Ejercitos.VISION.jugadorSolo");
            }
            else if (heroe.Ubicacion.DondeCase == UbicacionHeroe.DondeOneofCase.CampamentoId)
            {
                var c = sesion.Almacen.Buscar(TipoEntidad.CampamentoMercenarios, heroe.Ubicacion.CampamentoId)?.CampamentoMercenarios;
                if (c != null) Centro = (c.Posicion.X / 1000.0, c.Posicion.Y / 1000.0);
                Camara.RadioVision = datos.Numero("documentos.Movimiento_Ejercitos.VISION.jugadorSolo");
            }
            // (sin esperar a saber dónde está: dentro de una plaza ajena, su sitio llega precisamente por la VISTA)
            if (!vistaPedida)
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

        /// <summary>Dónde está el lugar en el que está dentro el héroe (plaza, pueblo o avanzada), o null.</summary>
        (double, double)? DondeEsta(UbicacionHeroe u)
        {
            Punto p = null;
            switch (u.DondeCase)
            {
                case UbicacionHeroe.DondeOneofCase.AsentamientoId: p = sesion.Almacen.Buscar(TipoEntidad.Asentamiento, u.AsentamientoId)?.Asentamiento?.Posicion; break;
                case UbicacionHeroe.DondeOneofCase.PuebloId: p = sesion.Almacen.Buscar(TipoEntidad.Pueblo, u.PuebloId)?.Pueblo?.Posicion; break;
                case UbicacionHeroe.DondeOneofCase.AvanzadaId: p = sesion.Almacen.Buscar(TipoEntidad.Avanzada, u.AvanzadaId)?.Avanzada?.Posicion; break;
            }
            return p == null ? ((double, double)?)null : (p.X / 1000.0, p.Y / 1000.0);
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
        /// <summary>Un clic en el mapa (BronzeAgeClient: `resolverClic`): con la lista abierta, solo la cierra; sin nada debajo, se quita la
        /// selección y se marcha allí; con una cosa, se elige; con dos o más, la lista.</summary>
        public async Task Clic(Vector2 pantalla)
        {
            if (ListaAbierta)
            {
                CerrarLista();
                return;
            }
            var punto = PuntoBajo(pantalla);
            if (punto == null) return;
            var bajo = CosasDelMapa.BajoElClic(Cosas, punto.Value.x, punto.Value.y, Camara.PxPorUnidad(Screen.height));
            if (bajo.Count == 0)
            {
                Seleccionar(null);
                await MarcharA(punto.Value.x, punto.Value.y);
            }
            else if (bajo.Count == 1) await Elegir(bajo[0]);
            else AbrirLista(bajo);
        }

        /// <summary>Con dos o más cosas bajo un clic (también el del mapa 2D): la lista para elegir.</summary>
        public void AbrirLista(List<CosaDelMapa> cosas)
        {
            ListaAbierta = true;
            AlElegir?.Invoke(cosas);
        }

        public void CerrarLista()
        {
            ListaAbierta = false;
            AlCerrarLista?.Invoke();
        }

        public async Task<ResultadoOrden> MarcharA(double x, double y)
        {
            var id = heroeId();
            var heroe = id == null ? null : sesion.Almacen.Buscar(TipoEntidad.Heroe, id)?.Heroe;
            if (heroe?.Ubicacion == null) return null;
            var p = new Punto { X = (long)Math.Round(x * 1000), Y = (long)Math.Round(y * 1000) };
            // Solo con la columna fuera. Desde dentro de un sitio (campamento, plaza, pueblo o avanzada) el clic no hace nada: se sale con la salida de
            // siempre (Víctor, 2026-11-16).
            if (heroe.Ubicacion.DondeCase != UbicacionHeroe.DondeOneofCase.EjercitoId) return null;
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
        /// <summary>Teselas de suelo construidas desde el principio y lo que han tardado (ms), para medir.</summary>
        public (int teselas, double ms) ConstruccionDelSuelo => terreno == null ? (0, 0) : (terreno.Construidas, terreno.MsConstruyendo);
        public bool SueloCompleto => terreno != null && terreno.Completo && (vegetacion == null || vegetacion.Completo) && (nodos == null || nodos.Completo);
        public VegetacionDelMapa Vegetacion => vegetacion;
        public double PeorMsTeselaDeSuelo => terreno?.PeorMsTesela ?? 0;
        TrazosEnElMapa trazos;
        NodosDelMapa nodos;
        MaquetasDelMapa maquetas;
        /// <summary>Las maquetas de los asentamientos (para las pruebas: su coste).</summary>
        public MaquetasDelMapa Maquetas => maquetas;
        /// <summary>Los nodos de recursos (para las pruebas: su coste y sus teselas).</summary>
        public NodosDelMapa Nodos => nodos;
        TerritorioDelMapa territorio;
        /// <summary>Para las pruebas: el territorio (lo que tarda en pintarse y subir).</summary>
        public TerritorioDelMapa Territorio => territorio;
        /// <summary>Para las pruebas: los vértices de las cintas de caminos y fronteras puestas.</summary>
        public int VerticesDeTrazos => trazos?.Vertices ?? 0;
        public TrazosEnElMapa Trazos => trazos;
        /// <summary>Para las pruebas: los cauces (y sus trechos de vado).</summary>
        public CaucesDelMapa Cauces => terreno?.Cauces;
        /// <summary>El suelo del mapa (para las pruebas: la altura de la malla pintada).</summary>
        public TerrenoDelMapa Terreno => terreno;
        /// <summary>Lo más que ha tardado la sombra del relieve en un fotograma (ms).</summary>
        public double PeorMsRelieve => terreno?.PeorMsRelieve ?? 0;
        /// <summary>Teselas a la espera de su sombra del relieve.</summary>
        public int HorneandoRelieve => terreno?.HorneandoRelieve ?? 0;
        public Camera Vista => camara;
        /// <summary>Lo que se ve en (x, y) del mapa: la altura del suelo o de su agua, en metros.</summary>
        public float AlturaVisible(double x, double y) => terreno.AlturaVisible(x, y);
        /// <summary>Puntos de la ruta pintada (0 sin ruta).</summary>
        public float? AlturaDeMalla(double x, double y) => terreno.AlturaDeMalla(x, y);
        /// <summary>Los ríos y vados pintados (una malla).</summary>
        public Mesh MallaDeRios => rios != null ? rios.GetComponent<MeshFilter>().sharedMesh : null;
        public int PuntosDeRuta => ruta != null && ruta.enabled ? ruta.positionCount : 0;
    }
}
