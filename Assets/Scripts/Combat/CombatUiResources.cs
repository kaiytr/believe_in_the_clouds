using System.Collections.Generic;
using UnityEngine;

public static class CombatUiResources
{
    private static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
    private static HashSet<string> installedFonts;
    private static CombatantHudView hudPrefab;
    public static CombatantHudView HudPrefab => hudPrefab != null ? hudPrefab :
        hudPrefab = Resources.Load<CombatantHudView>("Combat/CombatantHud");

    public static Font KoreanFont(string preferred = "Malgun Gothic")
    {
        preferred = string.IsNullOrEmpty(preferred) ? "Malgun Gothic" : preferred;
        if (fonts.TryGetValue(preferred, out Font cached)) return cached;
        if (installedFonts == null) installedFonts = new HashSet<string>(Font.GetOSInstalledFontNames());
        foreach (string family in new[] { preferred, "맑은 고딕", "Noto Sans CJK KR", "Apple SD Gothic Neo" })
        {
            if (!installedFonts.Contains(family)) continue;
            Font font = Font.CreateDynamicFontFromOSFont(family, 19);
            if (font != null && font.HasCharacter('너')) { fonts[preferred] = font; return font; }
            if (font != null) Release(font);
        }
        fonts[preferred] = null;
        return null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        foreach (Font font in fonts.Values) if (font != null) Release(font);
        fonts.Clear(); installedFonts = null; hudPrefab = null;
        Application.quitting -= Reset;
        Application.quitting += Reset;
    }
    private static void Release(Object value)
    { if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value); }
}
