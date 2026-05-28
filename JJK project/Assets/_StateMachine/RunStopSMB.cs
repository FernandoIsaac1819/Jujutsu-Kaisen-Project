using UnityEngine;

public class RunStopSMB : StateMachineBehaviour
{
    public override void OnStateExit(Animator animator, AnimatorStateInfo info, int layerIndex)
    {
        animator.SetBool("IsRunStopping", false);
    }
}