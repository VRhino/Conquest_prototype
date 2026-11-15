// Las opciones del juego (Víctor, 2026-11-15): un único servicio que guarda en PlayerPrefs y que leen los sistemas, y la lista declarativa de
// secciones y filas que pinta la ventana «Opciones» (`PantallasDeMundo.Opciones`). Una opción nueva es una fila más aquí: la ventana no cambia.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class OpcionesDelJuego
{
    /// <summary>Una opción de sí o no: su clave de PlayerPrefs, su texto, su explicación y su valor por defecto.</summary>
    public sealed class Opcion
    {
        public string Clave, Texto, Ayuda;
        public bool PorDefecto;
    }

    public sealed class Seccion
    {
        public string Titulo;
        public List<Opcion> Opciones = new List<Opcion>();
    }

    public const string EstandarteDeTropas = "batalla.estandarteTropas";

    /// <summary>Las secciones de la ventana, en su orden.</summary>
    public static readonly List<Seccion> Secciones = new List<Seccion>
    {
        new Seccion
        {
            Titulo = "Batalla",
            Opciones =
            {
                new Opcion
                {
                    Clave = EstandarteDeTropas, Texto = "Estandarte de las tropas", PorDefecto = true,
                    Ayuda = "Ver en la batalla el estandarte de cada escuadra. El de los héroes y las banderas de las zonas se ven siempre.",
                },
            },
        },
    };

    /// <summary>Se avisa con la clave de la opción que cambia.</summary>
    public static event Action<string> AlCambiar;

    static Opcion Buscar(string clave) => Secciones.SelectMany(s => s.Opciones).FirstOrDefault(o => o.Clave == clave);

    public static bool Valor(string clave) => PlayerPrefs.GetInt(clave, Buscar(clave)?.PorDefecto == true ? 1 : 0) == 1;

    public static void Poner(string clave, bool valor)
    {
        PlayerPrefs.SetInt(clave, valor ? 1 : 0);
        PlayerPrefs.Save();
        AlCambiar?.Invoke(clave);
    }
}
