using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// La gente y los animales de la ciudad por dentro, con los modelos y las animaciones que ya tiene el juego: los vecinos son las figuras de
/// batalla de los granjeros y los leñadores (`Squads/BronzeAge/*/BattleVisuals`, su `Locomocion` por `VelocidadReal`), y los animales, los de
/// las caravanas (`MapaMundo/Caravana`), que llevan el esqueleto del caballo y andan con su controlador (`MountGaitBlend`).
///
/// Solo es visual (no hay entidades ni lógica de juego): los vecinos van de una puerta a otra por las calles y las sendas de la planta, se
/// paran un rato y siguen; el buey del molino da vueltas a la muela; los demás animales están en su sitio, vivos.
/// </summary>
public static class VidaDeLaPlaza
{
    static readonly string[] Vecinos =
    {
        "Squads/BronzeAge/granjeros/BattleVisuals/Granjero_A", "Squads/BronzeAge/granjeros/BattleVisuals/Granjero_B", "Squads/BronzeAge/granjeros/BattleVisuals/Granjero_C",
        "Squads/BronzeAge/granjeros/BattleVisuals/Granjero_D", "Squads/BronzeAge/lenadores/BattleVisuals/Lenador_A", "Squads/BronzeAge/lenadores/BattleVisuals/Lenador_B",
        "Squads/BronzeAge/lenadores/BattleVisuals/Lenador_C", "Squads/BronzeAge/lenadores/BattleVisuals/Lenador_D",
    };
    static readonly int Paso = Animator.StringToHash("MountGaitBlend"), Ritmo = Animator.StringToHash("MountGaitScale");

    /// <summary>La red de por dónde se anda: los nudos, sus vecinos y los sitios adonde va la gente (los cabos de las sendas: puertas y patios).</summary>
    public sealed class Red
    {
        public readonly List<Vector3> Nudos = new List<Vector3>();
        public readonly List<List<int>> Vecinos = new List<List<int>>();
        public readonly List<int> Sitios = new List<int>();

        int Nudo(Vector3 p)
        {
            for (int i = 0; i < Nudos.Count; i++) if ((Nudos[i] - p).sqrMagnitude < 0.5f) return i;
            Nudos.Add(p);
            Vecinos.Add(new List<int>());
            return Nudos.Count - 1;
        }

        void Unir(int a, int b)
        {
            if (a == b || Vecinos[a].Contains(b)) return;
            Vecinos[a].Add(b);
            Vecinos[b].Add(a);
        }

        public Red(List<(Vector3 a, Vector3 b, float ancho)> calles, List<(Vector3, Vector3)> sendas)
        {
            static (Vector3 q, float t) Sobre(Vector3 p, Vector3 a, Vector3 b)
            {
                var ab = b - a;
                float l = ab.sqrMagnitude, t = l < 1e-6f ? 0 : Mathf.Clamp01(Vector3.Dot(p - a, ab) / l);
                return (a + ab * t, t);
            }
            // cada calle, partida donde le llega una senda u otra calle
            for (int i = 0; i < calles.Count; i++)
            {
                var (a, b, ancho) = calles[i];
                var cortes = new List<(float t, int nudo)> { (0, Nudo(a)), (1, Nudo(b)) };
                foreach (var (sa, sb) in sendas)
                    foreach (var p in new[] { sa, sb })
                    {
                        var (q, t) = Sobre(p, a, b);
                        if ((q - p).sqrMagnitude < 0.5f) cortes.Add((t, Nudo(q)));
                    }
                for (int j = 0; j < calles.Count; j++)
                {
                    if (j == i) continue;
                    foreach (var p in new[] { calles[j].a, calles[j].b })
                    {
                        var (q, t) = Sobre(p, a, b);
                        if ((q - p).magnitude > ancho + calles[j].ancho + 1.5f) continue;
                        int n = Nudo(q);
                        cortes.Add((t, n));
                        Unir(n, Nudo(p));
                    }
                }
                cortes.Sort((x, y) => x.t.CompareTo(y.t));
                for (int k = 1; k < cortes.Count; k++) Unir(cortes[k - 1].nudo, cortes[k].nudo);
            }
            foreach (var (sa, sb) in sendas) Unir(Nudo(sa), Nudo(sb));
            // adonde se va: los cabos sueltos que no son de calle
            for (int i = 0; i < Nudos.Count; i++)
                if (Vecinos[i].Count == 1 && !calles.Any(c => (Sobre(Nudos[i], c.a, c.b).q - Nudos[i]).sqrMagnitude < 0.5f)) Sitios.Add(i);
        }

        /// <summary>El camino más corto de un nudo a otro (vacío si no lo hay).</summary>
        public List<Vector3> Camino(int de, int a)
        {
            var dist = Enumerable.Repeat(float.MaxValue, Nudos.Count).ToArray();
            var previo = Enumerable.Repeat(-1, Nudos.Count).ToArray();
            var hecho = new bool[Nudos.Count];
            dist[de] = 0;
            while (true)
            {
                int u = -1;
                for (int i = 0; i < Nudos.Count; i++) if (!hecho[i] && dist[i] < float.MaxValue && (u < 0 || dist[i] < dist[u])) u = i;
                if (u < 0 || u == a) break;
                hecho[u] = true;
                foreach (var v in Vecinos[u])
                {
                    float d = dist[u] + (Nudos[u] - Nudos[v]).magnitude;
                    if (d < dist[v]) { dist[v] = d; previo[v] = u; }
                }
            }
            var camino = new List<Vector3>();
            if (previo[a] < 0 && a != de) return camino;
            for (int n = a; n >= 0; n = previo[n]) camino.Add(Nudos[n]);
            camino.Reverse();
            return camino;
        }
    }

    static readonly string[] Cabras = { "Goat", "Goat_blanca", "Goat_negra" }, Perros = { "Dog", "Dog_negro", "Dog_blanco" }, Gallinas = { "Hen", "Hen_blanca", "Hen_negra" };

    static GameObject Suelto(Transform padre, string prefab, Vector3 donde, System.Random azar)
    {
        var modelo = Resources.Load<GameObject>("Ciudad/Animales/" + prefab);
        if (modelo == null) return null;
        var go = Object.Instantiate(modelo, padre);
        go.name = prefab;
        go.transform.SetPositionAndRotation(donde, Quaternion.Euler(0, (float)azar.NextDouble() * 360, 0));
        return go;
    }

    /// <summary>Los animales sueltos de la plaza: en cada prado (monte junto a las casas), un hatillo de cabras que pasta y se mueve poco; en
    /// los patios, gallinas (y algún gallo) que picotean. `prados` y `patios`: el centro de sus casillas.</summary>
    public static void Soltar(Transform padre, List<Vector3> prados, List<Vector3> patios, int semilla)
    {
        var azar = new System.Random(semilla * 7 + 1);
        Vector3 Cerca(Vector3 c, float r) => c + new Vector3((float)azar.NextDouble() * 2 - 1, 0, (float)azar.NextDouble() * 2 - 1) * r;
        int hatos = Mathf.Min(4, prados.Count / 12);
        for (int k = 0; k < hatos; k++)
        {
            var sitio = prados[azar.Next(prados.Count)];
            int n = 2 + azar.Next(3);
            Debug.Log($"[Vida] cabras en ({sitio.x:0.0}, {sitio.z:0.0})");
            for (int i = 0; i < n; i++)
            {
                var go = Suelto(padre, Cabras[azar.Next(Cabras.Length)], Cerca(sitio, 1.5f), azar);
                if (go == null) return;
                Animal(go);
                var m = go.AddComponent<Merodea>();
                m.Casa = sitio; m.Radio = 4f; m.Paso = 0.55f; m.Azar = new System.Random(semilla + k * 13 + i);
            }
        }
        int corros = Mathf.Min(5, patios.Count / 4);
        for (int k = 0; k < corros; k++)
        {
            var sitio = patios[azar.Next(patios.Count)];
            int n = 3 + azar.Next(4);
            Debug.Log($"[Vida] gallinas en ({sitio.x:0.0}, {sitio.z:0.0})");
            for (int i = 0; i < n; i++)
            {
                var go = Suelto(padre, i == 0 && k % 2 == 0 ? "Rooster" : Gallinas[azar.Next(Gallinas.Length)], Cerca(sitio, 1.2f), azar);
                if (go == null) return;
                var m = go.AddComponent<Merodea>();
                m.Casa = sitio; m.Radio = 2.6f; m.Paso = 0.5f; m.Azar = new System.Random(semilla + k * 17 + i);
                go.AddComponent<Gallina>();
            }
        }
    }

    /// <summary>Pone `cuantos` vecinos más a andar por la red y, la primera vez (`conPerros`), un perro o dos que van por ella a su aire.</summary>
    public static void Poblar(Transform padre, Red red, int cuantos, bool conPerros, int semilla)
    {
        if (red.Sitios.Count < 2) return;
        var azar = new System.Random(semilla);
        for (int k = 0; conPerros && k < Mathf.Clamp(cuantos / 5, 1, 3); k++)
        {
            var go = Suelto(padre, Perros[azar.Next(Perros.Length)], Vector3.zero, azar);
            if (go == null) break;
            Animal(go);
            var v = go.AddComponent<Vecino>();
            v.Red = red; v.Cuadrupedo = true;
            v.Azar = new System.Random(semilla * 53 + k);
            v.Empezar();
        }
        for (int k = 0; k < cuantos; k++)
        {
            var modelo = Resources.Load<GameObject>(Vecinos[azar.Next(Vecinos.Length)]);
            if (modelo == null) continue;
            var go = Object.Instantiate(modelo, padre);
            go.name = "Vecino";
            SoloFigura(go);
            var v = go.AddComponent<Vecino>();
            v.Red = red;
            v.Azar = new System.Random(semilla * 31 + k);
            v.Empezar();
        }
    }

    /// <summary>De una figura del juego, solo lo que se ve y su Animator: sus scripts de batalla y sus colisores no van aquí.</summary>
    static void SoloFigura(GameObject go)
    {
        foreach (var b in go.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        foreach (var a in go.GetComponentsInChildren<Animator>(true)) { a.applyRootMotion = false; a.cullingMode = AnimatorCullingMode.CullUpdateTransforms; }
    }

    static readonly string[] Bestias = { "Goat", "Dog", "Donkey_Pack", "Ox_Draught" };

    /// <summary>Un animal de cuatro patas, vivo: con su controlador (`Ciudad/Animales/AC_*`: el reposo y el paso del caballo llevados a su
    /// esqueleto por `AnimalesDePlaza`), parado o al paso. Los que no lo tienen (el camello) se quedan quietos.</summary>
    public static Animator Animal(GameObject go, bool andando = false)
    {
        var a = go.GetComponentInChildren<Animator>();
        if (a == null) a = go.AddComponent<Animator>();
        var cual = Bestias.FirstOrDefault(b => go.name.Contains(b));
        var control = cual == null ? null : Resources.Load<RuntimeAnimatorController>("Ciudad/Animales/AC_" + cual);
        if (control == null) return a;
        // Las animaciones atan cada hueso por su ruta, que empieza en el nombre del esqueleto: el del burro y el del buey se llaman de otra
        // manera (`DonkeyRig`, `OxRig`), así que aquí toman el del caballo.
        foreach (Transform hijo in a.transform)
            if (hijo.name.EndsWith("Rig") && hijo.name != "HorseRig") hijo.name = "HorseRig";
        a.runtimeAnimatorController = control;
        a.applyRootMotion = false;
        a.Rebind();
        a.SetFloat(Paso, andando ? 1 : 0);
        a.SetFloat(Ritmo, andando ? 0.7f : 1);
        return a;
    }

    /// <summary>Un caballo suelto en su cercado: el del héroe (`Mounts/HeroHorse`), solo su cuerpo y su paso, que se mueve poco y no salta
    /// la cerca.</summary>
    public static GameObject Caballo(Transform padre, Vector3 donde, Vector3 mira)
    {
        var modelo = Resources.Load<GameObject>("Mounts/HeroHorse");
        if (modelo == null) return null;
        var go = Object.Instantiate(modelo, padre);
        go.name = "Caballo";
        go.transform.SetPositionAndRotation(donde, Quaternion.LookRotation(mira));
        SoloFigura(go);
        var m = go.AddComponent<Merodea>();
        m.Casa = donde; m.Radio = 5f; m.Paso = 0.8f;
        m.Azar = new System.Random(Mathf.RoundToInt(donde.x * 31 + donde.z * 17));
        return go;
    }

    /// <summary>El buey de la muela: da vueltas alrededor de `centro`, a `radio`.</summary>
    public static GameObject Noria(Transform padre, Vector3 centro, float radio)
    {
        var modelo = Resources.Load<GameObject>("MapaMundo/Caravana/Ox_Draught");
        if (modelo == null) return null;
        var go = Object.Instantiate(modelo, padre);
        go.name = "Ox_Draught de la muela";
        Animal(go, true);
        var n = go.AddComponent<EnNoria>();
        n.Centro = centro;
        n.Radio = radio;
        n.Giro = modelo.transform.rotation;
        n.Poner(0.8f);
        return go;
    }

    /// <summary>Para una imagen fija (el banco de pruebas, fuera de Play, donde el Animator no corre): cada vecino a medio camino, y a cada
    /// figura se le pone a mano un instante de su animación (el paso si anda, el reposo si no).</summary>
    public static void Posar(GameObject raiz)
    {
        var azar = new System.Random(3);
        foreach (var v in raiz.GetComponentsInChildren<Vecino>()) v.Avanzar((float)azar.NextDouble() * 25);
        int posados = 0;
        foreach (var a in raiz.GetComponentsInChildren<Animator>())
        {
            if (a.runtimeAnimatorController == null) continue;
            bool anda = a.GetComponentInParent<Vecino>() != null || a.GetComponentInParent<EnNoria>() != null;
            var clips = a.runtimeAnimatorController.animationClips;
            var clip = clips.FirstOrDefault(c => anda ? c.name.Contains("Andar") || c.name.Contains("Walk") : c.name.Contains("Reposo") || c.name.Contains("Idle")) ?? clips.FirstOrDefault();
            if (clip == null) continue;
            var hueso = a.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Forearm.L");
            var antes = hueso != null ? hueso.localRotation : Quaternion.identity;
            clip.SampleAnimation(a.gameObject, (0.2f + 0.6f * (float)azar.NextDouble()) * clip.length);
            if (hueso != null && anda) Debug.Log($"[Vida] {a.name} en ({a.transform.position.x:0.0}, {a.transform.position.z:0.0}) mirando a {a.transform.eulerAngles.y:0}°: el paso gira el antebrazo {Quaternion.Angle(antes, hueso.localRotation):0}°");
            posados++;
        }
        Debug.Log($"[Vida] posadas {posados} figuras");
    }
}

/// <summary>Un vecino de la plaza: va de un sitio a otro por la red, se para un rato y sigue.</summary>
public sealed class Vecino : MonoBehaviour
{
    public static readonly int Velocidad = Animator.StringToHash("VelocidadReal");
    const float Andar = 1.15f;                 // el umbral del paso en la `Locomocion` de las figuras

    static readonly int PasoDeBestia = Animator.StringToHash("MountGaitBlend"), RitmoDeBestia = Animator.StringToHash("MountGaitScale");
    public VidaDeLaPlaza.Red Red;
    public System.Random Azar;
    /// <summary>Un perro (el esqueleto y el paso del caballo), no una persona.</summary>
    public bool Cuadrupedo;
    List<Vector3> camino = new List<Vector3>();
    int tramo, sitio;
    float espera, paso = Andar;
    Vector3 lado;
    Animator animador;

    float Al(float a, float b) => a + (float)Azar.NextDouble() * (b - a);

    public void Empezar()
    {
        animador = GetComponentInChildren<Animator>();
        sitio = Red.Sitios[Azar.Next(Red.Sitios.Count)];
        // cada uno, algo corrido de la raya: no van en fila por el eje de la calle
        lado = new Vector3(Al(-0.9f, 0.9f), 0, Al(-0.9f, 0.9f));
        paso = Andar * Al(0.85f, 1.1f);
        transform.position = Red.Nudos[sitio] + lado;
        transform.rotation = Quaternion.Euler(0, Al(0, 360), 0);
        espera = Al(0, 6);
    }

    /// <summary>La planta ha cambiado: toma la red nueva sin moverse de donde está (su sitio pasa a ser el más cercano de la nueva y, si iba
    /// de camino, vuelve a salir desde él).</summary>
    public void CambiarRed(VidaDeLaPlaza.Red red)
    {
        Red = red;
        if (red.Sitios.Count == 0) { camino.Clear(); tramo = 0; return; }
        sitio = red.Sitios.OrderBy(s => (red.Nudos[s] - transform.position).sqrMagnitude).First();
        // lo que le queda es ir derecho a ese sitio; al llegar, sigue como siempre
        camino = new List<Vector3> { transform.position - lado, red.Nudos[sitio] };
        tramo = 1;
    }

    void Salir()
    {
        for (int intento = 0; intento < 4; intento++)
        {
            int a = Red.Sitios[Azar.Next(Red.Sitios.Count)];
            if (a == sitio) continue;
            var c = Red.Camino(sitio, a);
            if (c.Count < 2) continue;
            camino = c;
            tramo = 1;
            sitio = a;
            return;
        }
        espera = Al(3, 8);
    }

    /// <summary>Anda `metros` por su camino (sale si estaba parado). Devuelve si sigue andando.</summary>
    public bool Avanzar(float metros)
    {
        if (tramo >= camino.Count) Salir();
        while (metros > 0 && tramo < camino.Count)
        {
            var meta = camino[tramo] + lado;
            var d = meta - transform.position;
            d.y = 0;
            float falta = d.magnitude;
            if (falta > 1e-3f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), Mathf.Clamp01(metros * 2.5f));
            if (falta <= metros) { transform.position = meta; metros -= falta; tramo++; }
            else { transform.position += d / falta * metros; metros = 0; }
        }
        return tramo < camino.Count;
    }

    void Update()
    {
        if (Red == null) return;
        bool anda = false;
        if (espera > 0) espera -= Time.deltaTime;
        else
        {
            anda = Avanzar(paso * Time.deltaTime);
            if (!anda) espera = Al(4, 14);
        }
        if (animador == null) return;
        if (!Cuadrupedo) animador.SetFloat(Velocidad, anda ? paso : 0, 0.15f, Time.deltaTime);
        else { animador.SetFloat(PasoDeBestia, anda ? 1 : 0, 0.2f, Time.deltaTime); animador.SetFloat(RitmoDeBestia, anda ? 1.6f : 1); }
    }
}

/// <summary>El animal que da vueltas a la muela.</summary>
public sealed class EnNoria : MonoBehaviour
{
    public Vector3 Centro;
    public float Radio = 4.1f, Paso = 0.75f;
    public Quaternion Giro = Quaternion.identity;
    float angulo;

    public void Poner(float a)
    {
        angulo = a;
        var fuera = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
        transform.SetPositionAndRotation(Centro + fuera * Radio, Quaternion.LookRotation(new Vector3(-fuera.z, 0, fuera.x)) * Giro);
    }

    void Update() => Poner(angulo + Paso / Radio * Time.deltaTime);
}

/// <summary>Un animal suelto: se mueve poco alrededor de su sitio (pasta o picotea un rato, da unos pasos a otro punto libre y vuelve a
/// parar). No atraviesa lo que tiene colisión (cercas, casas): si el camino a su punto no está libre, elige otro.</summary>
public sealed class Merodea : MonoBehaviour
{
    static readonly int PasoDeBestia = Animator.StringToHash("MountGaitBlend"), RitmoDeBestia = Animator.StringToHash("MountGaitScale");
    public Vector3 Casa;
    public float Radio = 3, Paso = 0.5f;
    public System.Random Azar = new System.Random();
    /// <summary>¿Está andando ahora? (lo lee `Gallina` para mover las patas)</summary>
    public bool Anda { get; private set; }
    Vector3 meta;
    float espera;
    Animator animador;

    void Start()
    {
        animador = GetComponentInChildren<Animator>();
        espera = (float)Azar.NextDouble() * 5;
        meta = transform.position;
    }

    void Update()
    {
        var d = meta - transform.position;
        d.y = 0;
        Anda = espera <= 0 && d.sqrMagnitude > 0.01f;
        if (Anda)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), Mathf.Clamp01(Time.deltaTime * 4));
            transform.position += d.normalized * Mathf.Min(Paso * Time.deltaTime, d.magnitude);
        }
        else if (espera > 0) espera -= Time.deltaTime;
        else
        {
            // otro punto cerca de casa, con el camino libre
            espera = 2 + (float)Azar.NextDouble() * 9;
            var a = (float)Azar.NextDouble() * 6.2832f;
            var p = Casa + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (Radio * Mathf.Sqrt((float)Azar.NextDouble()));
            if (!Physics.Linecast(transform.position + Vector3.up * 0.3f, p + Vector3.up * 0.3f, ~0, QueryTriggerInteraction.Ignore)) meta = p;
        }
        if (animador != null && animador.runtimeAnimatorController != null)
        {
            animador.SetFloat(PasoDeBestia, Anda ? 1 : 0, 0.2f, Time.deltaTime);
            animador.SetFloat(RitmoDeBestia, Anda ? 1.3f : 1);
        }
    }
}

/// <summary>La gallina se mueve por código (su esqueleto es propio y pequeño: no hay animación que heredar): al andar, las patas se alternan y
/// la cabeza va y viene a cada paso; parada, picotea de tanto en tanto y sacude la cola.</summary>
public sealed class Gallina : MonoBehaviour
{
    Transform cuello, cabeza, cola, pataI, pataD, cuerpo;
    Quaternion cuello0, cabeza0, cola0, pataI0, pataD0, cuerpo0;
    Merodea merodea;
    float fase, picoteo;

    static Transform Hueso(Transform t, string nombre)
    {
        if (t.name == nombre) return t;
        foreach (Transform h in t) { var r = Hueso(h, nombre); if (r != null) return r; }
        return null;
    }

    void Start()
    {
        merodea = GetComponent<Merodea>();
        cuello = Hueso(transform, "Neck"); cabeza = Hueso(transform, "Head"); cola = Hueso(transform, "Tail");
        pataI = Hueso(transform, "Leg.L"); pataD = Hueso(transform, "Leg.R"); cuerpo = Hueso(transform, "Body");
        if (cuello == null || pataI == null || pataD == null || cuerpo == null) { enabled = false; return; }
        cuello0 = cuello.localRotation; cabeza0 = cabeza != null ? cabeza.localRotation : Quaternion.identity; cola0 = cola != null ? cola.localRotation : Quaternion.identity;
        pataI0 = pataI.localRotation; pataD0 = pataD.localRotation; cuerpo0 = cuerpo.localRotation;
        fase = Random.value * 10;
    }

    void LateUpdate()
    {
        bool anda = merodea != null && merodea.Anda;
        fase += Time.deltaTime * (anda ? 9f : 1f);
        float paso = anda ? Mathf.Sin(fase) : 0;
        // las patas, alternas; el cuerpo, un vaivén corto
        pataI.localRotation = pataI0 * Quaternion.Euler(paso * 28, 0, 0);
        pataD.localRotation = pataD0 * Quaternion.Euler(-paso * 28, 0, 0);
        cuerpo.localRotation = cuerpo0 * Quaternion.Euler(0, 0, paso * 4);
        // la cabeza: al andar, adelante y atrás a cada paso; parada, picotea (baja el cuello de golpe, dos o tres veces, y descansa)
        float baja = 0;
        if (anda) baja = Mathf.Abs(Mathf.Sin(fase)) * 14 - 7;
        else
        {
            picoteo -= Time.deltaTime;
            if (picoteo < -1.4f) picoteo = 2 + Random.value * 5;
            if (picoteo < 0) baja = Mathf.Abs(Mathf.Sin(picoteo * 7)) * 62;
        }
        cuello.localRotation = cuello0 * Quaternion.Euler(baja, 0, 0);
        if (cabeza != null) cabeza.localRotation = cabeza0 * Quaternion.Euler(baja * 0.25f, 0, 0);
        if (cola != null) cola.localRotation = cola0 * Quaternion.Euler(Mathf.Sin(fase * 0.7f) * 5 - baja * 0.15f, 0, 0);
    }
}
