using System;
using System.Collections.Generic;
using UnityEngine;

namespace ConquestTactics.Net
{
    /// <summary>
    /// Los catálogos del héroe del motor, importados tal cual (<c>HeroeDelMotor.Importar</c>, Resources/Heroe/BronzeAge): las 5 clases
    /// (<c>CLASES_HEROE</c>) y los objetos (<c>OBJETOS</c>), cada uno con su versión, la que trae el ticket en <c>Reglas</c>. Las cifras de una
    /// pieza de tienda o de botín salen de aquí (en el ticket solo viajan las de una forjada).
    /// </summary>
    public static class CatalogoDelHeroe
    {
        [Serializable] public class Atributos { public int fuerza, destreza, armadura, vitalidad; }
        [Serializable] public class PorTipo { public int perforante, cortante, contundente; }
        [Serializable]
        public class Combate
        {
            public Atributos atributos = new Atributos();
            public PorTipo dano = new PorTipo(), penetracion = new PorTipo(), defensa = new PorTipo();
            public int salud;
            public int vida, aguante;
            public float trote, galope;
        }
        [Serializable]
        public class Objeto
        {
            public string id, nombre, tipo, hueco, clase, tipoArmadura, metal;
            public int tier, precio;
            public Combate combate = new Combate();
        }
        [Serializable]
        public class Clase
        {
            public string id, nombre, armaduraInicial;
            public string[] armaduras = new string[0];
            public Atributos atributosBase = new Atributos();
        }
        [Serializable] class Clases { public string version; public Clase[] clases = new Clase[0]; }
        [Serializable] class Objetos { public string version; public Objeto[] objetos = new Objeto[0]; }

        static Clases _clases;
        static Objetos _objetos;
        static Dictionary<string, Objeto> _porId;
        static Dictionary<string, Clase> _clasePorId;

        static Clases DeClases => _clases ??= JsonUtility.FromJson<Clases>(Resources.Load<TextAsset>("Heroe/BronzeAge/clases")?.text ?? "{}");
        static Objetos DeObjetos => _objetos ??= JsonUtility.FromJson<Objetos>(Resources.Load<TextAsset>("Heroe/BronzeAge/objetos")?.text ?? "{}");

        public static string VersionDeClases => DeClases.version ?? "";
        public static string VersionDeObjetos => DeObjetos.version ?? "";
        public static IReadOnlyList<Clase> TodasLasClases => DeClases.clases;
        public static IReadOnlyList<Objeto> TodosLosObjetos => DeObjetos.objetos;

        /// <summary>La clase del motor por su id (la del arma: <c>HeroSnapshot.class_definition_id</c>), o null si no está.</summary>
        public static Clase ClaseDe(string id)
        {
            if (_clasePorId == null) { _clasePorId = new Dictionary<string, Clase>(); foreach (var c in DeClases.clases) _clasePorId[c.id] = c; }
            return id != null && _clasePorId.TryGetValue(id, out var r) ? r : null;
        }

        /// <summary>La ficha de un objeto del motor por su id (<c>ItemInstancia.item_definition_id</c>), o null si no está.</summary>
        public static Objeto ObjetoDe(string id)
        {
            if (_porId == null) { _porId = new Dictionary<string, Objeto>(); foreach (var o in DeObjetos.objetos) _porId[o.id] = o; }
            return id != null && _porId.TryGetValue(id, out var r) ? r : null;
        }
    }
}
