// El artefacto de mundo y el paquete de relieve (contrato 06 §4 y §8), bajados por hash y guardados en disco para no bajarlos otra vez
// (puerto de `BronzeAgeClient/src/mundo/artefacto.ts`, `relieve.ts` y `cacheDeUno.ts`). Cada caché guarda solo el último: un mundo nuevo
// sustituye al viejo.
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BronzeAge.Contract.V1;
using Google.Protobuf;

namespace ConquestTactics.Mundo
{
    /// <summary>Una carpeta que guarda un solo fichero por clave (`<hash>.bin`); guardar otro borra el anterior.</summary>
    public sealed class CacheDeUno
    {
        readonly string carpeta;
        public CacheDeUno(string carpeta) { this.carpeta = carpeta; }

        string Ruta(string clave) => Path.Combine(carpeta, clave + ".bin");

        public byte[] Leer(string clave) => File.Exists(Ruta(clave)) ? File.ReadAllBytes(Ruta(clave)) : null;

        /// <summary>La clave de lo que hay guardado (para `Hola.mundo_hash_conocido`), o null.</summary>
        public string Guardada()
        {
            if (!Directory.Exists(carpeta)) return null;
            foreach (var f in Directory.GetFiles(carpeta, "*.bin")) return Path.GetFileNameWithoutExtension(f);
            return null;
        }

        public void Guardar(string clave, byte[] datos)
        {
            Directory.CreateDirectory(carpeta);
            foreach (var f in Directory.GetFiles(carpeta, "*.bin")) File.Delete(f);
            var tmp = Ruta(clave) + ".tmp";
            File.WriteAllBytes(tmp, datos);
            File.Move(tmp, Ruta(clave)); // a medio escribir no queda como bueno
        }
    }

    public static class MundoEnDisco
    {
        /// <summary>El artefacto vigente: de la caché si coincide el hash; si no, lo baja de `url_artefacto` y lo guarda.</summary>
        public static async Task<ArtefactoDeMundo> CargarArtefacto(Acceso acceso, InfoMundo mundo, CacheDeUno cache)
        {
            var datos = cache.Leer(mundo.Hash);
            if (datos == null)
            {
                datos = await acceso.Bajar(mundo.UrlArtefacto).ConfigureAwait(false);
                cache.Guardar(mundo.Hash, datos);
            }
            return ArtefactoDeMundo.Parser.ParseFrom(datos);
        }

        /// <summary>El relieve fino; null si el servidor no lo anuncia. Lo que se baja se comprueba antes de guardarlo.</summary>
        public static async Task<PaqueteRelieve> CargarRelieve(Acceso acceso, InfoMundo mundo, CacheDeUno cache)
        {
            if (string.IsNullOrEmpty(mundo.UrlRelieve) || string.IsNullOrEmpty(mundo.HashRelieve)) return null;
            var guardado = cache.Leer(mundo.HashRelieve);
            if (guardado != null) return PaqueteRelieve.Parser.ParseFrom(guardado);
            var datos = await acceso.Bajar(mundo.UrlRelieve).ConfigureAwait(false);
            var p = PaqueteRelieve.Parser.ParseFrom(datos);
            if (!Verificar(p, mundo.HashRelieve)) throw new InvalidDataException("el relieve que se bajó no es el que anunció el servidor");
            cache.Guardar(mundo.HashRelieve, datos);
            return p;
        }

        /// <summary>¿Es el paquete el anunciado y está entero? Su hash es el sha256 de su serialización con `hash = ""`.</summary>
        public static bool Verificar(PaqueteRelieve p, string hashAnunciado)
        {
            if (p.Hash != hashAnunciado) return false;
            var sinHash = p.Clone();
            sinHash.Hash = "";
            using (var sha = SHA256.Create())
            {
                var h = sha.ComputeHash(sinHash.ToByteArray());
                var s = new StringBuilder(64);
                foreach (var b in h) s.Append(b.ToString("x2"));
                return s.ToString() == p.Hash;
            }
        }
    }
}
