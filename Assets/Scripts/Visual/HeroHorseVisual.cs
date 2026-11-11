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

        public Animator Animator => _animator;
        public Transform Saddle => _saddle;

        public void Configure(Animator animator, Transform saddle, GameObject reinsHand, GameObject reinsNeck,
            RuntimeAnimatorController sword, RuntimeAnimatorController lancer, RuntimeAnimatorController archer)
        {
            _animator = animator; _saddle = saddle; _reinsHand = reinsHand; _reinsNeck = reinsNeck;
            _swordRider = sword; _lancerRider = lancer; _archerRider = archer;
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
