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
    public sealed partial class PantallasDeMundo : MonoBehaviour
    {
        static readonly Regex Nick = new Regex(@"^[\p{L}\p{N}_.-]{3,32}$");

        ConexionDeMundo c;
        VisualElement pantalla, capaCarga, capaEncima;
        Dialogo dialogo;
        PanelAvisos panelAvisos;
        Sesion sesionVigilada;
        ConquestTactics.Mundo.Mapa.MapaDeMundo mapa;
        VisualElement diagnostico;

        /// <summary>
        /// Las pantallas con la sesión viva. Al volver a MundoScene desde la preparación o la batalla, la escena trae otro objeto «Mundo»: ese
        /// se quita y esta retoma la escena (<see cref="VolverAlMundo"/>).
        /// </summary>
        public static PantallasDeMundo Viva { get; private set; }
        bool sobrante;
        /// <summary>Los ajustes del panel, creados aquí: se van con las pantallas (si no, su panel sigue vivo y se queda con el ratón).</summary>
        PanelSettings ajustes;

        void Awake()
        {
            // Solo se aparta ante una viva que salió de su escena (DontDestroyOnLoad, camino de una batalla).
            if (Viva != null && Viva != this && Viva.gameObject.scene.name == "DontDestroyOnLoad")
            {
                sobrante = true;
                foreach (var componente in GetComponents<MonoBehaviour>()) componente.enabled = false;
                Destroy(gameObject);
                return;
            }
            Viva = this;
            c = GetComponent<ConexionDeMundo>();
            // El sigilo de las Facciones en los carteles del mapa (paso 8): el catálogo y la textura, desde aquí, que tiene la sesión.
            TexturaDeSigilo.Asegurar();
            ConquestTactics.Mundo.Mapa.CosasEnElMapa.SigiloDe = (faccionId, ancho) =>
                Sigilos.DeFaccion(c?.Sesion?.Almacen, faccionId) is Sigilo s ? TexturaDeSigilo.De(s, ancho * 2) : null;
            ajustes = ScriptableObject.CreateInstance<PanelSettings>();
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
            raiz.styleSheets.Add(Resources.Load<StyleSheet>("MundoUI/Faccion"));
            pantalla = Capa(raiz);
            capaCarga = Capa(raiz);
            capaEncima = Capa(raiz);
            dialogo = new Dialogo(capaEncima);
            panelAvisos = new PanelAvisos(c.Avisos, capaEncima)
            {
                VerInforme = id => AbrirInforme(id, null, false),
                HayInforme = id => c.Sesion?.Almacen.Buscar(TipoEntidad.Batalla, id) != null,
            };
        }

        static VisualElement Capa(VisualElement raiz)
        {
            var v = new VisualElement { pickingMode = PickingMode.Ignore };
            v.style.position = Position.Absolute;
            v.style.left = v.style.top = v.style.right = v.style.bottom = 0;
            raiz.Add(v);
            return v;
        }

        void OnDestroy()
        {
            // El mapa va en su propio objeto (con su cámara): se va con las pantallas, no se queda huérfano en la escena.
            QuitarMapa();
            CerrarBase();
            if (ajustes != null) Destroy(ajustes);
            if (Viva == this) Viva = null;
        }

        /// <summary>MundoScene, si está cargada; si no (la prueba monta las pantallas en su escena), la de este objeto.</summary>
        UnityEngine.SceneManagement.Scene EscenaDelMundo()
        {
            var s = UnityEngine.SceneManagement.SceneManager.GetSceneByName(EscenaMundo);
            return s.IsValid() && s.isLoaded ? s : gameObject.scene;
        }

        public const string EscenaMundo = "MundoScene";

        void Start()
        {
            if (sobrante) return;
            if (c.HayToken) _ = Partidas();
            else Acceso();
        }

        VisualElement Montar(string uxml)
        {
            CerrarBase();
            _ = cerrarEscenaEncima?.Invoke();
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
            // El error de acceso y partidas va en su fila, con el ⚠️ delante: se ve con él.
            if (l.parent != null && l.parent.ClassListContains("error-fila")) Ver(l.parent, !string.IsNullOrEmpty(texto));
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
            // Con héroe, el selector de personaje de Conquest en lugar de la tarjeta (salvo para enseñar un error).
            if (error == null && c.HeroeId != null) { VerHeroe(); return; }
            lista.Clear();
            var tarjeta = Dialogo.Caja("partida partida--elegida");
            tarjeta.Add(Dialogo.Texto("Mundo", "partida-nombre"));
            tarjeta.Add(Dialogo.Texto(LineaDelHeroe(b, c.Sesion.Almacen, b.HasHeroeId ? b.HeroeId : null), "partida-heroe"));
            tarjeta.Add(Dialogo.Texto($"{c.Servidor} · mundo {b.Mundo.Hash.Substring(0, Math.Min(8, b.Mundo.Hash.Length))}", "partida-detalle"));
            lista.Add(tarjeta);
            entrar.SetEnabled(true);
            entrar.clicked += () => _ = EntrarEnLaPartida();
        }

        /// <summary>La escena del selector de personaje de Conquest.</summary>
        const string Selector = "CharacterselecctionScene";

        /// <summary>El selector de personaje de Conquest, encima (como el creador): el héroe de la cuenta en 3D con su aspecto y su armadura, su
        /// nombre, nivel, clase y Facción. «Entrar al mundo» sigue con la carga y el mapa; «Cerrar sesión», al acceso. Necesita las reglas
        /// (la clase y el tipo de su armadura), que se leen aquí en vez de en la carga.</summary>
        async void VerHeroe()
        {
            if (SelectorDePersonaje.Heroe != null) return;
            DatosDeJuego datos;
            try { datos = await c.CargarDatos(); }
            catch (Exception e) { await Partidas("No se pudieron leer las reglas: " + e.Message); return; }
            if (this == null || c.Sesion == null || c.HeroeId == null) return;
            var raiz = pantalla.parent;
            var mundo = EscenaDelMundo();

            Action volverCamaras = null;
            async Task Cerrar()
            {
                if (SelectorDePersonaje.Heroe == null) return;
                SelectorDePersonaje.Heroe = null;
                cerrarEscenaEncima = null;
                if (mundo.IsValid() && mundo.isLoaded) UnityEngine.SceneManagement.SceneManager.SetActiveScene(mundo);
                var descarga = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(Selector);
                while (descarga != null && !descarga.isDone) await Task.Yield();
                volverCamaras?.Invoke();
                if (this != null) raiz.style.display = DisplayStyle.Flex;
            }

            SelectorDePersonaje.Heroe = HeroeParaVer(datos);
            SelectorDePersonaje.Heroe.Entrar = async () => { await Cerrar(); if (this != null) _ = EntrarEnLaPartida(); };
            SelectorDePersonaje.Heroe.CerrarSesion = async () => { await Cerrar(); if (this != null) _ = CerrarSesion(); };
            cerrarEscenaEncima = Cerrar;
            raiz.style.display = DisplayStyle.None;
            volverCamaras = ApartarCamarasDelMundo();
            var carga = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(Selector, UnityEngine.SceneManagement.LoadSceneMode.Additive);
            while (!carga.isDone) await Task.Yield();
            var escena = UnityEngine.SceneManagement.SceneManager.GetSceneByName(Selector);
            if (escena.IsValid() && escena.isLoaded) UnityEngine.SceneManagement.SceneManager.SetActiveScene(escena);
        }

        /// <summary>Apaga las cámaras (y los oídos) de la escena del mundo mientras otra escena de Conquest va encima, y devuelve cómo volver a
        /// encenderlas. Hacen falta todas: la de MundoScene («Camara», profundidad 0 y fondo liso) y la del mapa pintan por encima de las de
        /// AvatarCreator y el selector (profundidad −1) y las tapan de negro. Se apagan los componentes, no los objetos.</summary>
        Action ApartarCamarasDelMundo()
        {
            var camaras = new List<Camera>();
            var oidos = new List<AudioListener>();
            foreach (var raiz in EscenaDelMundo().GetRootGameObjects())
            {
                foreach (var cam in raiz.GetComponentsInChildren<Camera>()) if (cam.enabled) { cam.enabled = false; camaras.Add(cam); }
                foreach (var oido in raiz.GetComponentsInChildren<AudioListener>()) if (oido.enabled) { oido.enabled = false; oidos.Add(oido); }
            }
            return () =>
            {
                foreach (var cam in camaras) if (cam != null) cam.enabled = true;
                foreach (var oido in oidos) if (oido != null) oido.enabled = true;
            };
        }

        /// <summary>El héroe de la sesión para las escenas de Conquest (el selector y los interiores): nombre, nivel, Facción, clase, armadura y
        /// aspecto.</summary>
        SelectorDePersonaje.HeroeParaVer HeroeParaVer(DatosDeJuego datos)
        {
            var s = c.Sesion;
            var b = s.Bienvenida;
            var h = s.Almacen.Buscar(TipoEntidad.Heroe, c.HeroeId)?.Heroe;
            var clase = ClaseDeHeroe.Leer(datos).FirstOrDefault(k => k.Id == h?.ClassDefinitionId);
            var faccionId = h != null && h.HasFaccionId ? h.FaccionId : b.HasFaccionId ? b.FaccionId : null;
            return new SelectorDePersonaje.HeroeParaVer
            {
                Nombre = Textos.NombreDeHeroe(s.Almacen, c.HeroeId),
                Nivel = h?.Nivel ?? 1,
                Faccion = faccionId == null ? "Sin Facción" : NombreDeFaccion(s.Almacen, faccionId) ?? "Facción",
                Sigilo = faccionId == null ? null : Sigilos.DeFaccion(s.Almacen, faccionId),
                Clase = clase,
                Armadura = ClaseDeHeroe.TipoDeArmadura(datos, h?.Equipamiento?.Torso?.ItemDefinitionId) ?? clase?.ArmaduraInicial,
                Femenino = h?.Femenino ?? false,
                Cabeza = h?.Avatar?.CabezaId, Pelo = h?.Avatar?.PeloId, Barba = h?.Avatar?.BarbaId, Cejas = h?.Avatar?.CejasId,
                Piezas = PiezasPuestas(h),
            };
        }

        // ─────────────────────────── el interior de un campamento ───────────────────────────

        /// <summary>La escena base de los interiores de Conquest.</summary>
        const string Feudo = "FeudoScene";

        /// <summary>El campamento de mercenarios donde está dentro el héroe, con su planta ya llegada (ámbito CAMPAMENTO); si no, null.</summary>
        CampamentoMercenarios CampamentoDondeEsta()
        {
            var u = HeroeAhora()?.Ubicacion;
            if (u == null || u.DondeCase != UbicacionHeroe.DondeOneofCase.CampamentoId) return null;
            var camp = c.Sesion.Almacen.Buscar(TipoEntidad.CampamentoMercenarios, u.CampamentoId)?.CampamentoMercenarios;
            return camp != null && camp.Planta.Count > 0 ? camp : null;
        }

        /// <summary>El nombre que se enseña de un campamento: el de su región, como en «Nace tu héroe».</summary>
        string NombreDeCampamento(string id)
        {
            var b = c.Sesion.Bienvenida;
            // Los de la Bienvenida (sin héroe) o, ya nacido, los del artefacto: `campamento-N` es el `camp-N` de la sesión.
            var lista = b.CampamentosParaElegir.Count > 0
                ? b.CampamentosParaElegir.Select(x => new AnalisisDeCampamento { Id = x.CampamentoId, X = x.Posicion.X / 1000.0, Y = x.Posicion.Y / 1000.0 }).ToList()
                : (c.Artefacto?.Campamentos.Select(x => new AnalisisDeCampamento { Id = "camp-" + x.Id.Substring(x.Id.LastIndexOf('-') + 1), X = x.Posicion.X / 1000.0, Y = x.Posicion.Y / 1000.0 }) ?? Enumerable.Empty<AnalisisDeCampamento>()).ToList();
            foreach (var a in lista) a.Lugar = AnalisisDeCampamento.Nombre(a.Id);
            var tamano = c.Artefacto?.Manifiesto?.Tamano;
            if (tamano != null) AnalisisDeCampamento.PonerLugares(lista, tamano.X / 1000.0, tamano.Y / 1000.0);
            return lista.FirstOrDefault(x => x.Id == id)?.Lugar ?? AnalisisDeCampamento.Nombre(id);
        }

        /// <summary>Con el botón del ratón pulsado no se repinta nada solo: rehecho entre pulsar y soltar, el clic se perdería (el botón pulsado ya
        /// no está). Pasó en el HUD de la plaza, en la Base y en el informe.</summary>
        static bool RatonPulsado() => UnityEngine.InputSystem.Mouse.current?.leftButton.isPressed == true;

        /// <summary>Lo que una pantalla rehace sola cada `ms` (por firma, versión o reloj), con la guarda de `RatonPulsado`. Toda pantalla que se
        /// repinte por temporizador va por aquí.</summary>
        static IVisualElementScheduledItem RepintarCada(VisualElement v, long ms, Action repintar) =>
            v.schedule.Execute(() => { if (!RatonPulsado()) repintar(); }).Every(ms);

        /// <summary>Un rectángulo de la planta (esquina, ancho y alto en unidades locales) por su centro.</summary>
        static SelectorDePersonaje.PiezaDePlanta Pieza(RectLocal r, string tipo) =>
            new SelectorDePersonaje.PiezaDePlanta { Tipo = tipo, X = r.X + r.Ancho / 2.0, Y = r.Y + r.Alto / 2.0, Ancho = r.Ancho, Alto = r.Alto };

        /// <summary>El interior del campamento donde está el héroe (paso 4 del bloque 4): FeudoScene encima de esta escena, en modo «desde el
        /// motor», con el campamento montado desde la planta del motor y el héroe andando; aquí, la cabecera (dónde estás, avisos, «Salir al
        /// mundo», cerrar sesión). La sesión sigue viva en este objeto. Al salir al mundo, el mapa con la columna fuera.</summary>
        async void Interior(CampamentoMercenarios camp)
        {
            if (SelectorDePersonaje.Interior != null) return;
            var v = Montar("Interior");
            var lugar = NombreDeCampamento(camp.Id);
            v.Q<Label>("lugar").text = lugar.ToUpperInvariant();
            // Lo que el tablón de mercenarios deja atacar (bandidos, plazas en guerra) llega por la `VISTA`, como en la plaza: dentro sin haber pasado
            // por el mapa aún no se ha pedido; se pide la del mundo entero, como el mapa.
            if (c.Sesion != null && !c.Sesion.TieneVista && c.Relieve != null)
                c.Sesion.CambiarVista(new Rectangulo { Min = new Punto(), Max = new Punto { X = c.Relieve.Columnas * (long)(c.Relieve.Paso * 1000), Y = c.Relieve.Filas * (long)(c.Relieve.Paso * 1000) } });
            var botonAvisos = v.Q<Button>("avisos");
            botonAvisos.clicked += panelAvisos.Alternar;
            var cartel = v.Q<Label>("cartel");
            // El icono del campamento junto al nombre (`htmlBlasonInterior`: la tienda en oro; 46 px allí, 40 aquí para caber en la barra de 52).
            var cabeceraIcono = IconosDelMundo.Icono("parte", "campamento", 40);
            cabeceraIcono.AddToClassList("cabecera-icono");
            v.Q<Label>("lugar").parent.parent.Insert(0, cabeceraIcono);
            // La placa «Tu almacén» del pie (`htmlPlacaDeRecursos`): el emoji y la cifra de cada recurso; se rehace solo si cambia.
            var placaAlmacen = v.Q("placa-almacen");
            string almacenPintado = null, vestidoCon = null;
            SelectorDePersonaje.InteriorParaVer interior = null;
            // Lo que cambia mientras estás dentro: residentes y si resides, los avisos, y el cartel de la zona donde estás (la tecla y qué hay).
            RepintarCada(v, 100, () =>
            {
                PintarAvisos(botonAvisos);
                var ahora = CampamentoActual(camp.Id) ?? camp;
                var yo = HeroeAhora();
                v.Q<Label>("lugar-detalle").text = $"Campamento de mercenarios · {ahora.ResidentesIds.Count} residentes · {(yo != null && ahora.ResidentesIds.Contains(yo.Id) ? "tu residencia" : "de paso")}";
                var almacen = yo?.AlmacenPersonal?.PorId.Where(kv => kv.Value >= 1000).ToList() ?? new List<KeyValuePair<string, long>>();
                var firma = string.Join("|", almacen.OrderBy(kv => kv.Key).Select(kv => kv.Key + (kv.Value / 1000)));
                if (firma != almacenPintado)
                {
                    almacenPintado = firma;
                    var piezas = placaAlmacen.Q("placa-almacen-piezas");
                    piezas.Clear();
                    piezas.Add(PiezasDeAlmacen(almacen, "almacen-pie"));
                    Ver(placaAlmacen, almacen.Count > 0);
                }
                // Si cambia lo que lleva puesto (equipar en la Base), el héroe del interior se vuelve a vestir.
                var puesto = string.Join("|", (PiezasPuestas(yo) ?? new Dictionary<string, string>()).OrderBy(kv => kv.Key).Select(kv => kv.Key + "=" + kv.Value));
                if (interior != null && puesto != vestidoCon)
                {
                    vestidoCon = puesto;
                    interior.Heroe = HeroeParaVer(c.Datos);
                    var local = UnityEngine.Object.FindObjectsByType<ConquestTactics.Visual.EntityVisualSync>(FindObjectsSortMode.None).FirstOrDefault(x => x.IsLocalHero);
                    if (local != null) VestidoDelMotor.Vestir(local.transform, interior.Heroe);
                }
                var texto = interior != null && !interior.PanelAbierto && interior.ZonaCercana != null ? CartelDeZona(interior.ZonaCercana, ahora) : null;
                Poner(cartel, texto);
            });
            v.Q<Button>("cerrar-sesion").clicked += async () =>
            {
                if (await dialogo.Abrir("Tu héroe sale del mundo y se cierra la sesión en este equipo.", "¿Cerrar sesión?", "Cerrar sesión")) _ = CerrarSesion();
            };
            v.Q<Button>("salir-al-mundo").clicked += () => VentanaDeSalida(camp.Id, lugar);
            BarraDelJugador(v);

            interior = new SelectorDePersonaje.InteriorParaVer { Heroe = HeroeParaVer(c.Datos) };
            foreach (var e in camp.Planta)
                interior.Edificios.Add(new SelectorDePersonaje.PiezaDePlanta { Tipo = e.Tipo, Zona = ZonaDePieza(e.Tipo), X = e.Posicion.X / 1000.0, Y = e.Posicion.Y / 1000.0, Ancho = e.Ancho, Alto = e.Alto });
            // F en una zona: su panel (paso 4B).
            interior.AlInteractuar = zona =>
            {
                if (zona == "taberna") PanelTaberna(camp.Id);
                else if (zona == "mercado") PanelMercado(camp.Id);
                else if (zona == "militar") PanelTropa(camp.Id);
                else if (zona == "puerta") VentanaDeSalida(camp.Id, lugar);
            };
            if (camp.Trazado != null) foreach (var r in camp.Trazado.Calles) interior.Calles.Add(Pieza(r, "calle"));
            foreach (var r in camp.Empalizada) interior.Empalizada.Add(Pieza(r, "empalizada"));
            foreach (var r in camp.EmpalizadaPuerta) interior.Puerta.Add(Pieza(r, "puerta"));

            // La cámara del mundo se aparta (FeudoScene trae la suya, que sigue al héroe).
            var volverCamaras = ApartarCamarasDelMundo();
            var mundo = EscenaDelMundo();
            async Task Cerrar()
            {
                if (SelectorDePersonaje.Interior == null) return;
                cerrarEscenaEncima = null;
                cerrarInterior = null;
                if (mundo.IsValid() && mundo.isLoaded) UnityEngine.SceneManagement.SceneManager.SetActiveScene(mundo);
                var descarga = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(Feudo);
                while (descarga != null && !descarga.isDone) await Task.Yield();
                SelectorDePersonaje.Interior = null;
                volverCamaras();
            }
            SelectorDePersonaje.Interior = interior;
            cerrarEscenaEncima = Cerrar;
            cerrarInterior = Cerrar;
            var carga = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(Feudo, UnityEngine.SceneManagement.LoadSceneMode.Additive);
            while (!carga.isDone) await Task.Yield();
            var escena = UnityEngine.SceneManagement.SceneManager.GetSceneByName(Feudo);
            if (escena.IsValid() && escena.isLoaded) UnityEngine.SceneManagement.SceneManager.SetActiveScene(escena); // su luz, y el héroe nace en ella
        }

        /// <summary>Lo que lleva puesto el héroe, por hueco, con el id de su pieza en `OBJETOS` (para vestirlo pieza a pieza); null sin héroe.</summary>
        static Dictionary<string, string> PiezasPuestas(Heroe h)
        {
            if (h == null) return null;
            var e = h.Equipamiento ?? new Equipamiento();
            var piezas = new Dictionary<string, string>();
            foreach (var (hueco, pieza) in new[] { ("arma", e.Arma), ("casco", e.Casco), ("torso", e.Torso), ("guantes", e.Guantes), ("pantalones", e.Pantalones), ("botas", e.Botas), ("montura", e.Montura) })
                if (pieza != null) piezas[hueco] = pieza.ItemDefinitionId;
            return piezas;
        }

        /// <summary>Cierra el interior abierto (lo usa «Salir al mundo» antes de volver al mapa).</summary>
        Func<Task> cerrarInterior;

        /// <summary>«Salir al mundo» desde un campamento, calcada de la del prototipo (BronzeAgeClient: `ui/ventanaSalida.ts`, `cargaDeSalida.ts`,
        /// `velocidadColumna.ts`): ventana ancha con título y aspa; arriba, a la izquierda cómo sales (y los víveres) y a la derecha la carga del carro;
        /// abajo, la velocidad de la columna, la tropa con su Liderazgo y su loadout, y el riel con las tarjetas de tropa de Conquest, el «+» y los huecos.
        /// Si resides, sale cargado el loadout activo (o todo, si no tiene escuadras) y se quita con ✕ o se añade con «+»; «Guardar loadout» lo deja en
        /// el loadout elegido. Si no resides, sales con la columna con la que entraste, tal cual. El modo «Ejército» es del paso 11: aquí no sale.
        /// Desde una plaza (`plazaId`, paso 7): de tu residencia sales con la tropa de tu campamento y la carga del almacén de la plaza
        /// (`salirAlMundo`); de otra, retomas tu columna aparcada a la puerta (`salirDeAsentamiento`). Los víveres, solo la línea, como el prototipo.</summary>
        void VentanaDeSalida(string campId, string lugar, string plazaId = null)
        {
            var h = HeroeAhora();
            var camp = plazaId == null ? CampamentoActual(campId) : null;
            var plaza = plazaId == null ? null : c.Sesion.Almacen.Buscar(TipoEntidad.Asentamiento, plazaId)?.Asentamiento;
            var interior = SelectorDePersonaje.Interior;
            if (h == null || (camp == null && plaza == null) || interior == null || interior.PanelAbierto) return;
            // En una convocatoria: su preparación (paso 11).
            if (MiConvocatoria(h) is Convocatoria cv) { VentanaDePreparacion(cv, campId, lugar, plazaId); return; }
            bool resides = plaza != null ? h.HasResidenciaId && h.ResidenciaId == plazaId : camp.ResidentesIds.Contains(h.Id);
            var a = c.Sesion.Almacen;
            interior.PanelAbierto = true;
            var cartelDeZona = pantalla.Q<Label>("cartel");
            if (cartelDeZona != null) Ver(cartelDeZona, false);
            var velo = Dialogo.Caja("dialogo-velo");
            velo.name = "ventana-salida";
            var placa = Dialogo.Caja("placa salida-ventana");
            velo.Add(placa);
            void Cerrar() { velo.RemoveFromHierarchy(); interior.PanelAbierto = false; }
            velo.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Escape) Cerrar(); }, TrickleDown.TrickleDown);

            // La cabecera: el título y el aspa.
            var cabeza = Dialogo.Caja("salida-cab");
            cabeza.Add(Dialogo.Texto($"SALIR DE {lugar.ToUpperInvariant()}", "dialogo-titulo salida-titulo"));
            var aspa = Dialogo.Boton("×", "boton-secundario salida-cerrar", Cerrar);
            aspa.name = "cancelar";
            cabeza.Add(aspa);
            placa.Add(cabeza);

            // Arriba, dos columnas.
            var cuerpo = Dialogo.Caja("salida-cuerpo");
            placa.Add(cuerpo);
            ScrollView Columna(string clases)
            {
                var col = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
                foreach (var cl in clases.Split(' ')) col.AddToClassList(cl);
                cuerpo.Add(col);
                return col;
            }
            var izquierda = Columna("salida-col");
            var derecha = Columna("salida-col salida-col--derecha");

            // Izquierda: cómo sales, los víveres (con «Llenar víveres»: el prototipo lo tiene en la ficha de la puerta, aquí se queda a mano) y
            // el recuadro «Cómo sales» con la columna personal (el ejército, en el paso 11).
            izquierda.Add(Dialogo.Texto(resides ? "Sales al mapa por la puerta. Tus víveres van siempre contigo."
                : plaza != null ? "No resides aquí: retomas tu columna, aparcada a la puerta, con su tropa y su carro."
                : "No resides aquí: sales con la columna con la que entraste.", "dialogo-texto"));
            var error = Dialogo.Texto("", "error oculto");
            var filaViveres = Dialogo.Caja("salida-fila");
            izquierda.Add(filaViveres);
            var maximoViveres = c.Datos.Numero("documentos.Movimiento_Ejercitos.LOGISTICA.capacidadViveresPorHeroe");
            var textoViveres = Dialogo.Texto("", "campo-ayuda");
            textoViveres.name = "viveres";
            filaViveres.Add(textoViveres);
            Button repostar = null;
            void PintarViveres()
            {
                var yo = HeroeAhora();
                var tienes = Math.Ceiling(Instante.Valor(yo?.Viveres, c.Sesion.AhoraMundo) / 1000);
                textoViveres.text = plaza != null ? $"Víveres: {tienes:0} de trigo." : $"Víveres: {tienes:0} de {maximoViveres:0} de trigo.";
                Ver(repostar, plaza == null && tienes < maximoViveres - 0.5);
            }
            repostar = IconosDelMundo.BotonConGlifo("Llenar víveres", ("glifo", "viveres"), "boton-secundario", async () =>
            {
                repostar.SetEnabled(false);
                Poner(error, null);
                try
                {
                    var r = await c.Sesion.Ordenar(new Orden { RepostarEnCampamento = new OrdenRepostarEnCampamento { CampamentoId = campId } });
                    if (r.Resultado != Resultado.OrdenAceptada) Poner(error, Textos.Explicar(r.Rechazo));
                }
                catch (ErrorDeSesion e) { Poner(error, e.Message); }
                repostar.SetEnabled(true);
                await Task.Delay(400);
                PintarViveres();
            });
            repostar.name = "repostar";
            filaViveres.Add(repostar);
            PintarViveres();
            var recuadro = Dialogo.Caja("salida-recuadro");
            recuadro.Add(ConAyuda(Dialogo.Texto("Cómo sales", "salida-recuadro-titulo"),
                "Columna personal: por tu cuenta, con el rumbo libre. Ejército: salís juntos varios héroes de tu Facción; lo dirige su Líder con clics en el mapa. Para unirte a uno que se prepara, pulsa «Unirme»: sales con la tropa y la carga que marques. Para convocar el tuyo, elige quién puede unirse y pulsa «Convocar ejército». Un ejército espera dentro, sin límite de tiempo, hasta que su Líder pulse «Salir con el ejército» o cancele. Necesita al menos una escuadra."));
            // El ejército, desde tu plaza de residencia o desde un campamento (`admiteEjercito`).
            var admiteEjercito = plaza == null || resides;
            // Cada grupo de opciones, en su GroupBox: si no, todos los RadioButton del panel serían un solo grupo.
            var modos = new GroupBox();
            modos.AddToClassList("salida-modos");
            recuadro.Add(modos);
            var columnaPersonal = new RadioButton("Columna personal") { value = !(admiteEjercito && salirComoEjercito), name = "columna-personal" };
            modos.Add(columnaPersonal);
            var bloqueEjercito = Dialogo.Caja("salida-ejercito");
            bloqueEjercito.name = "bloque-ejercito";
            RadioButton comoEjercito = null;
            if (admiteEjercito)
            {
                comoEjercito = new RadioButton("Ejército") { value = salirComoEjercito, name = "ejercito" };
                modos.Add(comoEjercito);
                recuadro.Add(bloqueEjercito);
            }
            if (plaza == null || resides) izquierda.Add(recuadro);

            // Derecha: la carga del carro (de tu almacén personal si resides; si no, lo que ya lleva el carro de tu columna).
            var campos = new List<(IntegerField campo, string recurso)>();
            var capacidad = c.Datos.Numero("documentos.Movimiento_Ejercitos.LOGISTICA.capacidadCarroPorJugador");
            var columna = ColumnaDelHeroe(h);
            if (resides)
            {
                var total = Dialogo.Texto("", "salida-carga-total");
                total.name = "carga-total";
                var de = plaza != null ? "el almacén de la plaza" : "tu almacén personal";
                derecha.Add(ConAyuda(IconoDelante(Dialogo.Texto("Carga del carro", "salida-recuadro-titulo"), total),
                    $"Lo que metas en el carro sale contigo, de {de}. Es lo que llevas para comer o gastar fuera, y lo que se arriesga si pierdes un combate (se pierde la mitad)."));
                var almacen = (plaza != null
                    ? plaza.Almacen.Select(kv => new KeyValuePair<string, long>(kv.Key, (long)Instante.Valor(kv.Value.Cantidad, c.Sesion.AhoraMundo)))
                    : h.AlmacenPersonal?.PorId.AsEnumerable() ?? Enumerable.Empty<KeyValuePair<string, long>>())
                    .Where(kv => kv.Value >= 1000).OrderBy(kv => kv.Key).ToList();
                if (almacen.Count == 0) derecha.Add(Dialogo.Texto($"No hay nada que cargar en {de}.", "campo-ayuda"));
                void Total()
                {
                    var lleva = campos.Sum(x => x.campo.value);
                    total.text = $"{lleva} / {capacidad:0}";
                    total.EnableInClassList("excedido", lleva > capacidad);
                }
                foreach (var kv in almacen)
                {
                    var hay = (int)(kv.Value / 1000);
                    var fila = Dialogo.Caja("salida-fila carro-fila");
                    var nombre = IconosDelMundo.ConIcono("recurso", kv.Key, Dialogo.Texto(CosasDelMapa.NombreDeRecurso(kv.Key), "aviso-texto"), 20);
                    nombre.style.flexGrow = 1;
                    fila.Add(nombre);
                    fila.Add(Dialogo.Texto($"hay {hay}", "campo-ayuda carro-hay"));
                    var campo = new IntegerField { value = 0, name = "carga-" + kv.Key };
                    campo.RegisterValueChangedCallback(ev => { campo.SetValueWithoutNotify(Math.Max(0, Math.Min(hay, ev.newValue))); Total(); });
                    fila.Add(campo);
                    var recurso = kv.Key;
                    var todo = Dialogo.Boton("Todo", "boton-secundario", () =>
                    {
                        var otros = campos.Where(x => x.recurso != recurso).Sum(x => x.campo.value);
                        campo.value = Math.Max(0, Math.Min(hay, (int)capacidad - otros));
                    });
                    todo.name = "todo-" + kv.Key;
                    fila.Add(todo);
                    derecha.Add(fila);
                    campos.Add((campo, kv.Key));
                }
                Total();
            }
            else
            {
                var carga = columna?.Suministro?.PorId.Where(kv => kv.Value >= 1000).OrderBy(kv => kv.Key).ToList() ?? new List<KeyValuePair<string, long>>();
                derecha.Add(Dialogo.Texto($"Carro de tu columna · {carga.Sum(kv => kv.Value / 1000)} / {capacidad:0}", "salida-recuadro-titulo"));
                if (carga.Count == 0) derecha.Add(Dialogo.Texto("Vacío.", "campo-ayuda"));
                foreach (var kv in carga)
                {
                    var fila = Dialogo.Caja("salida-fila carro-fila");
                    fila.Add(IconosDelMundo.ConIcono("recurso", kv.Key, Dialogo.Texto(CosasDelMapa.NombreDeRecurso(kv.Key), "aviso-texto"), 20));
                    fila.Add(Dialogo.Texto((kv.Value / 1000).ToString(), "aviso-titulo"));
                    derecha.Add(fila);
                }
            }

            // Abajo: la velocidad, la tropa y el riel, y SALIR.
            var pie = Dialogo.Caja("salida-pie");
            placa.Add(pie);
            var tropaCaja = new VisualElement();
            pie.Add(tropaCaja);
            var acciones = Dialogo.Caja("salida-acciones");
            acciones.Add(error);
            pie.Add(acciones);

            // La tropa: la del campamento si resides (sale el loadout activo, o todo); si no, la de tu columna, sin elegir.
            var disponibles = h.EscuadronesIds.Select(id => a.Buscar(TipoEntidad.Escuadron, id)?.Escuadron).Where(e => e != null && e.Cantidad > 0 &&
                (resides ? e.Contenedor?.DondeCase == ContenedorEscuadron.DondeOneofCase.Campamento && !e.EnGuarnicion   // lo de la guarnición se queda (`ventanaSalida.ts`)
                         : e.Contenedor?.DondeCase == ContenedorEscuadron.DondeOneofCase.EjercitoId && columna != null && e.Contenedor.EjercitoId == columna.Id)).ToList();
            List<string> DelLoadout(Loadout l) => l == null || l.SquadIds.Count == 0 ? disponibles.Select(s => s.Id).ToList()
                : disponibles.Where(s => l.SquadIds.Contains(s.Id)).Select(s => s.Id).ToList();
            var loadout = h.Loadouts.FirstOrDefault(x => x.Activo) ?? h.Loadouts.FirstOrDefault();
            var elegidas = resides ? DelLoadout(loadout) : disponibles.Select(s => s.Id).ToList();
            bool eligiendo = false;
            int Coste(Escuadron s) => LiderazgoDe(s.TropaId) ?? 0;
            Button salir = null;

            void PintarTropa()
            {
                tropaCaja.Clear();
                var lista = elegidas.Select(id => disponibles.FirstOrDefault(s => s.Id == id)).Where(s => s != null).ToList();
                var usado = lista.Sum(Coste);
                var tope = (int)h.LiderazgoBase;
                var libre = tope - usado;
                var candidatas = resides ? disponibles.Where(s => !elegidas.Contains(s.Id)).ToList() : new List<Escuadron>();
                var caben = candidatas.Where(s => Coste(s) <= libre).ToList();

                tropaCaja.Add(LineaDeVelocidad(lista.Select(s => s.TropaId).ToList()));

                var cab = Dialogo.Caja("salida-tropa-cab");
                cab.Add(ConAyuda(Dialogo.Texto("TROPA", "salida-recuadro-titulo"), resides
                    ? "Sale cargado tu loadout activo. Quita escuadras con ✕ o añade con «+»: vale solo para esta salida, salvo que pulses «Guardar loadout», que lo guarda en el loadout elegido."
                    : "Sales con la columna con la que entraste: no se cambia nada."));
                var lid = Dialogo.Texto($"Liderazgo {usado} / {tope}", "salida-liderazgo");
                lid.name = "liderazgo";
                lid.EnableInClassList("excedido", libre < 0);
                cab.Add(lid);
                if (resides && h.Loadouts.Count > 0)
                {
                    var opciones = h.Loadouts.Select(x => x.DisplayName + (x.Activo ? " (activo)" : "")).ToList();
                    var elegido = new DropdownField("Loadout", opciones, Math.Max(0, h.Loadouts.IndexOf(loadout))) { name = "loadout" };
                    elegido.AddToClassList("salida-loadout");
                    elegido.RegisterValueChangedCallback(_ =>
                    {
                        loadout = h.Loadouts[elegido.index];
                        elegidas = DelLoadout(loadout);
                        eligiendo = false;
                        PintarTropa();
                    });
                    cab.Add(elegido);
                    var delLoadout = DelLoadout(loadout).OrderBy(x => x).ToList();
                    bool cambiado = !delLoadout.SequenceEqual(elegidas.OrderBy(x => x));
                    Button guardar = null;
                    guardar = Dialogo.Boton("Guardar loadout", "boton-secundario salida-guardar", async () =>
                    {
                        // Las escuadras del loadout que no están aquí (en guarnición, escoltando…) se quedan en él si siguen siendo tuyas, como el prototipo.
                        var aqui = new HashSet<string>(disponibles.Select(s => s.Id));
                        var o = new OrdenGuardarLoadout { LoadoutId = loadout.Id, DisplayName = loadout.DisplayName };
                        o.SquadIds.AddRange(loadout.SquadIds.Where(id => !aqui.Contains(id) && h.EscuadronesIds.Contains(id)));
                        o.SquadIds.AddRange(elegidas);
                        guardar.SetEnabled(false);
                        Poner(error, null);
                        try
                        {
                            var r = await c.Sesion.Ordenar(new Orden { GuardarLoadout = o });
                            if (r.Resultado != Resultado.OrdenAceptada) { Poner(error, Textos.Explicar(r.Rechazo)); guardar.SetEnabled(true); }
                            else panelAvisos.Mostrar($"Loadout «{loadout.DisplayName}» guardado.", false);
                        }
                        catch (ErrorDeSesion x) { Poner(error, x.Message); guardar.SetEnabled(true); }
                    });
                    guardar.name = "guardar-loadout";
                    guardar.SetEnabled(cambiado && libre >= 0);
                    cab.Add(guardar);
                }
                tropaCaja.Add(cab);

                // El riel: las tarjetas (con su ✕), el «+» si hay algo que quepa, y los huecos vacíos hasta completar (como ESPACIOS del prototipo,
                // que con las tarjetas verticales son más).
                var riel = new ScrollView(ScrollViewMode.Horizontal) { verticalScrollerVisibility = ScrollerVisibility.Hidden, name = "riel" };
                riel.AddToClassList("salida-riel");
                var mas = resides && caben.Count > 0;
                var espacios = Math.Max(EspaciosDeTropa, lista.Count + (mas ? 1 : 0));
                for (int i = 0; i < espacios; i++)
                {
                    if (i < lista.Count)
                    {
                        var s = lista[i];
                        var hueco = Dialogo.Caja("salida-hueco salida-hueco-tropa");
                        hueco.name = "hueco-" + s.Id;
                        hueco.tooltip = $"{(string.IsNullOrEmpty(s.Nombre) ? NombreDeTropa(s.TropaId) : s.Nombre)} · {s.Cantidad} hombres";
                        var total = (int?)c.Datos.TropasReclutables?.FirstOrDefault(x => (string)x["id"] == s.TropaId)?["unidadesPorDefecto"];
                        var tarjeta = PiezasDeTropa.Tarjeta(s.TropaId, Coste(s), (int)s.Cantidad, total, null, AnchoDeHueco);
                        if (tarjeta != null) { tarjeta.style.marginRight = 0; tarjeta.style.marginBottom = 0; hueco.Add(tarjeta); }
                        if (resides)
                        {
                            var id = s.Id;
                            var quitar = Dialogo.Boton("✕", "salida-hueco-quitar", () => { elegidas.Remove(id); PintarTropa(); });
                            quitar.name = "quitar-" + id;
                            quitar.tooltip = "Quitar de esta salida";
                            hueco.Add(quitar);
                            // ◀ ▶: el orden de combate (la primera entra primero). Se guarda en el héroe al salir (`ordenarEscuadras`).
                            var flechas = Dialogo.Caja("salida-hueco-orden");
                            var pos = i;
                            var antes = Dialogo.Boton("◀", "salida-hueco-flecha", () => { (elegidas[pos - 1], elegidas[pos]) = (elegidas[pos], elegidas[pos - 1]); PintarTropa(); });
                            antes.name = "antes-" + id;
                            antes.tooltip = "Antes en el combate";
                            antes.SetEnabled(i > 0);
                            var despues = Dialogo.Boton("▶", "salida-hueco-flecha", () => { (elegidas[pos + 1], elegidas[pos]) = (elegidas[pos], elegidas[pos + 1]); PintarTropa(); });
                            despues.name = "despues-" + id;
                            despues.tooltip = "Después en el combate";
                            despues.SetEnabled(i < lista.Count - 1);
                            flechas.Add(antes);
                            flechas.Add(despues);
                            hueco.Add(flechas);
                        }
                        riel.Add(hueco);
                    }
                    else if (mas && i == lista.Count)
                    {
                        var boton = Dialogo.Boton("+", "salida-hueco salida-hueco-mas", () => { eligiendo = !eligiendo; PintarTropa(); });
                        boton.name = "salida-mas";
                        boton.tooltip = "Añadir una escuadra";
                        boton.EnableInClassList("activo", eligiendo);
                        riel.Add(boton);
                    }
                    else riel.Add(Dialogo.Caja("salida-hueco salida-hueco-vacio"));
                }
                tropaCaja.Add(riel);

                // Lo que se puede añadir, al pulsar «+» (solo lo que cabe: lo demás no se ofrece).
                if (eligiendo && mas)
                {
                    var elegir = Dialogo.Caja("salida-elegir fila-envuelta");
                    elegir.name = "elegir";
                    foreach (var s in caben)
                    {
                        var id = s.Id;
                        var candidata = new Button(() => { elegidas.Add(id); eligiendo = disponibles.Any(x => !elegidas.Contains(x.Id)); PintarTropa(); }) { name = "anadir-" + id };
                        candidata.AddToClassList("salida-candidata");
                        if (PiezasDeTropa.Miniatura(s.TropaId, 32) is VisualElement mini) candidata.Add(mini);
                        var textos = new VisualElement();
                        textos.Add(Dialogo.Texto(string.IsNullOrEmpty(s.Nombre) ? NombreDeTropa(s.TropaId) : s.Nombre, "aviso-titulo"));
                        textos.Add(Dialogo.Texto($"{s.Cantidad} hombres · {Coste(s)} de Liderazgo", "campo-ayuda"));
                        candidata.Add(textos);
                        elegir.Add(candidata);
                    }
                    tropaCaja.Add(elegir);
                }
                if (resides && disponibles.Count == 0) tropaCaja.Add(Dialogo.Texto(plaza != null ? "No tienes tropa libre en la plaza (la de guarnición no sale). Puedes salir solo." : "No tienes tropa en el campamento. Puedes salir solo.", "campo-ayuda"));
                if (!resides) tropaCaja.Add(Dialogo.Texto(lista.Count > 0 ? "Sales con la columna con la que entraste, tal cual." : "Tu columna no lleva escuadras.", "campo-ayuda"));
                if (libre < 0) Poner(error, $"Te pasas de Liderazgo ({usado} / {tope}): quita escuadras para poder salir.");
                else if (error.text.StartsWith("Te pasas de Liderazgo")) Poner(error, null);
                salir?.SetEnabled(libre >= 0);
            }

            salir = Dialogo.Boton(plaza != null && !resides ? "SALIR Y RETOMAR MI COLUMNA" : "SALIR", "boton-primario salida-salir", async () =>
            {
                salir.SetEnabled(false);
                Poner(error, null);
                var carga = new Cantidades();
                // El orden del riel es el de combate: si el héroe no las tiene así a la cabeza, se le guarda antes de salir (las que salen primero, en
                // el orden del riel, y el resto detrás).
                var yo = HeroeAhora();
                if (resides && yo != null && elegidas.Count > 0 && !yo.EscuadronesIds.Take(elegidas.Count).SequenceEqual(elegidas))
                {
                    var ordenar = new OrdenOrdenarEscuadras();
                    ordenar.EscuadronIds.AddRange(elegidas);
                    ResultadoOrden ro;
                    try { ro = await c.Sesion.Ordenar(new Orden { OrdenarEscuadras = ordenar }); }
                    catch (ErrorDeSesion e) { Poner(error, e.Message); salir.SetEnabled(true); return; }
                    if (ro.Resultado != Resultado.OrdenAceptada) { Poner(error, Textos.Explicar(ro.Rechazo)); salir.SetEnabled(true); return; }
                }
                foreach (var (campo, recurso) in campos) if (campo.value > 0) carga.PorId[recurso] = campo.value * 1000L;
                Orden orden;
                if (plaza == null)
                {
                    var o = new OrdenSalirDelCampamento { CampamentoId = camp.Id, HeroeId = h.Id, Carga = carga };
                    if (resides) o.EscuadronIds.AddRange(elegidas);
                    orden = new Orden { SalirDelCampamento = o };
                }
                else if (resides)
                {
                    var o = new OrdenSalirAlMundo { AsentamientoId = plaza.Id, HeroeId = h.Id, Carga = carga };
                    o.EscuadronIds.AddRange(elegidas);
                    orden = new Orden { SalirAlMundo = o };
                }
                else orden = new Orden { SalirDeAsentamiento = new OrdenSalirDeAsentamiento { AsentamientoId = plaza.Id, HeroeId = h.Id } };
                ResultadoOrden r;
                try { r = await c.Sesion.Ordenar(orden); }
                catch (ErrorDeSesion e) { Poner(error, e.Message); salir.SetEnabled(true); return; }
                if (r.Resultado != Resultado.OrdenAceptada) { Poner(error, Textos.Explicar(r.Rechazo)); salir.SetEnabled(true); return; }
                Cerrar();
                if (cerrarInterior != null) await cerrarInterior();
                if (this != null) Mapa();
            });
            salir.name = "salir";
            acciones.Add(salir);
            // En modo ejército, SALIR convoca (`convocarEjercito`) y la ventana pasa a su preparación.
            (List<string>, Cantidades) Seleccion()
            {
                var carga = new Cantidades();
                foreach (var (campo, recurso) in campos) if (campo.value > 0) carga.PorId[recurso] = campo.value * 1000L;
                return (new List<string>(elegidas), carga);
            }
            void Reabrir() { Cerrar(); VentanaDeSalida(campId, lugar, plazaId); }
            Button convocar = null;
            convocar = Dialogo.Boton("CONVOCAR EJÉRCITO", "boton-primario salida-salir salida-ejercito-boton", async () =>
            {
                var (ids, carga) = Seleccion();
                var o = new OrdenConvocarEjercito { HeroeId = h.Id, PoliticaDeUnion = politicaDeSalida, Carga = carga };
                o.EscuadronIds.AddRange(ids);
                convocar.SetEnabled(false);
                if (await OrdenConError(new Orden { ConvocarEjercito = o }, m => Poner(error, m))) Reabrir();
                else convocar.SetEnabled(true);
            });
            convocar.name = "convocar";
            acciones.Add(convocar);
            void ModoDeSalida()
            {
                var ejercito = comoEjercito?.value == true;
                Ver(bloqueEjercito, ejercito);
                Ver(salir, !ejercito);
                Ver(convocar, ejercito);
                if (ejercito) BloqueDeEjercito(bloqueEjercito, plaza?.Id ?? camp.Id, h, Seleccion, m => Poner(error, m), Reabrir);
            }
            if (comoEjercito != null)
            {
                comoEjercito.RegisterValueChangedCallback(ev => { salirComoEjercito = ev.newValue; ModoDeSalida(); });
                columnaPersonal.RegisterValueChangedCallback(ev => { if (ev.newValue) { salirComoEjercito = false; ModoDeSalida(); } });
            }
            ModoDeSalida();
            PintarTropa();
            capaEncima.Add(velo);
        }

        /// <summary>Huecos del riel de tropa a la vista (ESPACIOS del prototipo eran 5 en horizontal; las tarjetas verticales caben más) y su ancho.</summary>
        const int EspaciosDeTropa = 7;
        const float AnchoDeHueco = 110;

        /// <summary>Un título con su ⓘ: al pulsarlo, se abre o se cierra su explicación debajo (la `ayuda` del prototipo).</summary>
        static VisualElement ConAyuda(VisualElement titulo, string texto)
        {
            var fila = Dialogo.Caja("con-ayuda");
            fila.Add(titulo);
            var explicacion = Dialogo.Texto(texto, "ayuda-texto oculto");
            var boton = Dialogo.Boton("i", "ayuda-boton", () => explicacion.ToggleInClassList("oculto"));
            boton.tooltip = "Qué es esto";
            fila.Add(boton);
            var caja = new VisualElement();
            caja.Add(fila);
            caja.Add(explicacion);
            return caja;
        }

        /// <summary>La línea «Velocidad de la columna» (`htmlLineaVelocidad` del prototipo), con las mismas reglas que el motor (`velocidad_de_columna`):
        /// la escuadra más lenta con soldados (`TROPAS_RECLUTABLES.velocidad`); sin tropa, la del héroe solo (`MOVIMIENTO.velocidadJugador`). El color
        /// compara con la infantería ligera (la velocidad más repetida del catálogo): rojo más lenta, blanco igual, azul más rápida.</summary>
        VisualElement LineaDeVelocidad(List<string> tropaIds)
        {
            var catalogo = c.Datos.TropasReclutables?.OfType<Newtonsoft.Json.Linq.JObject>().ToList() ?? new List<Newtonsoft.Json.Linq.JObject>();
            double? Vel(Newtonsoft.Json.Linq.JObject t) => (double?)t?["velocidad"];
            var piezas = tropaIds.Select(id => catalogo.FirstOrDefault(t => (string)t["id"] == id)).Where(t => Vel(t) > 0).ToList();
            var lenta = piezas.OrderBy(t => Vel(t)).FirstOrDefault();
            var valor = lenta != null ? Vel(lenta).Value : c.Datos.Numero("documentos.Movimiento_Ejercitos.MOVIMIENTO.velocidadJugador");
            var velocidades = catalogo.Select(Vel).Where(v => v > 0).Select(v => v.Value).ToList();
            double? ligera = velocidades.Count == 0 ? (double?)null : velocidades.GroupBy(v => v).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).First().Key;
            string ritmo = ligera == null ? "ligera" : Math.Abs(valor - ligera.Value) < 1e-6 ? "ligera" : valor < ligera ? "lenta" : "rapida";
            // Las flechas encendidas: por debajo de la ligera 1 (lo más lento) o 2 (la tropa media), la ligera 3, por encima 4.
            int flechas = ritmo == "ligera" ? 3 : ritmo == "rapida" ? 4
                : velocidades.Where(v => v < ligera).Distinct().OrderByDescending(v => v).FirstOrDefault() is double media && valor >= media ? 2 : 1;
            var texto = ritmo == "lenta" ? "más lenta que la infantería ligera" : ritmo == "rapida" ? "más rápida que la infantería ligera" : "al paso de la infantería ligera";
            var linea = Dialogo.Caja("salida-velocidad ritmo-" + ritmo);
            linea.name = "velocidad";
            linea.Add(ConAyuda(Dialogo.Texto("VELOCIDAD DE LA COLUMNA", "salida-velocidad-etq"),
                "La columna va al paso de lo más lento que lleva: la escuadra más lenta (con soldados); sin tropa, al del héroe solo. La marcha forzada la acelera. El terreno y los caminos cambian luego el tiempo de cada tramo. Rojo: más lenta que la infantería ligera. Blanco: igual. Azul: más rápida."));
            var iconos = Dialogo.Caja("salida-velocidad-flechas");
            for (int i = 0; i < 4; i++) { var f = Dialogo.Texto("›", "salida-velocidad-flecha"); f.style.opacity = i < flechas ? 1 : 0.22f; iconos.Add(f); }
            linea.Add(iconos);
            linea.Add(Dialogo.Texto($"{valor:0.#}", "salida-velocidad-cifra"));
            linea.Add(Dialogo.Texto("u/min", "salida-velocidad-unidad"));
            linea.Add(Dialogo.Texto($"{texto}{(lenta != null ? $" · marca el paso {(string)lenta["nombre"]}" : " · vas solo")}", "salida-velocidad-texto"));
            return linea;
        }

        /// <summary>El género y las piezas que guardó el motor al crear el héroe («mujer · cabeza head_02 · pelo hair_03 …»; vacía, la de por defecto).</summary>
        static string Aspecto(Heroe h)
        {
            if (h == null) return "—";
            string P(string n, string id) => $"{n} {(string.IsNullOrEmpty(id) ? "por defecto" : id)}";
            var a = h.Avatar ?? new BronzeAge.Contract.V1.Avatar();
            return $"{(h.Femenino ? "mujer" : "hombre")} · {P("cabeza", a.CabezaId)} · {P("pelo", a.PeloId)} · {P("barba", a.BarbaId)} · {P("cejas", a.CejasId)}";
        }

        /// <summary>`heroeId`: el de la Bienvenida o, si nació después (en esta sesión), el del ámbito HEROE.</summary>
        static string LineaDelHeroe(Bienvenida b, Almacen a, string heroeId)
        {
            if (heroeId == null) return "Eres miembro, aún sin héroe.";
            var h = a.Buscar(TipoEntidad.Heroe, heroeId)?.Heroe;
            var faccionId = h != null && h.HasFaccionId ? h.FaccionId : b.HasFaccionId ? b.FaccionId : null;
            var faccion = faccionId == null ? "Sin Facción" : NombreDeFaccion(a, faccionId) ?? "Facción";
            return $"{Textos.NombreDeHeroe(a, heroeId)} — {faccion} · Nivel {h?.Nivel ?? 1}";
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
                    // `.elegir-tarjeta-linea`: «👥 gente» y la tierra.
                    boton.Add(IconosDelMundo.ConIcono("glifo", "gente", Dialogo.Texto($"{Gente(a.Gente)} · {Tierra(a)}", "aviso-cuando"), 14));
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
                Fichas(v.Q("campamento-datos"), campamento == null ? new (string, string, VisualElement)[0] : DatosDelCampamento(campamento, LugarDe));
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
                var mundo = EscenaDelMundo();
                string rechazoDeNombre = null;
                raiz.style.display = DisplayStyle.None;
                mapaCrear.gameObject.SetActive(false);
                if (camara != null) camara.gameObject.SetActive(false);
                // Y la de MundoScene, que si no tapa de negro la del creador.
                var volverCamaras = ApartarCamarasDelMundo();

                async Task Cerrar(bool nacido)
                {
                    if (SelectorDePersonaje.Actual == null) return;
                    SelectorDePersonaje.Actual = null;
                    cerrarEscenaEncima = null;
                    if (mundo.IsValid() && mundo.isLoaded) UnityEngine.SceneManagement.SceneManager.SetActiveScene(mundo);
                    var descarga = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(Creador);
                    while (descarga != null && !descarga.isDone) await Task.Yield();
                    if (this == null) return;
                    raiz.style.display = DisplayStyle.Flex;
                    if (camara != null) camara.gameObject.SetActive(true);
                    volverCamaras();
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
                cerrarEscenaEncima = () => Cerrar(false);
                var carga = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(Creador, UnityEngine.SceneManagement.LoadSceneMode.Additive);
                while (!carga.isDone) await Task.Yield();
                var escena = UnityEngine.SceneManagement.SceneManager.GetSceneByName(Creador);
                if (escena.IsValid() && escena.isLoaded) UnityEngine.SceneManagement.SceneManager.SetActiveScene(escena); // su luz y su cielo
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
        /// <summary>Cierra la escena de Conquest que esté encima (el creador o el selector de personaje) al irse a otra pantalla con ella delante:
        /// sesión perdida, reemplazada…</summary>
        Func<Task> cerrarEscenaEncima;

        static string Gente(int n) => n == 1 ? "1 persona" : $"{n} personas";

        static string Tierra(AnalisisDeCampamento a) =>
            string.Join(", ", a.Suelo.Where(kv => kv.Value >= 0.1).OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value * 100:0}%"));

        /// <summary>Lo que rodea al campamento, como la ficha de BronzeAgeClient: gente, tierra, fertilidad y bosque a 150 u, yacimientos (a 150 u,
        /// hasta 250 y el más cerca), río, agua, bandidos y el vecino.</summary>
        static (string, string, VisualElement)[] DatosDelCampamento(AnalisisDeCampamento a, Func<string, string> lugarDe)
        {
            var r = new List<(string, string, VisualElement)>
            {
                ("Gente", Gente(a.Gente), null),
                ("Tierra a 150 u", Tierra(a), null),
                // Como `htmlFicha`: «🌾 Fertilidad», «🌲 Bosque», «〰️ Río», «🌊 Costa o lago».
                ("Fertilidad", $"{a.FertilidadMedia * 100:0}%", IconosDelMundo.Icono("glifo", "viveres", 18)),
                ("Bosque", $"{a.FraccionDeBosque * 100:0}%", IconosDelMundo.Icono("glifo", "bosque", 18)),
            };
            // Los yacimientos con su icono del mapa en su color (`htmlNodos`: `svgIcono(ICONO_DE_NODO, colorNodo)` a 18 px).
            foreach (var kv in a.Nodos.OrderBy(x => x.Value.masCerca))
                r.Add(($"Yacimiento: {CosasDelMapa.NombreDeRecurso(kv.Key)}", $"{kv.Value.cerca} a 150 u · {kv.Value.frontera} a 250 u · el más cerca a {kv.Value.masCerca:0} u",
                    IconosDelMundo.Icono("mapa", IconosDelMundo.DeNodo(kv.Key), 18, IconosDelMundo.ColorDeNodo(kv.Key))));
            r.Add(("Río", a.Rio == null ? "ninguno" : $"a {a.Rio:0} u{(a.RioNavegable ? ", navegable" : "")}", IconosDelMundo.Icono("glifo", "rio", 18)));
            r.Add(("Costa o lago", a.Agua == null ? "a más de 250 u" : $"a {a.Agua:0} u", IconosDelMundo.Icono("glifo", "costa", 18)));
            r.Add(("Bandidos a 250 u", a.Bandidos.Count == 0 ? "ninguno" : $"{a.Bandidos.Count}; el más cerca a {a.Bandidos[0].distancia:0} u (nivel {a.Bandidos[0].nivel})",
                IconosDelMundo.Icono("mapa", "bandidos", 18, RojoBandidos)));
            if (a.Vecino != null) r.Add(("Campamento vecino", $"{lugarDe(a.Vecino)}, a {a.DistanciaAlVecino:0} u", null));
            return r.ToArray();
        }

        /// <summary>El rojo de los bandidos en «Nace tu héroe» (`#d9534f`).</summary>
        static readonly Color RojoBandidos = new Color32(0xd9, 0x53, 0x4f, 0xff);

        /// <summary>Un icono delante de un texto, en fila.</summary>
        static VisualElement IconoDelante(VisualElement icono, VisualElement texto)
        {
            var fila = Dialogo.Caja("con-icono");
            fila.Add(icono);
            fila.Add(texto);
            return fila;
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
            void Fila(string titulo, string ta, string tb, int gana = 0, VisualElement va = null, VisualElement vb = null)
            {
                var f = new VisualElement();
                f.AddToClassList("comparar-fila");
                f.Add(Dialogo.Texto(titulo, "comparar-titulo"));
                var la = va ?? Dialogo.Texto(ta, "comparar-valor");
                var lb = vb ?? Dialogo.Texto(tb, "comparar-valor");
                la.EnableInClassList("comparar-gana", gana == 1);
                lb.EnableInClassList("comparar-gana", gana == 2);
                f.Add(la);
                f.Add(lb);
                donde.Add(f);
            }
            // Los yacimientos cerca con su icono a 14 px y la cifra (`iconosCerca`), o «—».
            VisualElement IconosCerca(AnalisisDeCampamento k)
            {
                var caja = Dialogo.Caja("comparar-valor fila-envuelta");
                foreach (var n in k.Nodos.Where(n => n.Value.cerca > 0))
                {
                    var t = IconoDelante(IconosDelMundo.Icono("mapa", IconosDelMundo.DeNodo(n.Key), 14, IconosDelMundo.ColorDeNodo(n.Key)) ?? new VisualElement(),
                        Dialogo.Texto(n.Value.cerca > 20 ? "20+" : n.Value.cerca.ToString(), "comparar-nodo"));
                    t.tooltip = CosasDelMapa.NombreDeRecurso(n.Key);
                    caja.Add(t);
                }
                if (caja.childCount == 0) caja.Add(Dialogo.Texto("—", "comparar-nodo"));
                return caja;
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
            Fila("Yacimientos a 150 u", Cerca(a), Cerca(b), Gana(a.Nodos.Sum(n => n.Value.cerca), b.Nodos.Sum(n => n.Value.cerca)), IconosCerca(a), IconosCerca(b));
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
            v.Q<Button>("opciones").clicked += AbrirOpciones;
            Botones(v);
            BarraDelJugador(v);
            RielDelMapa(v);

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
                    // Cada fila con su señal redonda (`.eleccion-clic-icono`); aquí, el icono del mapa de esa cosa, si lo tiene.
                    var cab = Dialogo.Caja("lista-fila-cab");
                    var redondo = Dialogo.Caja("lista-fila-icono");
                    // El símbolo del prototipo para cada cosa (los emoji de Twemoji; los demás, dibujados aquí), en oro.
                    var deLista = IconosDelMundo.DeLista(cosa.Clase);
                    var icono = deLista == null ? null : IconosDelMundo.Icono(deLista.Value.familia, deLista.Value.id, 16);
                    if (icono != null) redondo.Add(icono);
                    cab.Add(redondo);
                    var textos = Dialogo.Caja("lista-fila-texto");
                    textos.Add(Dialogo.Texto(cosa.Nombre, "aviso-titulo"));
                    if (!string.IsNullOrEmpty(cosa.Detalle)) textos.Add(Dialogo.Texto(cosa.Detalle, "aviso-cuando"));
                    cab.Add(textos);
                    fila.Add(cab);
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
                PintarAvisos(botonAvisos);
                PintarVentanaDeEjercito(v);
                forzada.SetEnabled(mapa.Columna != null);
                forzada.text = mapa.Columna?.MarchaForzada == true ? "Marcha forzada: sí" : "Marcha forzada: no";
                nodos.text = mapa.NombresDeNodos ? "Nodos: sí" : "Nodos: no";
                fertilidad.text = mapa.Fertilidad ? "Fertilidad: sí" : "Fertilidad: no";
                // Dentro de un campamento de mercenarios (con su planta ya llegada): su interior. Los demás sitios, el aviso hasta su paso.
                var campamento = CampamentoDondeEsta();
                if (campamento != null) { Interior(campamento); return; }
                // Dentro de tu plaza (paso 7): su interior.
                var plaza = PlazaDondeEsta();
                if (plaza != null) { InteriorDePlaza(plaza); return; }
                // Dentro de un pueblo, una aldea o una avanzada (paso 9): su interior.
                if (PuebloDondeEsta() is Pueblo pueblo) { InteriorDePueblo(pueblo); return; }
                if (AvanzadaDondeEsta() is Avanzada avanzada) { InteriorDeAvanzada(avanzada); return; }
                var lugar = LugarDondeEsta();
                Ver(dentro, lugar != null);
                if (lugar != null) v.Q<Label>("dentro-texto").text = $"Estás dentro de {lugar}. Su interior llega con su paso; de momento, desde aquí sales al mundo.";
                PintarFicha(v);
                VigilarBatalla();
                AnotarInformesDelMapa();
            }
            Refrescar();
            RepintarCada(v, 250, Refrescar);
        }

        string accionesPintadas;

        /// <summary>La ficha: nombre, qué es, distancia y tiempo de marcha, y los botones de lo que se puede hacer (Marchar aquí y las acciones
        /// a la puerta). Los botones se rehacen solo si cambia lo que se puede hacer.</summary>
        void PintarFicha(VisualElement v)
        {
            var cosa = mapa.Seleccionada;
            PintarPuerta(v, cosa);
            if (cosa == null) return;
            v.Q<Label>("ficha-nombre").text = cosa.Nombre;
            v.Q<Label>("ficha-detalle").text = cosa.Detalle ?? "";
            var nivel = v.Q("ficha-nivel");
            var deBandidos = cosa.Clase == ClaseDeCosa.Bandidos && int.TryParse(cosa.Detalle?.Replace("Nivel ", ""), out var n) ? n : 0;
            Ver(nivel, deBandidos > 0);
            if (deBandidos > 0 && !Equals(nivel.userData, deBandidos))
            {
                nivel.userData = deBandidos;
                nivel.Clear();
                for (int i = 1; i <= 3; i++) nivel.Add(Dialogo.Caja(i <= deBandidos ? "bandidos-nivel-punto bandidos-nivel-punto--on" : "bandidos-nivel-punto"));
                nivel.Add(Dialogo.Texto($"Nivel {deBandidos} de 3", "campo-ayuda"));
            }
            var hasta = mapa.HastaAlli(cosa);
            Fichas(v.Q("ficha-datos"), hasta == null ? new[] { ("Distancia", "sin columna en el mapa") }
                : new[] { ("Distancia", $"{hasta.Value.distancia:0.#} u"), ("Tiempo de marcha", hasta.Value.tiempo) });
            // Un Aeda itinerante: qué hace y, parado en una plaza tuya, lo que os vende (paso 13).
            if (cosa.Clase == ClaseDeCosa.Aeda) FichaDeAeda(v.Q("ficha-datos"), cosa);
            var acciones = CosasDelMapa.Acciones(cosa, mapa.Contexto());
            bool marchar = mapa.Columna != null && !mapa.Columna.EnBatalla;
            var ventanas = VentanasDePuerta(cosa);
            var firma = cosa.Clase + cosa.Id + marchar + string.Join("|", acciones.Select(a => a.Texto)) + "|" + string.Join("|", ventanas.Select(x => x.id));
            if (firma == accionesPintadas) return;
            accionesPintadas = firma;
            var caja = v.Q("ficha-acciones");
            caja.Clear();
            if (marchar)
                caja.Add(IconosDelMundo.BotonConGlifo("Marchar aquí", IconosDelMundo.DeAccion("Marchar aquí"), "boton-secundario", () =>
                    _ = cosa.Clase == ClaseDeCosa.Plaza ? mapa.MarcharAPlaza(cosa.Id) : mapa.MarcharA(cosa.X, cosa.Y)));
            foreach (var a in acciones)
            {
                var accion = a;
                // Con su glifo delante, como `.puerta-boton-icono` (🚪 entrar, ⚔ atacar, 👁 inspeccionar, 🎁 abrir).
                caja.Add(IconosDelMundo.BotonConGlifo(accion.Texto, IconosDelMundo.DeAccion(accion.Texto), "boton-secundario", () => _ = mapa.Ordenar(accion.Orden.Clone())));
            }
            // Las ventanas de la puerta (obra, guarnición, carga), detrás de las acciones.
            foreach (var x in ventanas) caja.Add(BotonDeVentana(v, cosa, x));
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
            VigilarFinDeBatalla();
            // Las Opciones, encima de todo, se cierran con Esc antes que lo de debajo.
            if (OpcionesAbiertas) { if (UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true) CerrarOpciones(); return; }
            // El mapa 2D, encima de la pantalla, se cierra con Esc antes que lo de debajo.
            if (Mapa2DAbierto && UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true) { CerrarMapa2D(); return; }
            TeclaDeLaBase();
            TeclaDeFaccion();
            TeclaDeMisiones();
            PintarRastreador();
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
                    ? new[] { ("Héroe", LineaDelHeroe(b, s.Almacen, c.HeroeId)), ("Id", c.HeroeId), ("Aspecto", Aspecto(s.Almacen.Buscar(TipoEntidad.Heroe, c.HeroeId)?.Heroe)) }
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
            RepintarCada(v, 500, Refrescar);
        }

        static void Fichas(VisualElement donde, params (string etiqueta, string valor)[] fichas) =>
            Fichas(donde, fichas.Select(x => (x.etiqueta, x.valor, (VisualElement)null)).ToArray());

        /// <summary>Fichas con un icono delante de la etiqueta (las filas del prototipo que lo llevan: yacimientos, bandidos…).</summary>
        static void Fichas(VisualElement donde, (string etiqueta, string valor, VisualElement icono)[] fichas)
        {
            donde.Clear();
            foreach (var (e, valor, icono) in fichas)
            {
                var f = Dialogo.Caja("ficha");
                var etiqueta = Dialogo.Texto(e, "ficha-etiqueta");
                f.Add(icono == null ? etiqueta : IconoDelante(icono, etiqueta));
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
