using UnityEngine;

public class JumpLandSMB : StateMachineBehaviour
{
    public override void OnStateExit(Animator animator, AnimatorStateInfo info, int layerIndex)
    {
        animator.SetBool("IsLanding", false);
        animator.SetBool("IsGrounded", true);
    }
}