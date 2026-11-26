using UnityEngine;

/// <summary>La luz de un fuego (el hogar de la plaza): tiembla en intensidad y se mueve un poco, con ruido suave.</summary>
[RequireComponent(typeof(Light))]
public sealed class LuzDeFuego : MonoBehaviour
{
    public float Temblor = 0.28f, Ritmo = 5.5f, Vaiven = 0.12f;
    Light luz;
    float intensidad;
    Vector3 sitio;

    void Awake()
    {
        luz = GetComponent<Light>();
        intensidad = luz.intensity;
        sitio = transform.localPosition;
    }

    void Update()
    {
        float t = Time.time * Ritmo;
        luz.intensity = intensidad * (1 + Temblor * (Mathf.PerlinNoise(t, 0.3f) * 2 - 1));
        transform.localPosition = sitio + new Vector3(Mathf.PerlinNoise(t * 0.7f, 5) - 0.5f, Mathf.PerlinNoise(t * 0.7f, 9) - 0.5f, Mathf.PerlinNoise(t * 0.7f, 13) - 0.5f) * Vaiven;
    }
}
