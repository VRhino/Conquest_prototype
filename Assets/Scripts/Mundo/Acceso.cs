// El servicio de acceso del motor (`Docs/Coordinacion/08_Consumo_del_Contrato.md` del motor; puerto de `BronzeAgeClient/src/contrato/acceso.ts`):
// registro y login por HTTP en el mismo puerto que la sesión, el cierre del token y las descargas por ruta relativa al servidor (artefacto de
// mundo, relieve, datos de juego). El token del login viaja en `Hola.token` al abrir el WebSocket.
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ConquestTactics.Mundo
{
    public sealed class Cuenta
    {
        public string CuentaId, Nick;
        public bool Bot;
    }

    public sealed class SesionDeAcceso
    {
        public string Token, CuentaId, Nick;
        /// <summary>Cuándo caduca, en ms de época Unix.</summary>
        public long ExpiraMs;
    }

    /// <summary>Un error del servicio de acceso, con su código estable (`nick_repetido`, `credenciales_invalidas`, `ocupado`...).
    /// Status 0: no se pudo hablar con el servidor.</summary>
    public sealed class ErrorDeAcceso : Exception
    {
        public readonly int Status;
        public readonly string Codigo;
        public ErrorDeAcceso(int status, string codigo, string mensaje) : base(mensaje) { Status = status; Codigo = codigo; }
    }

    public sealed class Acceso
    {
        static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };

        /// <summary>`http://host:puerto` del servidor, sin barra final.</summary>
        public readonly string Base;

        public Acceso(string baseUrl) { Base = baseUrl.TrimEnd('/'); }

        /// <summary>El WebSocket de la sesión: el mismo puerto, `ws://host:puerto/`.</summary>
        public string UrlSesion => (Base.StartsWith("https", StringComparison.OrdinalIgnoreCase) ? "wss" + Base.Substring(5) : "ws" + Base.Substring(4)) + "/";

        /// <summary>`POST /v1/registro`. El código es el de invitación (vacío si el servidor tiene el registro abierto).</summary>
        public async Task<Cuenta> Registrar(string nick, string clave, string codigo = "")
        {
            var r = await Pedir("/v1/registro", new JObject { ["nick"] = nick, ["clave"] = clave, ["codigo"] = codigo ?? "" }).ConfigureAwait(false);
            return new Cuenta { CuentaId = (string)r["cuenta_id"], Nick = (string)r["nick"], Bot = (bool?)r["bot"] ?? false };
        }

        /// <summary>`POST /v1/sesiones`: cada login da un token nuevo.</summary>
        public async Task<SesionDeAcceso> Entrar(string nick, string clave)
        {
            var r = await Pedir("/v1/sesiones", new JObject { ["nick"] = nick, ["clave"] = clave }).ConfigureAwait(false);
            return new SesionDeAcceso { Token = (string)r["token"], CuentaId = (string)r["cuenta_id"], Nick = (string)r["nick"], ExpiraMs = (long?)r["expira_ms"] ?? 0 };
        }

        /// <summary>`DELETE /v1/sesiones/actual`: anula el token al cerrar sesión. Sin esperar ni quejarse: si no llega, el token caduca solo.</summary>
        public void Anular(string token)
        {
            var p = new HttpRequestMessage(HttpMethod.Delete, Base + "/v1/sesiones/actual");
            p.Headers.TryAddWithoutValidation("Authorization", "sesion " + token);
            _ = http.SendAsync(p).ContinueWith(t => { _ = t.Exception; });
        }

        /// <summary>`GET` de una ruta del servidor (las de `InfoMundo` son relativas a él).</summary>
        public async Task<byte[]> Bajar(string ruta)
        {
            HttpResponseMessage r;
            try { r = await http.GetAsync(Absoluta(ruta)).ConfigureAwait(false); }
            catch (Exception e) when (e is HttpRequestException || e is TaskCanceledException) { throw SinServidor(e); }
            if (!r.IsSuccessStatusCode) throw new ErrorDeAcceso((int)r.StatusCode, "descarga", $"no se pudo bajar {ruta} ({(int)r.StatusCode})");
            return await r.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        }

        string Absoluta(string ruta) => ruta.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? ruta : Base + (ruta.StartsWith("/") ? ruta : "/" + ruta);

        /// <summary>Un `503 ocupado` (todos los cálculos de clave del servidor ocupados) se reintenta solo a los 2 y 4 s; si sigue, sale el error.</summary>
        async Task<JObject> Pedir(string ruta, JObject cuerpo)
        {
            foreach (var espera in new[] { 2000, 4000, -1 })
            {
                HttpResponseMessage r;
                try { r = await http.PostAsync(Base + ruta, new StringContent(cuerpo.ToString(), Encoding.UTF8, "application/json")).ConfigureAwait(false); }
                catch (Exception e) when (e is HttpRequestException || e is TaskCanceledException) { throw SinServidor(e); }
                var texto = await r.Content.ReadAsStringAsync().ConfigureAwait(false);
                JObject datos;
                try { datos = string.IsNullOrEmpty(texto) ? new JObject() : JObject.Parse(texto); }
                catch (Newtonsoft.Json.JsonException) { datos = new JObject(); }
                var codigo = (string)datos["error"] ?? "desconocido";
                if (r.StatusCode == HttpStatusCode.ServiceUnavailable && codigo == "ocupado" && espera > 0)
                {
                    await Task.Delay(espera).ConfigureAwait(false);
                    continue;
                }
                if (!r.IsSuccessStatusCode) throw new ErrorDeAcceso((int)r.StatusCode, codigo, (string)datos["mensaje"] ?? r.ReasonPhrase);
                return datos;
            }
            throw new InvalidOperationException("inalcanzable");
        }

        ErrorDeAcceso SinServidor(Exception e) => new ErrorDeAcceso(0, "sin_servidor", $"No se pudo contactar con el servidor en {Base}: {e.Message}");
    }
}
