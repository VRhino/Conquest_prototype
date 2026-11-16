// La pieza del carro de caravana en el mundo 3D (paso 10, tanda 2): el carro de Synty (Resources/MapaMundo/CarroDeCaravana, lo arma
// `CarroDeCaravanaSetup`) con su animal delante (el caballo del héroe: no hay buey ni camello), a escala real. Se sitúa cada fotograma en su sitio
// del mapa; mira hacia donde avanza y el animal anda mientras se mueve y se para cuando se para. La usa `CosasEnElMapa` para las caravanas que
// viajan solas, y servirá igual detrás de un ejército (paso 11).
using UnityEngine;

namespace ConquestTactics.Mundo.Mapa
{
    public sealed class CarroDeCaravana
    {
        static readonly int Paso = Animator.StringToHash("MountGaitBlend"), Ritmo = Animator.StringToHash("MountGaitScale");
        static GameObject modeloCarro, modeloAnimal;

        readonly GameObject raiz;
        readonly Animator animal;
        Vector3 ultimo;
        bool situado;
        float quieto;

        /// <summary>¿Están los modelos? Sin ellos, el mapa sigue con su caja.</summary>
        public static bool Disponible => (modeloCarro ??= Resources.Load<GameObject>("MapaMundo/CarroDeCaravana")) != null
            && (modeloAnimal ??= Resources.Load<GameObject>("Mounts/HeroHorse")) != null;

        public CarroDeCaravana(Transform padre, string nombre)
        {
            raiz = Object.Instantiate(modeloCarro, padre);
            raiz.name = nombre;
            var tiro = raiz.transform.Find("Tiro");
            var a = Object.Instantiate(modeloAnimal, tiro != null ? tiro : raiz.transform);
            a.transform.localPosition = Vector3.zero;
            a.transform.localRotation = Quaternion.identity;
            // Solo su cuerpo y su paso: el resto del caballo del héroe (riendas, jinete) no va aquí.
            foreach (var b in a.GetComponentsInChildren<MonoBehaviour>()) b.enabled = false;
            animal = a.GetComponentInChildren<Animator>();
            if (animal != null) animal.cullingMode = AnimatorCullingMode.CullCompletely;
        }

        /// <summary>Lo pone en `pos` (de la escena): gira hacia donde se ha movido y el animal anda si se mueve.</summary>
        public void Situar(Vector3 pos, float dt)
        {
            var t = raiz.transform;
            var d = pos - ultimo;
            d.y = 0;
            var anda = situado && d.sqrMagnitude > 1e-6f;
            if (anda) t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(d), Mathf.Clamp01(dt * 6f));
            t.position = pos;
            ultimo = pos;
            situado = true;
            // Unos fotogramas quieto antes de pararlo: la posición del plan avanza a saltos pequeños.
            quieto = anda ? 0 : quieto + dt;
            if (animal != null)
            {
                animal.SetFloat(Paso, quieto < 0.3f && situado ? 1f : 0f);
                animal.SetFloat(Ritmo, 1f);
            }
        }

        public void Mostrar(bool si) { if (raiz.activeSelf != si) raiz.SetActive(si); }

        public void Soltar() => Object.Destroy(raiz);
    }
}
