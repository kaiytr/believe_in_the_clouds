public interface ISlowable
{
    void ApplySlow(
        float speedMultiplier,
        float duration
    );
}