using UnityEngine;

/// <summary>
/// El cuerpo de un soldado muerto se queda en el suelo (Víctor, 18-11: «el cuerpo dura unos segundos en el suelo y luego
/// desaparece»). UnitDeathSystem retira la unidad como siempre (fuera de la escuadra, de la formación y de los golpes) y, en
/// vez de destruir su visual, se lo pasa a <see cref="Retirar"/>: si su Animator tiene estado de muerte (las levas
/// BronzeAge), el visual se suelta de la entidad (nada lo mueve ni le escribe parámetros, sin colisionadores ni cajas de
/// golpe), reproduce la muerte y se queda tumbado <see cref="SEGUNDOS_EN_EL_SUELO"/> s; si no la tiene (las tropas de
/// Conquest sin clip de muerte), desaparece al instante como antes, para que no se quede de pie.
/// </summary>
public class CuerpoCaido : MonoBehaviour
{
    /// <summary>Lo que dura tumbado, después de acabar la animación de muerte.</summary>
    public const float SEGUNDOS_EN_EL_SUELO = 4f;
    /// <summary>Si la animación no acaba en este tiempo (otro Animator, otro estado), se va igual.</summary>
    const float SEGUNDOS_MAXIMO = 12f;

    static readonly int TriggerDeath = Animator.StringToHash("TriggerDeath");
    static readonly int IsDead = Animator.StringToHash("IsDead");
    static readonly int EstadoMuerte = Animator.StringToHash("Base Layer.Muerte");

    Animator _animator;
    float _desde, _tumbado = -1f;

    /// <summary>El visual de una unidad que acaba de morir: se queda tumbado o se destruye.</summary>
    public static void Retirar(GameObject visual)
    {
        if (visual == null) return;
        var animator = visual.GetComponent<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null || !animator.HasState(0, EstadoMuerte)
            || !TieneParametro(animator, TriggerDeath))
        {
            Destroy(visual);
            return;
        }
        // suelto de la entidad: nada lo mueve, nada le escribe parámetros (Stance, ataques), nada lo golpea
        foreach (var b in visual.GetComponentsInChildren<Behaviour>(true))
            if (b != null && b != animator) b.enabled = false;      // (null: un script que falta)
        foreach (var c in visual.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        if (TieneParametro(animator, IsDead)) animator.SetBool(IsDead, true);
        animator.SetTrigger(TriggerDeath);
        visual.name += " (caído)";
        visual.AddComponent<CuerpoCaido>()._animator = animator;
    }

    static bool TieneParametro(Animator a, int hash)
    {
        foreach (var p in a.parameters) if (p.nameHash == hash) return true;
        return false;
    }

    void Start() => _desde = Time.time;

    void Update()
    {
        if (_tumbado < 0f && _animator != null)
        {
            var e = _animator.GetCurrentAnimatorStateInfo(0);
            if (e.fullPathHash == EstadoMuerte && e.normalizedTime >= 1f && !_animator.IsInTransition(0)) _tumbado = Time.time;
        }
        if ((_tumbado >= 0f && Time.time - _tumbado >= SEGUNDOS_EN_EL_SUELO) || Time.time - _desde >= SEGUNDOS_MAXIMO)
            Destroy(gameObject);
    }
}
