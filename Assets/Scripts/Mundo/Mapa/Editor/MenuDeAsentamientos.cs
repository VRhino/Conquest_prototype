// Para ver los asentamientos del mapa de mundo en el editor (solo editor; nada del juego cambia):
//  * `Conquest/Mundo/Asentamientos: Mejorado`: la casilla del estilo de las maquetas (`MaquetasDelMapa.Estilo`). Se guarda en EditorPrefs y
//    se aplica al entrar en Play; apagada, «Prototipo» (el valor del código).
//  * `Conquest/Mundo/Banco de asentamientos`: abre una escena vacía sin guardar, entra en Play y monta el mapa en local, sin servidor, con el
//    mundo que ya hay en disco (el que bajó la última partida, en `persistentDataPath/mundo`) y un banco de plazas: de nivel 1 (zonas de 12 y
//    20 u), 2 (25 u), 3 (35 u), 4 (45 u) y 5 (55 u), las del 2 al 5 abiertas y amuralladas, cada una en tierra firme, llana y despejada,
//    separadas. La cámara
//    es la del mapa (arrastrar gira, la rueda acerca); `Conquest/Mundo/Banco: siguiente plaza` lleva a la siguiente.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BronzeAge.Contract.V1;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using AmbitoPb = BronzeAge.Contract.V1.Ambito;

namespace ConquestTactics.Mundo.Mapa.Editor
{
    [InitializeOnLoad]
    static class MenuDeAsentamientos
    {
        const string ClaveMejorado = "Conquest.Mundo.AsentamientosMejorado", ClaveBanco = "Conquest.Mundo.AbrirBancoDeAsentamientos";
        const string RutaMejorado = "Conquest/Mundo/Asentamientos: Mejorado";

        static Almacen almacen;
        static readonly List<(string nombre, double x, double y)> sitios = new List<(string, double, double)>();
        static int actual;

        static MenuDeAsentamientos() { EditorApplication.playModeStateChanged += Cambio; }

        static void Cambio(PlayModeStateChange e)
        {
            if (e == PlayModeStateChange.EnteredPlayMode)
            {
                MaquetasDelMapa.Estilo = EditorPrefs.GetBool(ClaveMejorado, false) ? EstiloDeAsentamientos.Mejorado : EstiloDeAsentamientos.Prototipo;
                if (SessionState.GetBool(ClaveBanco, false))
                {
                    SessionState.SetBool(ClaveBanco, false);
                    try { Montar(); }
                    catch (Exception ex) { Debug.LogError($"[Banco de asentamientos] no se pudo montar: {ex.Message}"); }
                }
            }
            if (e == PlayModeStateChange.ExitingPlayMode) { almacen = null; sitios.Clear(); }
            // fuera de Play, el estilo vuelve al del código («Prototipo»): sin recarga de dominio, el de Play no se queda en el editor (ni en sus
            // pruebas)
            if (e == PlayModeStateChange.EnteredEditMode) MaquetasDelMapa.Estilo = EstiloDeAsentamientos.Prototipo;
        }

        [MenuItem(RutaMejorado, false, 200)]
        static void CambiarEstilo()
        {
            bool v = !EditorPrefs.GetBool(ClaveMejorado, false);
            EditorPrefs.SetBool(ClaveMejorado, v);
            Menu.SetChecked(RutaMejorado, v);
            if (Application.isPlaying) MaquetasDelMapa.Estilo = v ? EstiloDeAsentamientos.Mejorado : EstiloDeAsentamientos.Prototipo;
        }

        [MenuItem(RutaMejorado, true)]
        static bool ValidarEstilo()
        {
            Menu.SetChecked(RutaMejorado, EditorPrefs.GetBool(ClaveMejorado, false));
            return true;
        }

        [MenuItem("Conquest/Mundo/Banco de asentamientos", false, 201)]
        static void AbrirBanco()
        {
            if (Application.isPlaying) { Debug.LogWarning("[Banco de asentamientos] sal de Play primero"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(ClaveBanco, true);
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("Conquest/Mundo/Banco: siguiente plaza", false, 202)]
        static void Siguiente()
        {
            if (almacen == null || sitios.Count == 0) return;
            actual = (actual + 1) % sitios.Count;
            Ir(actual);
        }

        [MenuItem("Conquest/Mundo/Banco: siguiente plaza", true)]
        static bool ValidarSiguiente() => Application.isPlaying && almacen != null;

        static Punto Pu(double x, double y) => new Punto { X = (long)(x * 1000), Y = (long)(y * 1000) };

        static void Ir(int i)
        {
            var (nombre, x, y) = sitios[i];
            almacen.Buscar(TipoEntidad.Ejercito, "ej-banco").Ejercito.Posicion = Pu(x, y);
            Debug.Log($"[Banco de asentamientos] {nombre} ({x:0}, {y:0})");
        }

        static byte[] Leer(string carpeta)
        {
            if (!Directory.Exists(carpeta)) return null;
            var f = Directory.GetFiles(carpeta, "*.bin").FirstOrDefault();
            return f == null ? null : File.ReadAllBytes(f);
        }

        static void Montar()
        {
            // el mundo en disco: el que bajó la última partida
            var carpeta = Path.Combine(Application.persistentDataPath, "mundo");
            var bArtefacto = Leer(Path.Combine(carpeta, "artefacto"));
            var bDatos = Leer(Path.Combine(carpeta, "datos"));
            var bRelieve = Leer(Path.Combine(carpeta, "relieve"));
            if (bArtefacto == null || bDatos == null)
                throw new InvalidOperationException($"no hay mundo en disco en {carpeta}: entra antes una vez al mundo con el servidor (MundoScene)");
            var artefacto = ArtefactoDeMundo.Parser.ParseFrom(bArtefacto);
            var datos = DatosDeJuego.Leer(bDatos);
            var relieve = new Relieve(bRelieve != null ? PaqueteRelieve.Parser.ParseFrom(bRelieve) : null);
            var cauces = new CaucesDelMapa(artefacto, relieve);
            var bosque = new RasterDeMundo(artefacto.DensidadBosque);

            // los sitios: tierra firme, llana y despejada (sin agua, ni río, ni bosque espeso bajo la plaza y su suelo), separados
            var puestos = new List<(double x, double y, double r)>();
            bool Libre(double x, double y, double r)
            {
                double hMin = double.MaxValue, hMax = double.MinValue, paso = Math.Max(1, r / 5);
                if (relieve.AguaEn(x, y) || !puestos.All(q => Math.Sqrt((q.x - x) * (q.x - x) + (q.y - y) * (q.y - y)) > q.r + r + 6)) return false;
                for (double dy = -r; dy <= r; dy += paso)
                    for (double dx = -r; dx <= r; dx += paso)
                    {
                        if (dx * dx + dy * dy > r * r) continue;
                        if (relieve.AguaEn(x + dx, y + dy) || bosque.En(x + dx, y + dy) > 20 || cauces.ACauce(x + dx, y + dy, 3) < 2) return false;
                        double h = relieve.AlturaEn(x + dx, y + dy);
                        hMin = Math.Min(hMin, h);
                        hMax = Math.Max(hMax, h);
                        if (hMax - hMin > 30) return false;
                    }
                return puestos.All(q => Math.Sqrt((q.x - x) * (q.x - x) + (q.y - y) * (q.y - y)) > q.r + r + 6);
            }
            double cx = relieve.Columnas * relieve.Paso / 2, cy = relieve.Filas * relieve.Paso / 2;
            (double, double)? Buscar(double r)
            {
                var (ox, oy) = puestos.Count > 0 ? (puestos[puestos.Count - 1].x, puestos[puestos.Count - 1].y) : (cx, cy);
                for (double d = 0; d < 600; d += d < 80 ? 2 : 6)
                {
                    int n = Math.Max(1, (int)(d * 1.5));
                    for (int k = 0; k < n; k++)
                    {
                        double a = k * Math.PI * 2 / n, x = ox + d * Math.Cos(a), y = oy + d * Math.Sin(a);
                        if (x < 30 || y < 30 || x > cx * 2 - 30 || y > cy * 2 - 30) continue;
                        if (Libre(x, y, r)) { puestos.Add((x, y, r)); return (x, y); }
                    }
                }
                return null;
            }
            var foto = new Foto { Ambito = new AmbitoPb { Tipo = AmbitoTipo.AmbitoVista, Id = "f-1" }, Version = 2 };
            sitios.Clear();
            int id = 0;
            // (nivel, zona, muralla, Facción): el radio de búsqueda, la huella con su suelo y algo de margen
            foreach (var (nivel, zona, muralla, faccion) in new[] { (1u, 12.0, false, "f-1"), (1u, 20.0, false, "f-2"), (2u, 25.0, false, "f-1"), (2u, 25.0, true, "f-2"),
                                                                     (3u, 35.0, false, "f-1"), (3u, 35.0, true, "f-2"), (4u, 45.0, false, "f-2"), (4u, 45.0, true, "f-1"),
                                                                     (5u, 55.0, false, "f-1"), (5u, 55.0, true, "f-2") })
            {
                double huella = Math.Max(0.9, zona * 0.1 * 0.8 * (nivel <= 1 ? 0.62 : nivel == 2 ? 0.8 : 1));
                var sitio = Buscar(huella * 2.2 + 1);
                if (sitio == null) { Debug.LogWarning($"[Banco de asentamientos] sin sitio para el nivel {nivel} (zona {zona} u)"); continue; }
                var (x, y) = sitio.Value;
                var a = new Asentamiento { Id = $"banco-{id++}", Nombre = $"Nivel {nivel} · zona {zona:0} u{(muralla ? " · muralla" : "")}", Nivel = nivel, FaccionId = faccion,
                    Posicion = Pu(x, y), TieneMuralla = muralla };
                for (int i = 0; i < 24; i++) a.Zona.Add(Pu(x + zona * Math.Cos(i * Math.PI / 12), y + zona * Math.Sin(i * Math.PI / 12)));
                foto.Entidades.Add(new Entidad { Asentamiento = a });
                sitios.Add((a.Nombre, x, y));
            }
            if (sitios.Count == 0) throw new InvalidOperationException("no se encontró ningún sitio despejado");

            // el héroe del banco y su columna (la cámara la sigue), y el banco
            almacen = new Almacen();
            var heroe = new Foto { Ambito = new AmbitoPb { Tipo = AmbitoTipo.AmbitoHeroe, Id = "heroe-banco" }, Version = 1 };
            heroe.Entidades.Add(new Entidad { Heroe = new Heroe { Id = "heroe-banco", DisplayName = "Banco", FaccionId = "f-1", Ubicacion = new UbicacionHeroe { EjercitoId = "ej-banco" } } });
            var columna = new Ejercito { Id = "ej-banco", FaccionId = "f-1", Tipo = TipoColumna.ColumnaEjercito, Posicion = Pu(sitios[0].x, sitios[0].y), LiderId = "heroe-banco" };
            columna.Participantes.Add(new Participante { HeroeId = "heroe-banco" });
            heroe.Entidades.Add(new Entidad { Ejercito = columna });
            almacen.Aplicar(new MensajeDeServidor { Foto = heroe });
            almacen.Aplicar(new MensajeDeServidor { Foto = foto });
            var catalogo = Resources.Load<TextAsset>("MundoUI/Sigilo/catalogoSigilos");
            if (catalogo != null) Sigilos.Cargar(catalogo.text, "{}", "{}");
            // (una sesión sin servidor: nunca se abre)
            var sesion = new Sesion("http://127.0.0.1:1", "", "conquest-banco", almacen);
            var mapa = new GameObject("Banco de asentamientos").AddComponent<MapaDeMundo>();
            mapa.RadioVisionFijo = 30;
            mapa.Iniciar(sesion, relieve, artefacto, datos, () => "heroe-banco", _ => false, (t, p) => { }, null);
            actual = 0;
            Ir(0);
            Debug.Log($"[Banco de asentamientos] {sitios.Count} plazas; estilo {MaquetasDelMapa.Estilo}. «Conquest/Mundo/Banco: siguiente plaza» lleva a la siguiente.");
        }
    }
}
