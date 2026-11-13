// El puente entre Unity y la conexión con el motor (`Conquest.Mundo`): guarda el token, abre y cierra la sesión, la bombea cada fotograma
// desde el hilo principal, baja lo que el mundo necesita (reglas, artefacto y relieve, con caché en disco) y lleva los avisos del héroe.
// Lo usan las pantallas de fuera de la batalla; no pinta nada.
using System;
using System.IO;
using System.Threading.Tasks;
using BronzeAge.Contract.V1;
using UnityEngine;

namespace ConquestTactics.Mundo.UI
{
    public sealed class ConexionDeMundo : MonoBehaviour
    {
        public const string VersionCliente = "conquest";
        const string ClaveToken = "mundo.token", ClaveNick = "mundo.nick", ClaveServidor = "mundo.servidor";

        [Tooltip("El servidor del motor (HTTP y WebSocket en el mismo puerto). Se puede cambiar al arrancar con -servidor <url>.")]
        [SerializeField] string servidor = "http://127.0.0.1:7878";

        public Acceso Acceso { get; private set; }
        public Sesion Sesion { get; private set; }
        public DatosDeJuego Datos { get; private set; }
        public ArtefactoDeMundo Artefacto { get; private set; }
        public PaqueteRelieve Relieve { get; private set; }
        public readonly Avisos Avisos = new Avisos();

        public string Nick { get; private set; }
        public string Servidor => Acceso.Base;
        public bool HayToken => !string.IsNullOrEmpty(token);
        /// <summary>El héroe de la cuenta: el de la `Bienvenida` o, si nació después, el de su ámbito `HEROE`.</summary>
        public string HeroeId
        {
            get
            {
                if (Sesion?.Bienvenida == null) return null;
                if (Sesion.Bienvenida.HasHeroeId) return Sesion.Bienvenida.HeroeId;
                foreach (var k in Sesion.Almacen.Ambitos.Keys) if (k.StartsWith("HEROE:", StringComparison.Ordinal)) return k.Substring("HEROE:".Length);
                return null;
            }
        }

        string token;
        string carpeta;
        string archivoAvisos;

        void Awake()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-servidor") servidor = args[i + 1];
            Acceso = new Acceso(servidor);
            carpeta = Path.Combine(Application.persistentDataPath, "mundo");
            // El token solo vale para el servidor que lo dio.
            if (PlayerPrefs.GetString(ClaveServidor) == Acceso.Base)
            {
                token = PlayerPrefs.GetString(ClaveToken, null);
                Nick = PlayerPrefs.GetString(ClaveNick, null);
            }
            Avisos.AlCambiar += GuardarAvisos;
        }

        void Update() => Sesion?.Bombear();

        void OnDestroy() => Sesion?.Cerrar();

        public Task Registrar(string nick, string clave, string codigo) => Acceso.Registrar(nick, clave, codigo);

        public async Task Entrar(string nick, string clave)
        {
            var s = await Acceso.Entrar(nick, clave);
            token = s.Token;
            Nick = s.Nick;
            PlayerPrefs.SetString(ClaveToken, token);
            PlayerPrefs.SetString(ClaveNick, Nick);
            PlayerPrefs.SetString(ClaveServidor, Acceso.Base);
            PlayerPrefs.Save();
        }

        /// <summary>Abre la sesión con el token guardado (si ya hay una abierta, la devuelve). Un token caducado borra lo guardado.</summary>
        public async Task<Bienvenida> AbrirSesion()
        {
            if (Sesion != null && Sesion.Estado != EstadoSesion.Cerrada && Sesion.Bienvenida != null) return Sesion.Bienvenida;
            Sesion?.Cerrar();
            var s = new Sesion(Acceso.UrlSesion, token, VersionCliente) { MundoHash = new CacheDeUno(Path.Combine(carpeta, "artefacto")).Guardada() };
            s.AlBienvenida += b => CargarAvisos(b.HasHeroeId ? b.HeroeId : null);
            s.AlEvento += (_, e) => { if (HeroeId != null) Avisos.Procesar(e, HeroeId, s.Almacen, Datos); };
            Sesion = s;
            try { return await s.Abrir(); }
            catch (ErrorDeSesion e) when (e.Codigo == "token_invalido")
            {
                OlvidarToken();
                throw;
            }
        }

        /// <summary>Las reglas, el artefacto de mundo y el relieve de la `Bienvenida`, de la caché o del servidor. `progreso` va de 0 a 1.</summary>
        public async Task CargarMundo(Action<float, string> progreso)
        {
            var mundo = Sesion.Bienvenida.Mundo;
            progreso(0.1f, "Leyendo las reglas");
            await CargarDatos();
            progreso(0.3f, "Desplegando el mapa del mundo");
            Artefacto = await MundoEnDisco.CargarArtefacto(Acceso, mundo, new CacheDeUno(Path.Combine(carpeta, "artefacto")));
            progreso(0.7f, "Alzando el relieve");
            Relieve = await MundoEnDisco.CargarRelieve(Acceso, mundo, new CacheDeUno(Path.Combine(carpeta, "relieve")));
            progreso(1f, "Listo");
        }

        /// <summary>Las reglas de `/v1/datos` del mundo de la sesión (de la caché si no han cambiado). El selector de personaje las necesita
        /// antes de cargar el resto del mundo.</summary>
        public async Task<DatosDeJuego> CargarDatos() =>
            Datos = await DatosDeJuego.Cargar(Acceso, Sesion.Bienvenida.Mundo, new CacheDeUno(Path.Combine(carpeta, "datos")));

        /// <summary>«Cerrar sesión»: saca al héroe del mundo (`cerrarSesion`; si no contesta en unos segundos, se cierra igual y el servidor lo
        /// trata como una caída), cierra el socket y anula el token.</summary>
        public async Task CerrarSesion()
        {
            await SalirDelMundo();
            Sesion?.Cerrar();
            Sesion = null;
            if (HayToken) Acceso.Anular(token);
            OlvidarToken();
        }

        /// <summary>«Cambiar de partida»: saca al héroe y cierra el socket, pero el token sigue valiendo.</summary>
        public async Task DejarPartida()
        {
            await SalirDelMundo();
            Sesion?.Cerrar();
            Sesion = null;
        }

        async Task SalirDelMundo()
        {
            var s = Sesion;
            if (s == null || HeroeId == null || s.Estado != EstadoSesion.Abierta) return;
            var orden = s.Ordenar(new Orden { CerrarSesion = new OrdenCerrarSesion { HeroeId = HeroeId } });
            await Task.WhenAny(orden, Task.Delay(4000));
        }

        void OlvidarToken()
        {
            token = null;
            PlayerPrefs.DeleteKey(ClaveToken);
            PlayerPrefs.Save();
        }

        void CargarAvisos(string heroeId)
        {
            archivoAvisos = heroeId == null ? null : Path.Combine(carpeta, $"avisos-{System.Text.RegularExpressions.Regex.Replace(Acceso.Base, "[^A-Za-z0-9]", "_")}-{heroeId}.json");
            var json = archivoAvisos != null && File.Exists(archivoAvisos) ? File.ReadAllText(archivoAvisos) : null;
            Avisos.DesdeJson(json);
        }

        void GuardarAvisos()
        {
            if (archivoAvisos == null) return;
            Directory.CreateDirectory(carpeta);
            File.WriteAllText(archivoAvisos, Avisos.AJson());
        }
    }
}
