// Las pantallas de fuera de la batalla, en UI Toolkit (paso 1 del bloque 4): acceso (Entrar y Crear cuenta), partidas con tu héroe, pantalla de
// carga y, mientras no exista el mapa de mundo (paso 2), una pantalla provisional que enseña que la sesión está viva. Encima, siempre, el aviso
// flotante, el panel de avisos y el cuadro de confirmación. Los textos y el flujo siguen a BronzeAgeClient (`ui/pantallaAcceso.ts`,
// `ui/pantallaPartidas.ts`, `ui/pantallaCarga.ts`, `cerrarSesion` de `apiCliente.ts`).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BronzeAge.Contract.V1;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.UI
{
    [RequireComponent(typeof(ConexionDeMundo))]
    public sealed class PantallasDeMundo : MonoBehaviour
    {
        static readonly Regex Nick = new Regex(@"^[\p{L}\p{N}_.-]{3,32}$");

        ConexionDeMundo c;
        VisualElement pantalla, capaCarga, capaEncima;
        Dialogo dialogo;
        PanelAvisos panelAvisos;
        Sesion sesionVigilada;
        ConquestTactics.Mundo.Mapa.MapaDeMundo mapa;
        VisualElement diagnostico;

        void Awake()
        {
            c = GetComponent<ConexionDeMundo>();
            var ajustes = ScriptableObject.CreateInstance<PanelSettings>();
            ajustes.themeStyleSheet = Resources.Load<ThemeStyleSheet>("MundoUI/TemaMundo");
            ajustes.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ajustes.referenceResolution = new Vector2Int(1600, 900);
            ajustes.match = 0.5f;
            // El documento se monta en un hijo apagado para que tenga sus ajustes antes de encenderse.
            var hijo = new GameObject("Interfaz");
            hijo.SetActive(false);
            hijo.transform.SetParent(transform, false);
            var doc = hijo.AddComponent<UIDocument>();
            doc.panelSettings = ajustes;
            hijo.SetActive(true);
            var raiz = doc.rootVisualElement;
            raiz.pickingMode = PickingMode.Ignore;
            raiz.styleSheets.Add(Resources.Load<StyleSheet>("MundoUI/Mundo"));
            pantalla = Capa(raiz);
            capaCarga = Capa(raiz);
            capaEncima = Capa(raiz);
            dialogo = new Dialogo(capaEncima);
            panelAvisos = new PanelAvisos(c.Avisos, capaEncima);
        }

        static VisualElement Capa(VisualElement raiz)
        {
            var v = new VisualElement { pickingMode = PickingMode.Ignore };
            v.style.position = Position.Absolute;
            v.style.left = v.style.top = v.style.right = v.style.bottom = 0;
            raiz.Add(v);
            return v;
        }

        void Start()
        {
            if (c.HayToken) _ = Partidas();
            else Acceso();
        }

        VisualElement Montar(string uxml)
        {
            _ = cerrarCreador?.Invoke();
            QuitarMapa();
            pantalla.Clear();
            var v = Resources.Load<VisualTreeAsset>("MundoUI/" + uxml).Instantiate();
            // El contenedor que crea Instantiate llena la pantalla: no puede quedarse con el ratón (en el mapa, el clic es del mapa).
            v.pickingMode = PickingMode.Ignore;
            v.style.position = Position.Absolute;
            v.style.left = v.style.top = v.style.right = v.style.bottom = 0;
            pantalla.Add(v);
            return v;
        }

        static void Ver(VisualElement e, bool visible) => e.EnableInClassList("oculto", !visible);

        static void Poner(Label l, string texto)
        {
            l.text = texto ?? "";
            Ver(l, !string.IsNullOrEmpty(texto));
        }

        // ─────────────────────────── acceso ───────────────────────────

        /// <summary>Entrar y Crear cuenta. Comprueba antes de mandar lo mismo que el motor (nick de 3 a 32 caracteres: letras, cifras, «_», «-» o
        /// «.»; contraseña de 8 a 128 y repetida igual). Lo que rechaza el servidor va junto al campo que toca y lo escrito no se pierde.</summary>
        void Acceso(string nick = null, string error = null, string aviso = null)
        {
            var v = Montar("Acceso");
            var registro = false;
            var conCodigo = false;
            var enviando = false;
            TextField F(string n) => v.Q<TextField>(n);
            Label L(string n) => v.Q<Label>(n);
            F("nick").value = nick ?? c.Nick ?? "";
            L("servidor").text = "Servidor: " + c.Servidor;
            Poner(L("error"), error);
            Poner(L("aviso"), aviso);

            void ErrorEn(string campo, string texto)
            {
                Poner(L(campo + "-error"), texto);
                v.Q(campo == "nick" || campo == "clave" || campo == "repetir" || campo == "codigo" ? "campo-" + campo : campo).EnableInClassList("campo--error", texto != null);
                if (texto != null) F(campo).Focus();
            }
            void LimpiarErrores()
            {
                foreach (var x in new[] { "nick", "clave", "repetir", "codigo" }) ErrorEn(x, null);
                Poner(L("error"), null);
            }
            void Pintar()
            {
                v.Q("pestana-entrar").EnableInClassList("pestana--activa", !registro);
                v.Q("pestana-registro").EnableInClassList("pestana--activa", registro);
                L("subtitulo").text = registro ? "Crea tu cuenta para jugar" : "Entra con tu cuenta";
                Ver(v.Q("campo-repetir"), registro);
                Ver(L("nick-ayuda"), registro && L("nick-error").ClassListContains("oculto"));
                Ver(L("clave-ayuda"), registro);
                Ver(v.Q("campo-codigo"), registro && conCodigo);
                Ver(v.Q("con-codigo"), registro && !conCodigo);
                var b = v.Q<Button>("enviar");
                b.text = enviando ? (registro ? "CREANDO LA CUENTA…" : "ENTRANDO…") : registro ? "CREAR CUENTA" : "ENTRAR";
                b.SetEnabled(!enviando);
            }
            void Modo(bool r)
            {
                if (r == registro || enviando) return;
                registro = r;
                LimpiarErrores();
                Poner(L("aviso"), null);
                Pintar();
            }

            async void Enviar()
            {
                if (enviando) return;
                LimpiarErrores();
                var n = F("nick").value.Trim();
                var clave = F("clave").value;
                if (registro)
                {
                    var errores = ValidarRegistro(n, clave, F("repetir").value);
                    if (errores.Count > 0) { foreach (var e in errores) ErrorEn(e.Key, e.Value); return; }
                }
                else
                {
                    if (n.Length == 0) { ErrorEn("nick", "Escribe tu nick."); return; }
                    if (clave.Length == 0) { ErrorEn("clave", "Escribe tu contraseña."); return; }
                }
                enviando = true;
                Pintar();
                if (registro)
                {
                    try { await c.Registrar(n, clave, conCodigo ? F("codigo").value.Trim() : ""); }
                    catch (ErrorDeAcceso e)
                    {
                        enviando = false;
                        Pintar();
                        if (e.Status == 409) ErrorEn("nick", "Ese nick ya existe: elige otro.");
                        else if (e.Status == 403)
                        {
                            var habia = conCodigo && F("codigo").value.Trim().Length > 0;
                            conCodigo = true;
                            Pintar();
                            ErrorEn("codigo", habia ? "El código de invitación no coincide." : "Este servidor pide un código de invitación.");
                        }
                        else if (e.Status == 400 && Regex.IsMatch(e.Message, "nick", RegexOptions.IgnoreCase)) ErrorEn("nick", Mayuscula(e.Message) + ".");
                        else if (e.Status == 400 && Regex.IsMatch(e.Message, "clave", RegexOptions.IgnoreCase)) ErrorEn("clave", Mayuscula(e.Message).Replace("La clave", "La contraseña") + ".");
                        else Poner(L("error"), MensajeDeAcceso(e));
                        return;
                    }
                }
                try
                {
                    await c.Entrar(n, clave);
                    _ = Partidas();
                }
                catch (ErrorDeAcceso e)
                {
                    enviando = false;
                    if (registro)
                    {
                        // La cuenta ya existe: no se vuelve a crear; se pasa a «Entrar» con el nick puesto y un aviso.
                        registro = false;
                        Poner(L("aviso"), $"Cuenta «{n}» creada. Entra con tu contraseña.");
                    }
                    Pintar();
                    if (e.Status == 401) ErrorEn("clave", "Nick o contraseña incorrectos.");
                    else Poner(L("error"), MensajeDeAcceso(e));
                }
            }

            v.Q<Button>("pestana-entrar").clicked += () => Modo(false);
            v.Q<Button>("pestana-registro").clicked += () => Modo(true);
            v.Q<Button>("con-codigo").clicked += () => { conCodigo = true; Pintar(); F("codigo").Focus(); };
            v.Q<Button>("enviar").clicked += Enviar;
            foreach (var x in new[] { "nick", "clave", "repetir", "codigo" })
            {
                var campo = x;
                F(campo).RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) Enviar(); }, TrickleDown.TrickleDown);
                // Al corregir un campo con error, su aviso se va.
                F(campo).RegisterValueChangedCallback(_ => { if (v.Q("campo-" + campo).ClassListContains("campo--error")) ErrorEn(campo, null); });
            }
            Pintar();
            v.schedule.Execute(() => F(F("nick").value.Length > 0 ? "clave" : "nick").Focus());
        }

        /// <summary>Las comprobaciones del motor (`crates/acceso`, `validar`), en el cliente.</summary>
        static Dictionary<string, string> ValidarRegistro(string nick, string clave, string repetir)
        {
            var e = new Dictionary<string, string>();
            var largoNick = new System.Globalization.StringInfo(nick).LengthInTextElements;
            if (nick.Length == 0) e["nick"] = "Escribe un nick.";
            else if (largoNick < 3 || largoNick > 32) e["nick"] = "El nick tiene de 3 a 32 caracteres.";
            else if (!Nick.IsMatch(nick)) e["nick"] = "Solo letras, cifras, «_», «-» o «.» (sin espacios).";
            var largo = new System.Globalization.StringInfo(clave).LengthInTextElements;
            if (clave.Length == 0) e["clave"] = "Escribe una contraseña.";
            else if (largo < 8) e["clave"] = $"Al menos 8 caracteres (llevas {largo}).";
            else if (largo > 128) e["clave"] = "Como mucho 128 caracteres.";
            if (!e.ContainsKey("clave") && repetir != clave) e["repetir"] = "No coincide con la contraseña.";
            return e;
        }

        static string Mayuscula(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);

        static string MensajeDeAcceso(ErrorDeAcceso e) =>
            e.Status == 0 ? $"No se pudo contactar con el servidor ({e.Message}). ¿Está corriendo el servidor del motor?"
            : Textos.Explicar(e.Codigo) != e.Codigo ? Textos.Explicar(e.Codigo) : Mayuscula(e.Message);

        // ─────────────────────────── partidas ───────────────────────────

        /// <summary>Las partidas del servidor (el motor tiene una) con tu héroe: nombre, Facción y nivel, o «aún sin héroe».</summary>
        async Task Partidas(string error = null)
        {
            var v = Montar("Partidas");
            v.Q<Label>("subtitulo").text = "Sesión de " + (c.Nick ?? "");
            var lista = v.Q<ScrollView>("lista");
            lista.Add(Dialogo.Texto("Cargando partidas…", "vacio"));
            var entrar = v.Q<Button>("entrar");
            entrar.SetEnabled(false);
            Poner(v.Q<Label>("error"), error);
            v.Q<Button>("cerrar-sesion").clicked += () => _ = CerrarSesion();
            Bienvenida b;
            try
            {
                b = await c.AbrirSesion();
                VigilarSesion(c.Sesion);
                await EsperarHeroe(c.Sesion, 5000);
            }
            catch (ErrorDeSesion e) when (e.Codigo == "token_invalido")
            {
                Acceso(c.Nick, "Tu sesión ha caducado: vuelve a entrar.");
                return;
            }
            catch (Exception e)
            {
                if (pantalla.Q("lista") != lista) return; // se cambió de pantalla mientras cargaba
                lista.Clear();
                lista.Add(Dialogo.Texto("No se pudo abrir la partida.", "vacio"));
                Poner(v.Q<Label>("error"), e is ErrorDeSesion s ? $"{Textos.Explicar(s.Codigo)} ({s.Codigo})" : e.Message);
                entrar.text = "REINTENTAR";
                entrar.SetEnabled(true);
                entrar.clicked += () => _ = Partidas();
                return;
            }
            if (pantalla.Q("lista") != lista) return;
            lista.Clear();
            var tarjeta = Dialogo.Caja("partida partida--elegida");
            tarjeta.Add(Dialogo.Texto("Mundo", "partida-nombre"));
            tarjeta.Add(Dialogo.Texto(LineaDelHeroe(b, c.Sesion.Almacen), "partida-heroe"));
            tarjeta.Add(Dialogo.Texto($"{c.Servidor} · mundo {b.Mundo.Hash.Substring(0, Math.Min(8, b.Mundo.Hash.Length))}", "partida-detalle"));
            lista.Add(tarjeta);
            entrar.SetEnabled(true);
            entrar.clicked += () => _ = EntrarEnLaPartida();
        }

        static string LineaDelHeroe(Bienvenida b, Almacen a)
        {
            if (!b.HasHeroeId) return "Eres miembro, aún sin héroe.";
            var h = a.Buscar(TipoEntidad.Heroe, b.HeroeId)?.Heroe;
            var faccionId = h != null && h.HasFaccionId ? h.FaccionId : b.HasFaccionId ? b.FaccionId : null;
            var faccion = faccionId == null ? "Sin Facción" : NombreDeFaccion(a, faccionId) ?? "Facción";
            return $"{Textos.NombreDeHeroe(a, b.HeroeId)} — {faccion} · Nivel {h?.Nivel ?? 1}";
        }

        static string NombreDeFaccion(Almacen a, string id)
        {
            var f = a.Buscar(TipoEntidad.Faccion, id)?.Faccion;
            if (f != null) return f.Nombre;
            var dir = a.Buscar(TipoEntidad.DirectorioFacciones, "facciones")?.DirectorioFacciones;
            return dir?.Facciones.FirstOrDefault(x => x.Id == id)?.Nombre;
        }

        /// <summary>Con héroe, su `HEROE` (y su `FACCION`) llegan justo detrás de la Bienvenida: se esperan un poco para no pintar «sin héroe».</summary>
        static async Task EsperarHeroe(Sesion s, int ms)
        {
            var hasta = Time.realtimeSinceStartup + ms / 1000f;
            bool Llegado()
            {
                var b = s.Bienvenida;
                if (b == null || !b.HasHeroeId) return true;
                if (!s.Almacen.Ambitos.ContainsKey("HEROE:" + b.HeroeId)) return false;
                var h = s.Almacen.Buscar(TipoEntidad.Heroe, b.HeroeId)?.Heroe;
                var f = h != null && h.HasFaccionId ? h.FaccionId : b.HasFaccionId ? b.FaccionId : null;
                return f == null || s.Almacen.Ambitos.ContainsKey("FACCION:" + f);
            }
            while (!Llegado() && Time.realtimeSinceStartup < hasta) await Task.Delay(50);
        }

        async Task EntrarEnLaPartida()
        {
            var carga = new PantallaCarga(capaCarga, null, "Entrando en el mundo");
            try
            {
                await c.CargarMundo(carga.Progreso);
            }
            catch (Exception e)
            {
                _ = carga.Cerrar();
                await Partidas("No se pudo cargar el mundo: " + e.Message);
                return;
            }
            // Con héroe, el mapa; sin héroe, crearlo. Sin relieve (un servidor que no lo tiene), la pantalla de la sesión viva.
            if (c.Relieve == null) SesionViva(null);
            else if (c.HeroeId != null) Mapa();
            else CrearHeroe();
            await carga.Cerrar();
        }

        // ─────────────────────────── crear el héroe ───────────────────────────

        /// <summary>«Nace tu héroe» (paso 3): solo el lugar y el nombre. El campamento se elige sobre el mapa del paso 2 o en la lista (con su
        /// región, el «Recomendado», «Elegir por mí» y la comparación de dos, como BronzeAgeClient). «Nacer en …» abre el creador de personaje
        /// de Conquest (la escena AvatarCreator, encima de esta): allí la clase, el género y el aspecto, y al confirmar sale `crearHeroe`. Si se
        /// acepta, el mapa con el héroe dentro de su campamento; con «Volver», aquí otra vez con lo que había.</summary>
        void CrearHeroe()
        {
            var v = Montar("CrearHeroe");
            Botones(v);
            var b = c.Sesion.Bienvenida;
            var analisis = AnalisisDeCampamento.Analizar(b.CampamentosParaElegir, c.Artefacto, new Relieve(c.Relieve));
            var recomendado = AnalisisDeCampamento.Recomendado(analisis);
            var clases = ClaseDeHeroe.Leer(c.Datos);
            AnalisisDeCampamento campamento = null;
            var comparar = new List<AnalisisDeCampamento>();
            var abierto = false;
            string LugarDe(string id) => analisis.FirstOrDefault(x => x.Id == id)?.Lugar ?? AnalisisDeCampamento.Nombre(id);

            mapa = new GameObject("Mapa").AddComponent<ConquestTactics.Mundo.Mapa.MapaDeMundo>();
            mapa.RadioVisionFijo = AnalisisDeCampamento.RadioCercano;
            mapa.NombresDeNodos = false; // con la visión amplia de elegir campamento serían cientos
            mapa.Iniciar(c.Sesion, new Relieve(c.Relieve), c.Artefacto, c.Datos, () => c.HeroeId, SobreInterfaz, (t, peligro) => panelAvisos.Mostrar(t, peligro), v.Q("nombres"));
            // Lo que rodea a los campamentos: los bandidos del artefacto (el almacén aún está vacío).
            foreach (var bandidos in c.Artefacto.BandidosIniciales)
                mapa.Adicionales.Add(new CosaDelMapa { Clase = ClaseDeCosa.Bandidos, Id = bandidos.Id, Nombre = "Bandidos", Detalle = $"Nivel {bandidos.Nivel}",
                    X = bandidos.Posicion.X / 1000.0, Y = bandidos.Posicion.Y / 1000.0, Radio = 0.768, Alto = 0.35, RadioClic = 0.888, Color = CosasDelMapa.Bandidos });
            var cajas = new Dictionary<string, CosaDelMapa>();
            foreach (var a in analisis)
            {
                var caja = new CosaDelMapa { Clase = ClaseDeCosa.Mercenarios, Id = a.Id, Nombre = a.Lugar, Detalle = Gente(a.Gente),
                    X = a.X, Y = a.Y, Radio = 1.008, Alto = 0.45, RadioClic = 1.128, Color = CosasDelMapa.Mercenarios };
                cajas[a.Id] = caja;
                mapa.Adicionales.Add(caja);
            }

            var lista = v.Q<ScrollView>("campamentos");
            void PintarLista()
            {
                lista.Clear();
                foreach (var a in analisis.OrderByDescending(x => x.Gente))
                {
                    var este = a;
                    var tarjeta = new VisualElement { name = "tarjeta-" + a.Id };
                    tarjeta.AddToClassList("campamento");
                    tarjeta.EnableInClassList("campamento--elegido", campamento?.Id == a.Id);
                    tarjeta.EnableInClassList("campamento--comparado", comparar.Contains(a));
                    var boton = new Button(() => Elegir(este)) { name = "elegir" };
                    boton.AddToClassList("campamento-boton");
                    var cabeza = new VisualElement();
                    cabeza.AddToClassList("fila-envuelta");
                    cabeza.Add(Dialogo.Texto(a.Lugar, "aviso-titulo"));
                    if (a == recomendado) cabeza.Add(Dialogo.Texto("★ Recomendado", "campamento-recomendado"));
                    boton.Add(cabeza);
                    boton.Add(Dialogo.Texto($"{Gente(a.Gente)} · {Tierra(a)}", "aviso-cuando"));
                    tarjeta.Add(boton);
                    var marca = new Toggle("Comparar") { name = "comparar", value = comparar.Contains(a) };
                    marca.AddToClassList("campamento-comparar");
                    marca.RegisterValueChangedCallback(e => AlternarComparar(este, e.newValue));
                    tarjeta.Add(marca);
                    lista.Add(tarjeta);
                }
            }

            void AlternarComparar(AnalisisDeCampamento a, bool si)
            {
                comparar.Remove(a);
                if (si) comparar.Add(a);
                while (comparar.Count > 2) comparar.RemoveAt(0);
                PintarLista();
                PintarFicha();
            }

            void Elegir(AnalisisDeCampamento a)
            {
                campamento = a;
                comparar.Clear();
                mapa.Enfoque = (a.X, a.Y);
                mapa.RayasAdicionales.Clear();
                mapa.RayasAdicionales.Add(CosasDelMapa.Circulo((a.X, a.Y), AnalisisDeCampamento.RadioCercano, 0.8, 0xd8b878, 96));
                mapa.RayasAdicionales.Add(CosasDelMapa.Circulo((a.X, a.Y), AnalisisDeCampamento.RadioFrontera, 0.6, 0x6b4a29, 128));
                mapa.RehacerRayas();
                if (mapa.Seleccionada?.Id != a.Id) mapa.Seleccionar(cajas[a.Id]);
                PintarLista();
                PintarFicha();
            }
            // Un clic en el mapa sobre un campamento lo elige; en el suelo, sigue elegido el que había.
            mapa.AlSeleccionar += cosa =>
            {
                if (cosa != null && cosa.Clase == ClaseDeCosa.Mercenarios && cosa.Id != campamento?.Id) Elegir(analisis.First(x => x.Id == cosa.Id));
                else if (cosa == null && campamento != null) mapa.Seleccionar(cajas[campamento.Id]);
            };
            mapa.AlElegir += cosas => { mapa.ListaAbierta = false; var otro = cosas.FirstOrDefault(x => x.Clase == ClaseDeCosa.Mercenarios); if (otro != null) _ = mapa.Elegir(otro); };
            v.Q<Button>("elegir-por-mi").clicked += () => { var a = AnalisisDeCampamento.ElegirPorMi(analisis, UnityEngine.Random.value); if (a != null) Elegir(a); };

            void PintarFicha()
            {
                var comparando = comparar.Count == 2;
                Ver(v.Q("ficha-campamento"), !comparando);
                Ver(v.Q("comparacion"), comparando);
                if (comparando) PintarComparacion(v.Q("comparacion"), comparar[0], comparar[1], recomendado, x => Elegir(x), () => { comparar.Clear(); PintarLista(); PintarFicha(); });
                v.Q<Label>("campamento-nombre").text = campamento == null ? "Elige un campamento" : campamento.Lugar;
                Poner(v.Q<Label>("campamento-recomendado"), campamento == null ? null
                    : campamento == recomendado ? "★ Recomendado" + (campamento.Razones.Count > 0 ? ": " + string.Join(" · ", campamento.Razones).ToLowerInvariant() : "")
                    : campamento.Razones.Count > 0 ? string.Join(" · ", campamento.Razones) : null);
                Fichas(v.Q("campamento-datos"), campamento == null ? new (string, string)[0] : DatosDelCampamento(campamento, LugarDe));
                // Solo lo que se puede hacer: «Nacer en …» sale con campamento; mientras, lo que falta.
                Ver(v.Q<Button>("nacer"), campamento != null);
                v.Q<Button>("nacer").text = campamento == null ? "NACER AQUÍ" : $"NACER EN {campamento.Lugar.ToUpperInvariant()}";
                v.Q<Button>("nacer").SetEnabled(!abierto);
                v.Q<Label>("falta").text = campamento != null ? "" : "Elige campamento.";
            }

            // «Nacer en …»: el creador de personaje, encima. Lo de aquí (la interfaz, el mapa y su cámara) se aparta mientras; este objeto sigue
            // encendido y con él la sesión con el motor.
            async void AbrirCreador()
            {
                if (abierto || campamento == null || mapa == null) return;
                abierto = true;
                PintarFicha();
                Poner(v.Q<Label>("nombre-error"), null);
                var raiz = pantalla.parent;
                var mapaCrear = mapa;
                var camara = mapaCrear.Vista;
                var mundo = gameObject.scene;
                string rechazoDeNombre = null;
                raiz.style.display = DisplayStyle.None;
                mapaCrear.gameObject.SetActive(false);
                if (camara != null) camara.gameObject.SetActive(false);

                async Task Cerrar(bool nacido)
                {
                    if (SelectorDePersonaje.Actual == null) return;
                    SelectorDePersonaje.Actual = null;
                    cerrarCreador = null;
                    if (mundo.IsValid() && mundo.isLoaded) UnityEngine.SceneManagement.SceneManager.SetActiveScene(mundo);
                    var descarga = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(Creador);
                    while (descarga != null && !descarga.isDone) await Task.Yield();
                    if (this == null) return;
                    raiz.style.display = DisplayStyle.Flex;
                    if (camara != null) camara.gameObject.SetActive(true);
                    abierto = false;
                    if (nacido) { Mapa(); return; }
                    if (mapaCrear == null) return; // ya se fue a otra pantalla
                    mapaCrear.gameObject.SetActive(true);
                    PintarFicha();
                    // Un nombre rechazado allí se dice aquí, junto al campo, que es donde se cambia.
                    if (rechazoDeNombre != null) { Poner(v.Q<Label>("nombre-error"), rechazoDeNombre); v.Q<TextField>("nombre").Focus(); }
                }

                async Task<string> Confirmar(SelectorDePersonaje.Eleccion e)
                {
                    var orden = new Orden { CrearHeroe = new OrdenCrearHeroe
                    {
                        DisplayName = v.Q<TextField>("nombre").value.Trim(), CampamentoId = campamento.Id, ClassDefinitionId = e.ClaseId, Genero = e.Genero,
                        // El motor de hoy no guarda el género ni el avatar (pedido en «Pedidos al motor»).
                        Avatar = new OrdenCrearHeroe.Types.Avatar { CabezaId = e.Cabeza ?? "", PeloId = e.Pelo ?? "", BarbaId = e.Barba ?? "", CejasId = e.Cejas ?? "" },
                    } };
                    ResultadoOrden r;
                    try { r = await c.Sesion.Ordenar(orden); }
                    catch (ErrorDeSesion x) { return x.Message; }
                    if (r.Resultado == Resultado.OrdenAceptada) { await Cerrar(true); return null; }
                    var texto = Textos.Explicar(r.Rechazo);
                    rechazoDeNombre = r.Rechazo != null && r.Rechazo.Codigo.StartsWith("heroe.nombre") ? texto : null;
                    return texto;
                }

                SelectorDePersonaje.Actual = new SelectorDePersonaje.Encargo
                {
                    Nombre = v.Q<TextField>("nombre").value.Trim(), Lugar = campamento.Lugar, Clases = clases, Confirmar = Confirmar, Volver = () => _ = Cerrar(false),
                };
                cerrarCreador = () => Cerrar(false);
                var carga = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(Creador, UnityEngine.SceneManagement.LoadSceneMode.Additive);
                while (!carga.isDone) await Task.Yield();
                var escena = UnityEngine.SceneManagement.SceneManager.GetSceneByName(Creador);
                if (escena.IsValid()) UnityEngine.SceneManagement.SceneManager.SetActiveScene(escena); // su luz y su cielo
            }
            v.Q<Button>("nacer").clicked += AbrirCreador;
            v.Q<TextField>("nombre").RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) AbrirCreador(); }, TrickleDown.TrickleDown);

            PintarLista();
            // Empieza mirando al campamento con más gente (el más poblado), sin elegirlo por ti.
            var primero = analisis.OrderByDescending(x => x.Gente).FirstOrDefault();
            if (primero != null) mapa.Enfoque = (primero.X, primero.Y);
            PintarFicha();
        }

        /// <summary>La escena del creador de personaje de Conquest.</summary>
        const string Creador = "AvatarCreator";
        /// <summary>Cierra el creador si está abierto (al irse a otra pantalla con él delante: sesión perdida, reemplazada…).</summary>
        Func<Task> cerrarCreador;

        static string Gente(int n) => n == 1 ? "1 persona" : $"{n} personas";

        static string Tierra(AnalisisDeCampamento a) =>
            string.Join(", ", a.Suelo.Where(kv => kv.Value >= 0.1).OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value * 100:0}%"));

        /// <summary>Lo que rodea al campamento, como la ficha de BronzeAgeClient: gente, tierra, fertilidad y bosque a 150 u, yacimientos (a 150 u,
        /// hasta 250 y el más cerca), río, agua, bandidos y el vecino.</summary>
        static (string, string)[] DatosDelCampamento(AnalisisDeCampamento a, Func<string, string> lugarDe)
        {
            var r = new List<(string, string)>
            {
                ("Gente", Gente(a.Gente)),
                ("Tierra a 150 u", Tierra(a)),
                ("Fertilidad, bosque", $"{a.FertilidadMedia * 100:0}% · {a.FraccionDeBosque * 100:0}% de bosque"),
            };
            foreach (var kv in a.Nodos.OrderBy(x => x.Value.masCerca))
                r.Add(($"Yacimiento: {CosasDelMapa.NombreDeRecurso(kv.Key)}", $"{kv.Value.cerca} a 150 u · {kv.Value.frontera} a 250 u · el más cerca a {kv.Value.masCerca:0} u"));
            r.Add(("Río", a.Rio == null ? "ninguno" : $"a {a.Rio:0} u{(a.RioNavegable ? ", navegable" : "")}"));
            r.Add(("Agua (mar o lago)", a.Agua == null ? "a más de 250 u" : $"a {a.Agua:0} u"));
            r.Add(("Bandidos a 250 u", a.Bandidos.Count == 0 ? "ninguno" : $"{a.Bandidos.Count}; el más cerca a {a.Bandidos[0].distancia:0} u (nivel {a.Bandidos[0].nivel})"));
            if (a.Vecino != null) r.Add(("Campamento vecino", $"{lugarDe(a.Vecino)}, a {a.DistanciaAlVecino:0} u"));
            return r.ToArray();
        }

        /// <summary>Dos campamentos lado a lado (BronzeAgeClient `pintarComparacion`), con el mejor de cada fila en oro, y un botón para elegir cada uno.</summary>
        static void PintarComparacion(VisualElement donde, AnalisisDeCampamento a, AnalisisDeCampamento b, AnalisisDeCampamento recomendado,
            Action<AnalisisDeCampamento> elegir, Action cerrar)
        {
            donde.Clear();
            var cabeza = new VisualElement();
            cabeza.AddToClassList("comparar-fila");
            cabeza.Add(Dialogo.Texto("COMPARAR", "kicker"));
            var x = Dialogo.Boton("×", "boton-secundario", cerrar);
            x.name = "cerrar-comparacion";
            cabeza.Add(x);
            donde.Add(cabeza);
            void Fila(string titulo, string ta, string tb, int gana = 0)
            {
                var f = new VisualElement();
                f.AddToClassList("comparar-fila");
                f.Add(Dialogo.Texto(titulo, "comparar-titulo"));
                var la = Dialogo.Texto(ta, "comparar-valor");
                var lb = Dialogo.Texto(tb, "comparar-valor");
                la.EnableInClassList("comparar-gana", gana == 1);
                lb.EnableInClassList("comparar-gana", gana == 2);
                f.Add(la);
                f.Add(lb);
                donde.Add(f);
            }
            // Gana el valor más alto (o el más bajo, con `alto` en false); sin dato o empatados, ninguno.
            int Gana(double? va, double? vb, bool alto = true) => va == null || vb == null || va == vb ? 0 : (va > vb) == alto ? 1 : 2;
            string Cerca(AnalisisDeCampamento k) => k.Nodos.Count(n => n.Value.cerca > 0) == 0 ? "—"
                : string.Join(", ", k.Nodos.Where(n => n.Value.cerca > 0).Select(n => $"{CosasDelMapa.NombreDeRecurso(n.Key)} {n.Value.cerca}"));
            int Bandidos(AnalisisDeCampamento k) => k.Bandidos.Count(z => z.distancia <= AnalisisDeCampamento.RadioCercano);
            string Estrellas(AnalisisDeCampamento k) => new string('★', Math.Max(1, (int)Math.Round(k.Nota * 5))) + (k == recomendado ? " recomendado" : "");
            Fila("", a.Lugar, b.Lugar);
            Fila("Gente", Gente(a.Gente), Gente(b.Gente), Gana(a.Gente, b.Gente, false));
            Fila("Terreno", Tierra(a), Tierra(b));
            Fila("Fertilidad", $"{a.FertilidadMedia * 100:0}%", $"{b.FertilidadMedia * 100:0}%", Gana(a.FertilidadMedia, b.FertilidadMedia));
            Fila("Bosque", $"{a.FraccionDeBosque * 100:0}%", $"{b.FraccionDeBosque * 100:0}%", Gana(a.FraccionDeBosque, b.FraccionDeBosque));
            Fila("Yacimientos a 150 u", Cerca(a), Cerca(b), Gana(a.Nodos.Sum(n => n.Value.cerca), b.Nodos.Sum(n => n.Value.cerca)));
            Fila("Río", a.Rio == null ? "lejos" : $"a {a.Rio:0} u", b.Rio == null ? "lejos" : $"a {b.Rio:0} u", Gana(-a.Rio, -b.Rio));
            Fila("Costa o lago", a.Agua == null ? "lejos" : $"a {a.Agua:0} u", b.Agua == null ? "lejos" : $"a {b.Agua:0} u", Gana(-a.Agua, -b.Agua));
            Fila("Bandidos a 150 u", Bandidos(a).ToString(), Bandidos(b).ToString(), Gana(Bandidos(a), Bandidos(b), false));
            Fila("Vecino más cercano", a.Vecino == null ? "—" : $"a {a.DistanciaAlVecino:0} u", b.Vecino == null ? "—" : $"a {b.DistanciaAlVecino:0} u",
                Gana(a.Vecino == null ? (double?)null : a.DistanciaAlVecino, b.Vecino == null ? (double?)null : b.DistanciaAlVecino));
            Fila("Valoración", Estrellas(a), Estrellas(b), Gana(a.Nota, b.Nota));
            var acciones = new VisualElement();
            acciones.AddToClassList("fila-envuelta");
            acciones.style.marginTop = 8;
            var ea = Dialogo.Boton("Elegir " + a.Lugar, "boton-secundario", () => elegir(a));
            ea.name = "elegir-a";
            var eb = Dialogo.Boton("Elegir " + b.Lugar, "boton-secundario", () => elegir(b));
            eb.name = "elegir-b";
            acciones.Add(ea);
            acciones.Add(eb);
            donde.Add(acciones);
        }

        // ─────────────────────────── el mapa de mundo ───────────────────────────

        /// <summary>El mapa con su barra (capas, marcha forzada, avisos, cambiar de partida y cerrar sesión), la lista de lo que hay bajo el clic,
        /// la ficha de lo seleccionado con sus acciones y, dentro de un sitio, dónde estás y la salida. F2 enseña la sesión viva como diagnóstico.</summary>
        void Mapa()
        {
            var v = Montar("Mapa");
            mapa = new GameObject("Mapa").AddComponent<ConquestTactics.Mundo.Mapa.MapaDeMundo>();
            mapa.Iniciar(c.Sesion, new Relieve(c.Relieve), c.Artefacto, c.Datos, () => c.HeroeId, SobreInterfaz, (t, peligro) => panelAvisos.Mostrar(t, peligro), v.Q("nombres"));
            mapa.NombresDeNodos = PlayerPrefs.GetInt("mundo.capa.nodos", 1) == 1;
            mapa.Fertilidad = PlayerPrefs.GetInt("mundo.capa.fertilidad", 0) == 1;
            diagnostico = v.Q("diagnostico");
            var botonAvisos = v.Q<Button>("avisos");
            var forzada = v.Q<Button>("marcha-forzada");
            var nodos = v.Q<Button>("capa-nodos");
            var fertilidad = v.Q<Button>("capa-fertilidad");
            botonAvisos.clicked += panelAvisos.Alternar;
            forzada.clicked += () => _ = mapa.AlternarMarchaForzada();
            nodos.clicked += () => { mapa.NombresDeNodos = !mapa.NombresDeNodos; PlayerPrefs.SetInt("mundo.capa.nodos", mapa.NombresDeNodos ? 1 : 0); };
            fertilidad.clicked += () => { mapa.Fertilidad = !mapa.Fertilidad; PlayerPrefs.SetInt("mundo.capa.fertilidad", mapa.Fertilidad ? 1 : 0); };
            Botones(v);

            // La lista de lo que hay bajo el clic.
            var lista = v.Q("lista");
            mapa.AlElegir += cosas =>
            {
                v.Q<Label>("lista-titulo").text = $"AQUÍ HAY {cosas.Count} COSAS";
                var filas = v.Q("lista-filas");
                filas.Clear();
                foreach (var cosa in cosas)
                {
                    var elegida = cosa;
                    var fila = new Button(() => { Ver(lista, false); _ = mapa.Elegir(elegida); });
                    fila.AddToClassList("lista-fila");
                    fila.Add(Dialogo.Texto(cosa.Nombre, "aviso-titulo"));
                    if (!string.IsNullOrEmpty(cosa.Detalle)) fila.Add(Dialogo.Texto(cosa.Detalle, "aviso-cuando"));
                    filas.Add(fila);
                }
                Ver(lista, true);
            };
            mapa.AlCerrarLista += () => Ver(lista, false);

            // La ficha de lo seleccionado.
            var ficha = v.Q("ficha");
            v.Q<Button>("ficha-cerrar").clicked += () => mapa.Seleccionar(null);
            mapa.AlSeleccionar += cosa => { Ver(ficha, cosa != null); accionesPintadas = null; };

            // Dentro de un sitio: dónde y la salida.
            var dentro = v.Q("dentro");
            v.Q<Button>("salir").clicked += () => { var o = OrdenDeSalida(); if (o != null) _ = mapa.Ordenar(o); };

            void Refrescar()
            {
                if (mapa == null) return;
                botonAvisos.text = c.Avisos.NoLeidos > 0 ? $"Avisos ({c.Avisos.NoLeidos})" : "Avisos";
                forzada.SetEnabled(mapa.Columna != null);
                forzada.text = mapa.Columna?.MarchaForzada == true ? "Marcha forzada: sí" : "Marcha forzada: no";
                nodos.text = mapa.NombresDeNodos ? "Nodos: sí" : "Nodos: no";
                fertilidad.text = mapa.Fertilidad ? "Fertilidad: sí" : "Fertilidad: no";
                var lugar = LugarDondeEsta();
                Ver(dentro, lugar != null);
                if (lugar != null) v.Q<Label>("dentro-texto").text = $"Estás dentro de {lugar}. Su interior llega con su paso; de momento, desde aquí sales al mundo.";
                PintarFicha(v);
            }
            Refrescar();
            v.schedule.Execute(Refrescar).Every(250);
        }

        string accionesPintadas;

        /// <summary>La ficha: nombre, qué es, distancia y tiempo de marcha, y los botones de lo que se puede hacer (Marchar aquí y las acciones
        /// a la puerta). Los botones se rehacen solo si cambia lo que se puede hacer.</summary>
        void PintarFicha(VisualElement v)
        {
            var cosa = mapa.Seleccionada;
            if (cosa == null) return;
            v.Q<Label>("ficha-nombre").text = cosa.Nombre;
            v.Q<Label>("ficha-detalle").text = cosa.Detalle ?? "";
            var hasta = mapa.HastaAlli(cosa);
            Fichas(v.Q("ficha-datos"), hasta == null ? new[] { ("Distancia", "sin columna en el mapa") }
                : new[] { ("Distancia", $"{hasta.Value.distancia:0.#} u"), ("Tiempo de marcha", hasta.Value.tiempo) });
            var acciones = CosasDelMapa.Acciones(cosa, mapa.Contexto());
            bool marchar = mapa.Columna != null;
            var firma = cosa.Clase + cosa.Id + marchar + string.Join("|", acciones.Select(a => a.Texto));
            if (firma == accionesPintadas) return;
            accionesPintadas = firma;
            var caja = v.Q("ficha-acciones");
            caja.Clear();
            if (marchar)
                caja.Add(Dialogo.Boton("Marchar aquí", "boton-secundario", () =>
                    _ = cosa.Clase == ClaseDeCosa.Plaza ? mapa.MarcharAPlaza(cosa.Id) : mapa.MarcharA(cosa.X, cosa.Y)));
            foreach (var a in acciones)
            {
                var accion = a;
                caja.Add(Dialogo.Boton(accion.Texto, "boton-secundario", () => _ = mapa.Ordenar(accion.Orden.Clone())));
            }
        }

        /// <summary>El nombre del sitio donde está dentro el héroe (plaza, campamento, pueblo o avanzada), o null si está en el mapa.</summary>
        string LugarDondeEsta()
        {
            var u = HeroeAhora()?.Ubicacion;
            if (u == null) return null;
            var a = c.Sesion.Almacen;
            switch (u.DondeCase)
            {
                case UbicacionHeroe.DondeOneofCase.AsentamientoId:
                    var p = a.Buscar(TipoEntidad.Asentamiento, u.AsentamientoId)?.Asentamiento;
                    return p != null && p.HasNombre ? p.Nombre : "una plaza";
                case UbicacionHeroe.DondeOneofCase.CampamentoId: return "un campamento de mercenarios";
                case UbicacionHeroe.DondeOneofCase.PuebloId: return "un pueblo";
                case UbicacionHeroe.DondeOneofCase.AvanzadaId: return "una avanzada";
                default: return null;
            }
        }

        Heroe HeroeAhora() => c.HeroeId == null ? null : c.Sesion?.Almacen.Buscar(TipoEntidad.Heroe, c.HeroeId)?.Heroe;

        /// <summary>La orden para salir del sitio donde está el héroe (BronzeAgeClient: `ui/ventanaSalida.ts`), sin elegir tropa ni carga todavía.</summary>
        Orden OrdenDeSalida()
        {
            var h = HeroeAhora();
            var u = h?.Ubicacion;
            if (u == null) return null;
            switch (u.DondeCase)
            {
                case UbicacionHeroe.DondeOneofCase.AsentamientoId:
                    // De tu residencia se sale al mundo con tu columna; de otra plaza, sin más.
                    return h.HasResidenciaId && h.ResidenciaId == u.AsentamientoId
                        ? new Orden { SalirAlMundo = new OrdenSalirAlMundo { AsentamientoId = u.AsentamientoId, HeroeId = h.Id } }
                        : new Orden { SalirDeAsentamiento = new OrdenSalirDeAsentamiento { AsentamientoId = u.AsentamientoId, HeroeId = h.Id } };
                case UbicacionHeroe.DondeOneofCase.CampamentoId:
                    return new Orden { SalirDelCampamento = new OrdenSalirDelCampamento { CampamentoId = u.CampamentoId, HeroeId = h.Id } };
                case UbicacionHeroe.DondeOneofCase.PuebloId:
                    return new Orden { SalirDePueblo = new OrdenSalirDePueblo { PuebloId = u.PuebloId, HeroeId = h.Id } };
                case UbicacionHeroe.DondeOneofCase.AvanzadaId:
                    return new Orden { SalirDeAvanzada = new OrdenSalirDeAvanzada { AvanzadaId = u.AvanzadaId, HeroeId = h.Id } };
                default: return null;
            }
        }

        void Update()
        {
            var teclado = UnityEngine.InputSystem.Keyboard.current;
            if (diagnostico == null || teclado == null || !teclado.f2Key.wasPressedThisFrame) return;
            if (diagnostico.childCount == 0) SesionViva(diagnostico);
            diagnostico.ToggleInClassList("oculto");
        }

        /// <summary>¿Está el puntero sobre algo de la interfaz? (entonces el clic no es del mapa)</summary>
        public bool SobreInterfaz(Vector2 enPantalla)
        {
            var panel = pantalla.panel;
            if (panel == null) return false;
            var p = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(enPantalla.x, Screen.height - enPantalla.y));
            return panel.Pick(p) != null;
        }

        void QuitarMapa()
        {
            if (mapa != null) Destroy(mapa.gameObject);
            mapa = null;
            diagnostico = null;
        }

        void Botones(VisualElement v)
        {
            v.Q<Button>("cambiar-partida").clicked += async () => { await c.DejarPartida(); _ = Partidas(); };
            v.Q<Button>("cerrar-sesion").clicked += async () =>
            {
                if (await dialogo.Abrir("Tu héroe sale del mundo y se cierra la sesión en este equipo.", "¿Cerrar sesión?", "Cerrar sesión")) _ = CerrarSesion();
            };
        }

        // ─────────────────────────── la sesión viva ───────────────────────────

        /// <summary>El reloj del mundo, el héroe, la conexión, los ámbitos suscritos y cuántas entidades hay de cada tipo. A pantalla completa
        /// sin héroe; con mapa, como panel de diagnóstico (F2) dentro de `dentro`.</summary>
        void SesionViva(VisualElement dentro)
        {
            VisualElement v;
            if (dentro == null)
            {
                v = Montar("Sesion");
                var botonAvisos = v.Q<Button>("avisos");
                botonAvisos.clicked += panelAvisos.Alternar;
                Botones(v);
                v.schedule.Execute(() => botonAvisos.text = c.Avisos.NoLeidos > 0 ? $"Avisos ({c.Avisos.NoLeidos})" : "Avisos").Every(500);
            }
            else
            {
                v = Resources.Load<VisualTreeAsset>("MundoUI/Sesion").Instantiate();
                v.style.flexGrow = 1;
                v.Q(className: "barra-arriba").RemoveFromHierarchy();
                v.Q(className: "fondo").style.backgroundColor = new StyleColor(Color.clear);
                v.Q(className: "diagnostico").style.top = 16;
                dentro.Add(v);
            }
            var reloj = v.Q<Label>("reloj");
            VisualElement fr = v.Q("fichas-reloj"), fh = v.Q("fichas-heroe"), fc = v.Q("fichas-conexion");
            var ambitos = v.Q<ScrollView>("ambitos");
            var entidades = v.Q<ScrollView>("entidades");

            void Refrescar()
            {
                var s = c.Sesion;
                if (s == null || ambitos.panel == null || (dentro != null && dentro.ClassListContains("oculto"))) return;
                var b = s.Bienvenida;
                reloj.text = Instante.AFecha(s.AhoraMundo).ToString("dd/MM/yyyy  HH:mm:ss");
                Fichas(fr, ("Ritmo", s.RelojPausado ? "pausado" : $"×{s.FactorReloj:0.##}"));
                Fichas(fh, b == null ? new (string, string)[0] : c.HeroeId != null
                    ? new[] { ("Héroe", LineaDelHeroe(b, s.Almacen)), ("Id", c.HeroeId) }
                    : new[] { ("Héroe", "aún sin héroe"), ("Campamentos donde nacer", b.CampamentosParaElegir.Count.ToString()) });
                var conexion = new List<(string, string)>
                {
                    ("Estado", s.Estado.ToString()), ("Servidor", c.Servidor), ("Ida y vuelta", double.IsInfinity(s.Rtt) ? "—" : $"{s.Rtt:0} ms"),
                    ("Tramas", $"{s.TramasRecibidas} ({s.TramasZstd} en zstd)"), ("Mundo", b?.Mundo.Hash.Substring(0, 12) ?? "—"),
                    ("Reglas", c.Datos?.Huella?.Substring(0, 12) ?? "—"), ("Relieve", c.Relieve != null ? $"{c.Relieve.Columnas}×{c.Relieve.Filas} muestras" : "sin relieve"),
                };
                if (mapa != null) conexion.Add(("Mapa", $"{mapa.TeselasCargadas} teselas · {1f / Mathf.Max(Time.smoothDeltaTime, 1e-4f):0} fps"));
                Fichas(fc, conexion.ToArray());
                Fichas(ambitos, s.Almacen.Ambitos.OrderBy(x => x.Key).Select(x => (x.Key, $"v{x.Value.Version} · {x.Value.Entidades.Count} entidades{(x.Value.Niebla != null ? " · niebla" : "")}")).ToArray());
                var porTipo = s.Almacen.Ambitos.Values.SelectMany(x => x.Entidades).GroupBy(x => x.Key).Select(g => g.First().Value.CuerpoCase)
                    .GroupBy(x => x).OrderByDescending(g => g.Count()).Select(g => (g.Key.ToString(), g.Count().ToString())).ToArray();
                Fichas(entidades, porTipo.Length > 0 ? porTipo : new[] { ("—", "nada aún") });
            }
            Refrescar();
            v.schedule.Execute(Refrescar).Every(500);
        }

        static void Fichas(VisualElement donde, params (string etiqueta, string valor)[] fichas)
        {
            donde.Clear();
            foreach (var (e, valor) in fichas)
            {
                var f = Dialogo.Caja("ficha");
                f.Add(Dialogo.Texto(e, "ficha-etiqueta"));
                f.Add(Dialogo.Texto(valor, "ficha-valor"));
                donde.Add(f);
            }
        }

        // ─────────────────────────── la sesión por debajo ───────────────────────────

        /// <summary>Lo que la sesión cuenta sin que se le pregunte: avisos del servidor, el token caducado y «sesión reemplazada».</summary>
        void VigilarSesion(Sesion s)
        {
            if (sesionVigilada == s) return;
            sesionVigilada = s;
            s.AlAviso += a => { if (a.Codigo != "sesion.reemplazada") panelAvisos.Mostrar(string.IsNullOrEmpty(a.Mensaje) ? Textos.Explicar(a.Codigo) : a.Mensaje); };
            s.AlEstado += async e =>
            {
                if (e != EstadoSesion.Cerrada || s != c.Sesion || s.UltimoError == null) return;
                if (s.UltimoError.Codigo == "sesion_reemplazada")
                {
                    // No se vuelve a entrar sola: las dos sesiones se echarían una a la otra sin parar.
                    var volver = await dialogo.Abrir("La partida se ha abierto en otro sitio con esta cuenta, y esta sesión se ha cerrado.", "Sesión reemplazada", "Volver a entrar aquí", "Salir");
                    if (volver) _ = Partidas();
                    else Acceso(c.Nick);
                }
                else if (s.UltimoError.Codigo == "token_invalido") Acceso(c.Nick, "Tu sesión ha caducado: vuelve a entrar.");
                else _ = Partidas($"{Textos.Explicar(s.UltimoError.Codigo)} ({s.UltimoError.Mensaje})");
            };
        }

        async Task CerrarSesion()
        {
            await c.CerrarSesion();
            if (panelAvisos.Abierto) panelAvisos.Alternar();
            Acceso(c.Nick);
        }
    }
}
