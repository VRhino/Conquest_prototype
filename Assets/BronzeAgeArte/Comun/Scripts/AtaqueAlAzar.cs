using UnityEngine;

namespace BronzeAge.Batalla
{
    /// <summary>
    /// En el estado que elige el ataque (Animator de las tropas BronzeAge): al entrar pone «AtaqueAlAzar» a un número al
    /// azar entre 0 y <see cref="variantes"/> - 1, y las transiciones de salida van al ataque de ese número (Víctor:
    /// estocada alta o baja al azar en cada ataque).
    /// </summary>
    public class AtaqueAlAzar : StateMachineBehaviour
    {
        public const string Parametro = "AtaqueAlAzar";
        static readonly int Hash = Animator.StringToHash(Parametro);
        public int variantes = 2;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            animator.SetInteger(Hash, Random.Range(0, Mathf.Max(1, variantes)));
        }
    }
}
