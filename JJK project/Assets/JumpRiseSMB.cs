using UnityEngine;

public class JumpRiseSMB : StateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo info, int layerIndex)
    {
        animator.SetBool("IsJumping", false);
    }
}