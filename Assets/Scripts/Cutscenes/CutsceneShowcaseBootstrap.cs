using UnityEngine;

public sealed class CutsceneShowcaseBootstrap : MonoBehaviour
{
    [SerializeField] private CutsceneRunner runner;
    [SerializeField] private CutsceneDefinition sequence;

    public void Configure(CutsceneRunner sequenceRunner, CutsceneDefinition sampleSequence)
    {
        runner = sequenceRunner;
        sequence = sampleSequence;
    }

    private void Start()
    {
        if (runner == null)
            runner = GetComponent<CutsceneRunner>();
        if (runner == null || sequence == null)
        {
            Debug.LogError("Cutscene showcase is missing its runner or sample definition.", this);
            return;
        }

        CutsceneCharacter player = CreateSampleActor("Player Actor", "플레이어",
            new Vector3(-2.2f, -0.3f, 0f), new Color32(109, 151, 192, 255));
        CutsceneCharacter boss = CreateSampleActor("Boss Actor", "보스",
            new Vector3(2.2f, -0.3f, 0f), new Color32(148, 86, 66, 255));

        runner.SetActorBindings(
            new CutsceneActorBinding("player", player),
            new CutsceneActorBinding("boss", boss));
        runner.Play(sequence, null, false);
    }

    private static CutsceneCharacter CreateSampleActor(string objectName, string label,
        Vector3 position, Color color)
    {
        GameObject actor = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        actor.name = objectName;
        actor.transform.position = position;
        Collider collider = actor.GetComponent<Collider>();
        if (collider != null)
            collider.enabled = false;

        Renderer renderer = actor.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = color;
            renderer.material.SetFloat("_Smoothness", 0f);
        }

        CutsceneCharacter character = actor.AddComponent<CutsceneCharacter>();
        character.Configure(label, null);
        return character;
    }
}
