using UnityEngine;

public class BeliefTestSceneController : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private BeliefTestDummy dummy;
    [SerializeField] private BeliefTestDriver driver;

    private void Start()
    {
        if (player == null || dummy == null || driver == null)
        {
            Debug.LogError("Belief test scene references are incomplete.", this);
            return;
        }

        BeliefController belief = dummy.GetComponent<BeliefController>();
        if (belief == null)
        {
            Debug.LogError("Belief test dummy has no BeliefController.", dummy);
            return;
        }

        BeliefTestAttack attack = dummy.GetComponent<BeliefTestAttack>();
        RearPositionBeliefCondition rear =
            dummy.GetComponent<RearPositionBeliefCondition>();
        DodgeAttackBeliefCondition dodge =
            dummy.GetComponent<DodgeAttackBeliefCondition>();
        SequentialFlankAttackBeliefCondition flank =
            dummy.GetComponent<SequentialFlankAttackBeliefCondition>();

        if (attack == null || rear == null || dodge == null || flank == null)
        {
            Debug.LogError("Belief test dummy is missing an attack or belief condition.", dummy);
            return;
        }

        EnemyTestPlayerMove oldMovement =
            player.GetComponent<EnemyTestPlayerMove>();
        if (oldMovement != null)
            oldMovement.enabled = false;

        player.position = new Vector3(3.5f, -1.5f, 0f);
        driver.Configure(
            player,
            dummy,
            belief,
            rear,
            dodge,
            flank
        );
    }
}
