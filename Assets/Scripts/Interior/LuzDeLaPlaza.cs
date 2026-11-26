using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// La luz de la ciudad por dentro, según la hora del mundo: el sol (su altura, su rumbo, su color y su fuerza), la luz de ambiente (cielo,
/// horizonte y suelo), el cielo, la bruma y el tono del postproceso. El día pasa por el amanecer, la mañana, el mediodía, la tarde y el ocaso;
/// la noche no es oscura (Víctor, 2026-11-26: «como un atardecer azulado, para que se siga jugando cómodo»): luz de luna alta y azul, ambiente
/// azulado y los fuegos, que entonces es cuando lucen.
///
/// La hora sale del reloj del mundo (`Sesion.AhoraMundo`, que llega por <see cref="Reloj"/>): un día dura <see cref="MinutosPorDia"/> minutos
/// del mundo. Mientras está puesta, apaga las demás luces direccionales de la escena y guarda lo que cambia de `RenderSettings`; al quitarse,
/// lo devuelve.
/// </summary>
public sealed class LuzDeLaPlaza : MonoBehaviour
{
    /// <summary>Lo que dura un día entero, en minutos del reloj del mundo. Un valor de partida: se afina jugando.</summary>
    public const double MinutosPorDia = 40;
    /// <summary>Para verla quieta a una hora (el banco de pruebas, las capturas): de 0 a 24; negativa, corre con el reloj.</summary>
    public static float HoraFija = -1;

    public Func<double> Reloj;

    struct Clave
    {
        public float Hora, Fuerza, Cabeceo, Guinada, Ambiente, Exposicion, Noche, Ocaso, Bruma;
        public Color Sol, Cielo, Horizonte, Suelo, Niebla, Tinte;
    }

    static Color C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c.linear; }

    // De medianoche a medianoche. `Noche` y `Ocaso`: cuánto pesa el postproceso frío de la noche y el cálido del orto y el ocaso.
    // (revisión a cuatro horas: el orto y el ocaso salían rojo sangre y la noche, oscura de más: el sol bajo, menos teñido; la noche,
    // con más luna y más ambiente: se tiene que poder jugar)
    static readonly Clave[] Claves =
    {
        new Clave { Hora = 0f, Sol = C("#b9cbff"), Fuerza = 0.95f, Cabeceo = 58, Guinada = 40, Cielo = C("#8098d0"), Horizonte = C("#7186ba"), Suelo = C("#485476"), Ambiente = 1.3f,
            Exposicion = 0.7f, Tinte = C("#4a62a0"), Niebla = C("#4a5e88"), Bruma = 0.0036f, Noche = 1, Ocaso = 0 },
        new Clave { Hora = 5f, Sol = C("#b9cbff"), Fuerza = 0.9f, Cabeceo = 40, Guinada = 70, Cielo = C("#8098d0"), Horizonte = C("#7186ba"), Suelo = C("#485476"), Ambiente = 1.3f,
            Exposicion = 0.7f, Tinte = C("#4a62a0"), Niebla = C("#4a5e88"), Bruma = 0.0036f, Noche = 1, Ocaso = 0 },
        new Clave { Hora = 6.3f, Sol = C("#ffd0a8"), Fuerza = 1.0f, Cabeceo = 10, Guinada = -78, Cielo = C("#9aaccc"), Horizonte = C("#d8bfa8"), Suelo = C("#665a50"), Ambiente = 1.1f,
            Exposicion = 1.0f, Tinte = C("#8a8fa6"), Niebla = C("#d4c0b0"), Bruma = 0.0040f, Noche = 0.1f, Ocaso = 1 },
        new Clave { Hora = 8.5f, Sol = C("#ffeacc"), Fuerza = 1.35f, Cabeceo = 32, Guinada = -100, Cielo = C("#9fbbe6"), Horizonte = C("#cdd6e0"), Suelo = C("#6e6252"), Ambiente = 1.05f,
            Exposicion = 1.1f, Tinte = C("#858f99"), Niebla = C("#c2cfdb"), Bruma = 0.0034f, Noche = 0, Ocaso = 0.15f },
        new Clave { Hora = 12.5f, Sol = C("#fff6e4"), Fuerza = 1.55f, Cabeceo = 64, Guinada = -150, Cielo = C("#a6c3ee"), Horizonte = C("#d5dde6"), Suelo = C("#766a58"), Ambiente = 1.1f,
            Exposicion = 1.2f, Tinte = C("#858f99"), Niebla = C("#b8c7d6"), Bruma = 0.0030f, Noche = 0, Ocaso = 0 },
        new Clave { Hora = 17f, Sol = C("#ffedcc"), Fuerza = 1.5f, Cabeceo = 38, Guinada = -218, Cielo = C("#a2bde6"), Horizonte = C("#d9d6d0"), Suelo = C("#756856"), Ambiente = 1.1f,
            Exposicion = 1.15f, Tinte = C("#858f99"), Niebla = C("#b8c7d6"), Bruma = 0.0032f, Noche = 0, Ocaso = 0.2f },
        new Clave { Hora = 19.6f, Sol = C("#ffc49a"), Fuerza = 1.05f, Cabeceo = 10, Guinada = -258, Cielo = C("#98a6c8"), Horizonte = C("#dcb89c"), Suelo = C("#665850"), Ambiente = 1.1f,
            Exposicion = 0.95f, Tinte = C("#8f8aa0"), Niebla = C("#d8b8a0"), Bruma = 0.0040f, Noche = 0.15f, Ocaso = 1 },
        new Clave { Hora = 20.8f, Sol = C("#b9cbff"), Fuerza = 0.85f, Cabeceo = 46, Guinada = 10, Cielo = C("#8098d0"), Horizonte = C("#7186ba"), Suelo = C("#485476"), Ambiente = 1.3f,
            Exposicion = 0.7f, Tinte = C("#4a62a0"), Niebla = C("#4a5e88"), Bruma = 0.0036f, Noche = 1, Ocaso = 0.1f },
        new Clave { Hora = 24f, Sol = C("#b9cbff"), Fuerza = 0.95f, Cabeceo = 58, Guinada = 40, Cielo = C("#8098d0"), Horizonte = C("#7186ba"), Suelo = C("#485476"), Ambiente = 1.3f,
            Exposicion = 0.7f, Tinte = C("#4a62a0"), Niebla = C("#4a5e88"), Bruma = 0.0036f, Noche = 1, Ocaso = 0 },
    };

    Light sol;
    Material cielo;
    Component volNoche, volOcaso;
    Type tipoVolumen;
    Light[] apagadas = Array.Empty<Light>();
    // lo que había
    Material cieloDeAntes;
    AmbientMode ambienteDeAntes;
    bool nieblaDeAntes;
    Color colorNieblaDeAntes;
    float densidadDeAntes, intensidadDeAntes;
    FogMode modoDeAntes;
    Light solDeAntes;

    /// <summary>Pone la luz de la plaza bajo `padre`. `reloj`: los milisegundos del mundo (sin él, el del juego).</summary>
    public static LuzDeLaPlaza Poner(Transform padre, Func<double> reloj)
    {
        var go = new GameObject("Luz de la plaza");
        go.transform.SetParent(padre, false);
        var luz = go.AddComponent<LuzDeLaPlaza>();
        luz.Reloj = reloj;
        luz.Montar();
        return luz;
    }

    void Montar()
    {
        apagadas = Array.FindAll(FindObjectsByType<Light>(FindObjectsSortMode.None), l => l.type == LightType.Directional && l.enabled);
        foreach (var l in apagadas) l.enabled = false;
        sol = new GameObject("Sol").AddComponent<Light>();
        sol.transform.SetParent(transform, false);
        sol.type = LightType.Directional;
        sol.shadows = LightShadows.Soft;
        sol.shadowStrength = 0.9f;

        cieloDeAntes = RenderSettings.skybox; ambienteDeAntes = RenderSettings.ambientMode; nieblaDeAntes = RenderSettings.fog;
        colorNieblaDeAntes = RenderSettings.fogColor; densidadDeAntes = RenderSettings.fogDensity; modoDeAntes = RenderSettings.fogMode;
        intensidadDeAntes = RenderSettings.ambientIntensity; solDeAntes = RenderSettings.sun;
        var base_ = Resources.Load<Material>("Ciudad/Banco/cielo");
        if (base_ != null) { cielo = new Material(base_); RenderSettings.skybox = cielo; }
        RenderSettings.sun = sol;
        RenderSettings.ambientMode = AmbientMode.Trilight;       // tres colores por hora: no hay que rehacer la luz del cielo a cada rato
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;

        // El postproceso: el de siempre y, encima, el frío de la noche y el cálido del ocaso, cada uno con su peso según la hora.
        // ponytail: el Volume va por reflexión porque este ensamblado no referencia el núcleo de SRP; si se le añade la referencia, tiparlo.
        var tipo = Type.GetType("UnityEngine.Rendering.Volume, Unity.RenderPipelines.Core.Runtime");
        if (tipo != null)
        {
            tipoVolumen = tipo;
            Component Volumen(string perfil, float prioridad)
            {
                var p = Resources.Load<ScriptableObject>("Ciudad/Banco/" + perfil);
                if (p == null) return null;
                var v = new GameObject("Postproceso " + perfil).AddComponent(tipo);
                v.transform.SetParent(transform, false);
                Dar(v, "isGlobal", true);
                Dar(v, "priority", prioridad);
                Dar(v, "sharedProfile", p);
                return v;
            }
            Volumen("Ambiente", 0);
            volNoche = Volumen("AmbienteNoche", 1);
            volOcaso = Volumen("AmbienteOcaso", 2);
        }
        Aplicar(Hora());
    }

    /// <summary>Da valor a un miembro del Volume, sea campo o propiedad (cambia de una versión de SRP a otra).</summary>
    void Dar(Component v, string nombre, object valor)
    {
        var campo = tipoVolumen.GetField(nombre);
        if (campo != null) { campo.SetValue(v, valor); return; }
        tipoVolumen.GetProperty(nombre)?.SetValue(v, valor);
    }

    float Hora()
    {
        if (HoraFija >= 0) return HoraFija % 24;
        double minutos = (Reloj != null ? Reloj() : Time.timeAsDouble * 1000) / 60000.0;
        // (el mundo arranca a media mañana)
        return (float)((minutos / MinutosPorDia * 24 + 9) % 24);
    }

    void Update() => Aplicar(Hora());

    public void Aplicar(float hora)
    {
        int k = 0;
        while (k < Claves.Length - 2 && hora >= Claves[k + 1].Hora) k++;
        Clave a = Claves[k], b = Claves[k + 1];
        float t = Mathf.InverseLerp(a.Hora, b.Hora, hora);
        t = t * t * (3 - 2 * t);
        sol.color = Color.Lerp(a.Sol, b.Sol, t);
        sol.intensity = Mathf.Lerp(a.Fuerza, b.Fuerza, t);
        // de sol a luna (y al revés) la luz cambia de sitio: en ese cruce se apaga un momento, y no se ve el salto de las sombras
        bool cruce = (a.Noche > 0.5f) != (b.Noche > 0.5f);
        if (cruce) sol.intensity *= Mathf.Abs(2 * t - 1);
        var desde = cruce && t >= 0.5f ? b : a;
        var hasta = cruce && t < 0.5f ? a : b;
        sol.transform.rotation = Quaternion.Euler(Mathf.Lerp(desde.Cabeceo, hasta.Cabeceo, t), Mathf.Lerp(desde.Guinada, hasta.Guinada, t), 0);
        RenderSettings.ambientSkyColor = Color.Lerp(a.Cielo, b.Cielo, t) * Mathf.Lerp(a.Ambiente, b.Ambiente, t);
        RenderSettings.ambientEquatorColor = Color.Lerp(a.Horizonte, b.Horizonte, t) * Mathf.Lerp(a.Ambiente, b.Ambiente, t);
        RenderSettings.ambientGroundColor = Color.Lerp(a.Suelo, b.Suelo, t) * Mathf.Lerp(a.Ambiente, b.Ambiente, t);
        RenderSettings.fogColor = Color.Lerp(a.Niebla, b.Niebla, t);
        RenderSettings.fogDensity = Mathf.Lerp(a.Bruma, b.Bruma, t);
        if (cielo != null)
        {
            cielo.SetFloat("_Exposure", Mathf.Lerp(a.Exposicion, b.Exposicion, t));
            cielo.SetColor("_SkyTint", Color.Lerp(a.Tinte, b.Tinte, t));
        }
        if (volNoche != null) Dar(volNoche, "weight", Mathf.Lerp(a.Noche, b.Noche, t));
        if (volOcaso != null) Dar(volOcaso, "weight", Mathf.Lerp(a.Ocaso, b.Ocaso, t));
    }

    void OnDestroy()
    {
        foreach (var l in apagadas) if (l != null) l.enabled = true;
        RenderSettings.skybox = cieloDeAntes; RenderSettings.ambientMode = ambienteDeAntes; RenderSettings.fog = nieblaDeAntes;
        RenderSettings.fogColor = colorNieblaDeAntes; RenderSettings.fogDensity = densidadDeAntes; RenderSettings.fogMode = modoDeAntes;
        RenderSettings.ambientIntensity = intensidadDeAntes; RenderSettings.sun = solDeAntes;
    }
}
