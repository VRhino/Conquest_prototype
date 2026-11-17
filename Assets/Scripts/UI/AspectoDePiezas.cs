using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// El aspecto de cada pieza del catálogo del motor (<c>OBJETOS</c>), por su id: la parte del personaje modular que se pone
/// (<c>visualPartId</c>) y su icono. Es dato de Conquest, no del motor: <c>Resources/Heroe/aspecto.json</c>, que hoy da a todas las piezas
/// de un hueco y un peso la misma parte (y a las armas, la de su clase) y se puede afinar pieza a pieza cuando haya arte.
/// Un id que no está aquí es el de una pieza vieja de Conquest (el inventario y la tienda del campamento): sale de <see cref="ItemService"/>.
/// </summary>
public static class AspectoDePiezas
{
    [Serializable] class Pieza { public string id, parte, icono; }
    [Serializable] class Tabla { public Pieza[] piezas = new Pieza[0]; }

    static Dictionary<string, Pieza> _porId;

    static Dictionary<string, Pieza> PorId
    {
        get
        {
            if (_porId != null) return _porId;
            _porId = new Dictionary<string, Pieza>();
            var tabla = JsonUtility.FromJson<Tabla>(Resources.Load<TextAsset>("Heroe/aspecto")?.text ?? "{}");
            foreach (var p in tabla.piezas) _porId[p.id] = p;
            return _porId;
        }
    }

    /// <summary>¿Es una pieza del catálogo del motor con aspecto?</summary>
    public static bool DelMotor(string id) => id != null && PorId.ContainsKey(id);

    /// <summary>La parte del personaje de una pieza; null si no hay pieza o no se conoce.</summary>
    public static string Parte(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return PorId.TryGetValue(id, out var p) ? p.parte : ItemService.GetItemById(id)?.visualPartId;
    }

    /// <summary>El icono de una pieza; null si no lo tiene.</summary>
    public static Sprite Icono(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return PorId.TryGetValue(id, out var p) ? Resources.Load<Sprite>(p.icono) : ItemService.GetItemById(id)?.icon;
    }
}
