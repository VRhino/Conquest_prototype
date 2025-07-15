using UnityEngine;

public class ForceGrounded : MonoBehaviour
{
    public Animator animator;
    private int frameCount = 0;
    private const int forceFrames = 5; // Forzar por unos pocos frames

    void Start()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (animator == null)
        {
            Debug.LogWarning("Animator no asignado.");
            return;
        }
        animator.SetBool("IsGrounded", true);
    }
}
