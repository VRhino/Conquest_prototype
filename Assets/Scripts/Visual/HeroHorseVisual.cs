using UnityEngine;

namespace ConquestTactics.Visual
{
    /// <summary>
    /// The hero's horse (M19), on Resources/Mounts/HeroHorse.prefab (built by CavalrySetup): the horse model with its
    /// gait Animator, its saddle, both kinds of reins and the hero's rider Animators, one per weapon. Visual only; the
    /// hero's <see cref="HeroMountPresenter"/> drives it.
    /// </summary>
    public sealed class HeroHorseVisual : MonoBehaviour
    {
        public const string ResourcePath = "Mounts/HeroHorse";

        [SerializeField] private Animator _animator;
        [Tooltip("Hueso Saddle: el asiento del jinete.")]
        [SerializeField] private Transform _saddle;
        [SerializeField] private GameObject _reinsHand;
        [SerializeField] private GameObject _reinsNeck;
        [Tooltip("Animators del héroe a caballo (andares, ataque, subir y bajar), por arma.")]
        [SerializeField] private RuntimeAnimatorController _swordRider;
        [SerializeField] private RuntimeAnimatorController _lancerRider;
        [SerializeField] private RuntimeAnimatorController _archerRider;
        [Tooltip("Pelajes (coat 1..n): bayo, negro, castaño, bayo claro, pío.")]
        [SerializeField] private Material[] _coats;

        public Animator Animator => _animator;
        public Transform Saddle => _saddle;

        public void Configure(Animator animator, Transform saddle, GameObject reinsHand, GameObject reinsNeck,
            RuntimeAnimatorController sword, RuntimeAnimatorController lancer, RuntimeAnimatorController archer)
        {
            _animator = animator; _saddle = saddle; _reinsHand = reinsHand; _reinsNeck = reinsNeck;
            _swordRider = sword; _lancerRider = lancer; _archerRider = archer;
        }

        public void SetCoats(Material[] coats) => _coats = coats;

        /// <summary>Puts coat <paramref name="coat"/> (1..n) on this copy's horse; 0 or unknown keeps the prefab's. With the war horses
        /// (<see cref="CaballosDeGuerra"/>), the coat's mount brings its own body and coat instead (only what is seen); its marks come from <paramref name="heroId"/>, as on the map.</summary>
        public void SetCoat(byte coat, string heroId = null)
        {
            var guerra = CaballosDeGuerra.Cargar();
            if (guerra != null && guerra.Vestir(gameObject, VestidoDelMotor.MonturaDePelaje(coat), heroId)) return;
            if (coat == 0 || _coats == null || coat > _coats.Length || _coats[coat - 1] == null) return;
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true)) r.sharedMaterial = _coats[coat - 1];
        }

        /// <summary>The rider's clips for the hero's weapon: sword and shield or greatsword, spear, or bow.</summary>
        public RuntimeAnimatorController RiderFor(HeroClass heroClass) => heroClass switch
        {
            HeroClass.Spear => _lancerRider,
            HeroClass.Bow => _archerRider,
            _ => _swordRider,
        };

        /// <summary>The archer needs both hands: its reins lie on the neck. The others hold them in the left fist.</summary>
        public void SetReins(HeroClass heroClass)
        {
            bool neck = heroClass == HeroClass.Bow;
            if (_reinsHand) _reinsHand.SetActive(!neck);
            if (_reinsNeck) _reinsNeck.SetActive(neck);
        }

        /// <summary>The gait tree's values for a ground speed, the trot at <paramref name="trotSpeed"/> and the gallop at <paramref name="gallopSpeed"/>.</summary>
        public static (float blend, float scale) Gait(float speed, float trotSpeed, float gallopSpeed)
        {
            float blend = MountGait.Blend(speed, trotSpeed, gallopSpeed);
            return (blend, MountGait.Scale(speed, blend));
        }

        public void SetGait(float blend, float scale)
        {
            if (_animator == null) return;
            _animator.SetFloat(AnimationHashes.MountGaitBlend, blend);
            _animator.SetFloat(AnimationHashes.MountGaitScale, scale);
        }
    }
}
