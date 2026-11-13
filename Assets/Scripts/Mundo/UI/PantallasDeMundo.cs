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
            pantalla.Clear();
            var v = Resources.Load<VisualTreeAsset>("MundoUI/" + uxml).Instantiate();
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
            SesionViva();
            await carga.Cerrar();
        }

        // ─────────────────────────── la sesión viva (provisional) ───────────────────────────

        /// <summary>Hasta que esté el mapa (paso 2): el reloj del mundo, el héroe, la conexión, los ámbitos suscritos y cuántas entidades hay de
        /// cada tipo.</summary>
        void SesionViva()
        {
            var v = Montar("Sesion");
            var botonAvisos = v.Q<Button>("avisos");
            botonAvisos.clicked += panelAvisos.Alternar;
            v.Q<Button>("cambiar-partida").clicked += async () => { await c.DejarPartida(); _ = Partidas(); };
            v.Q<Button>("cerrar-sesion").clicked += async () =>
            {
                if (await dialogo.Abrir("Tu héroe sale del mundo y se cierra la sesión en este equipo.", "¿Cerrar sesión?", "Cerrar sesión")) _ = CerrarSesion();
            };
            var reloj = v.Q<Label>("reloj");
            VisualElement fr = v.Q("fichas-reloj"), fh = v.Q("fichas-heroe"), fc = v.Q("fichas-conexion");
            var ambitos = v.Q<ScrollView>("ambitos");
            var entidades = v.Q<ScrollView>("entidades");

            void Refrescar()
            {
                var s = c.Sesion;
                if (s == null || pantalla.Q("ambitos") != ambitos) return;
                var b = s.Bienvenida;
                reloj.text = Instante.AFecha(s.AhoraMundo).ToString("dd/MM/yyyy  HH:mm:ss");
                Fichas(fr, ("Ritmo", s.RelojPausado ? "pausado" : $"×{s.FactorReloj:0.##}"));
                Fichas(fh, b == null ? new (string, string)[0] : b.HasHeroeId
                    ? new[] { ("Héroe", LineaDelHeroe(b, s.Almacen)), ("Id", b.HeroeId) }
                    : new[] { ("Héroe", "aún sin héroe"), ("Campamentos donde nacer", b.CampamentosParaElegir.Count.ToString()) });
                Fichas(fc, ("Estado", s.Estado.ToString()), ("Servidor", c.Servidor), ("Ida y vuelta", double.IsInfinity(s.Rtt) ? "—" : $"{s.Rtt:0} ms"),
                    ("Tramas", $"{s.TramasRecibidas} ({s.TramasZstd} en zstd)"), ("Mundo", b?.Mundo.Hash.Substring(0, 12) ?? "—"),
                    ("Reglas", c.Datos?.Huella?.Substring(0, 12) ?? "—"), ("Relieve", c.Relieve != null ? $"{c.Relieve.Columnas}×{c.Relieve.Filas} muestras" : "sin relieve"));
                Fichas(ambitos, s.Almacen.Ambitos.OrderBy(x => x.Key).Select(x => (x.Key, $"v{x.Value.Version} · {x.Value.Entidades.Count} entidades{(x.Value.Niebla != null ? " · niebla" : "")}")).ToArray());
                var porTipo = s.Almacen.Ambitos.Values.SelectMany(x => x.Entidades).GroupBy(x => x.Key).Select(g => g.First().Value.CuerpoCase)
                    .GroupBy(x => x).OrderByDescending(g => g.Count()).Select(g => (g.Key.ToString(), g.Count().ToString())).ToArray();
                Fichas(entidades, porTipo.Length > 0 ? porTipo : new[] { ("—", "nada aún") });
                botonAvisos.text = c.Avisos.NoLeidos > 0 ? $"Avisos ({c.Avisos.NoLeidos})" : "Avisos";
            }
            // Con héroe, la VISTA del mundo entero, como BronzeAgeClient: el servidor ya la filtra por la niebla. La sesión la vuelve a pedir al reconectar.
            if (c.HeroeId != null && c.Artefacto?.Manifiesto?.Tamano != null)
                c.Sesion.CambiarVista(new Rectangulo { Min = new Punto(), Max = c.Artefacto.Manifiesto.Tamano.Clone() });
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
