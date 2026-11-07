using UnityEngine;

/// <summary>
/// Boss stage audio. The rain loop plays from the moment the stage is entered. The battle music joins it
/// (both play together) as soon as the fight begins, i.e. after the intro conversation; it fades out when the
/// boss or the player dies. The rain keeps going.
/// </summary>
public sealed class BossStageAudio : MonoBehaviour
{
    [SerializeField] private AudioClip rainLoop;
    [SerializeField, Range(0f, 1f)] private float rainVolume = .5f;
    [SerializeField] private AudioClip battleBgm;
    [SerializeField, Range(0f, 1f)] private float battleVolume = .6f;
    [SerializeField, Min(.1f)] private float fadeSeconds = 1.5f;

    private AudioSource rain, battle;
    private BossCombatController boss;
    private PlayerHealth player;

    private void Start()
    {
        boss = FindFirstObjectByType<BossCombatController>();
        player = FindFirstObjectByType<PlayerHealth>();
        rain = CreateSource("Rain loop", rainLoop, rainVolume);
        if (rain != null) rain.Play();
        battle = CreateSource("Battle music", battleBgm, 0f);
    }

    private AudioSource CreateSource(string sourceName, AudioClip clip, float volume)
    {
        if (clip == null) return null;
        var holder = new GameObject(sourceName);
        holder.transform.SetParent(transform, false);
        AudioSource source = holder.AddComponent<AudioSource>();
        source.clip = clip; source.loop = true; source.playOnAwake = false;
        source.spatialBlend = 0f; source.volume = volume;
        return source;
    }

    private void Update()
    {
        if (battle == null) return;
        bool fighting = boss != null && boss.EncounterStarted && !boss.IsDead && (player == null || !player.IsDead);
        if (fighting && !battle.isPlaying) battle.Play();
        float target = fighting ? battleVolume : 0f;
        battle.volume = Mathf.MoveTowards(battle.volume, target, battleVolume / fadeSeconds * Time.unscaledDeltaTime);
        if (!fighting && battle.isPlaying && battle.volume <= 0f) battle.Stop();
    }
}
