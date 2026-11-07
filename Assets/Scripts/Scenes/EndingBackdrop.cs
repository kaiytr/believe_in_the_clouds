using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scrolling sunset backdrop of the ending, built from the authored layer sprites: sky, three clouds, back
/// and front mountains. Each layer is a seamless strip that is tiled and scrolls at its own speed while the
/// ground moves (parallax), so far layers move slowly and near layers faster. Clouds also drift by themselves.
/// Everything is unlit so the sunset colours stay as drawn.
/// </summary>
public sealed class EndingBackdrop : MonoBehaviour
{
    [System.Serializable]
    public struct Art
    {
        public Sprite sky, mountainBack, mountainFront;
        public Sprite[] clouds;
    }

    private sealed class Layer
    {
        public Transform[] Copies;
        public float Period, Factor, Drift, Offset;
    }

    private readonly List<Layer> layers = new List<Layer>();

    /// <param name="canvasBottomY">World height of the bottom edge of the layer art. The art is a full 1080px-tall canvas
    /// (one screen tall at the ending camera), so this decides how much of the sky and sun shows above the ground.</param>
    public static EndingBackdrop Build(Transform parent, float canvasBottomY, Art art)
    {
        var root = new GameObject("Sunset backdrop");
        root.transform.SetParent(parent, false);
        var backdrop = root.AddComponent<EndingBackdrop>();
        // Back to front: sky, clouds (far to near), back mountains, front mountains.
        backdrop.AddLayer("Sky", art.sky, canvasBottomY, -100, .015f, 0f);
        if (art.clouds != null)
        {
            // Different pace per cloud so they do not move as one sheet.
            float[] factor = { .05f, .075f, .035f };
            float[] drift = { .10f, .06f, .14f };
            for (int i = 0; i < art.clouds.Length; i++)
                backdrop.AddLayer("Cloud " + (i + 1), art.clouds[i], canvasBottomY, -96 + i, factor[i % 3], drift[i % 3]);
        }
        backdrop.AddLayer("Mountains back", art.mountainBack, canvasBottomY, -90, .10f, 0f);
        backdrop.AddLayer("Mountains front", art.mountainFront, canvasBottomY, -80, .22f, 0f);
        return backdrop;
    }

    /// <summary>Moves the layers by the distance the ground travelled this frame (plus their own drift).</summary>
    public void Scroll(float distance, float deltaTime)
    {
        foreach (Layer layer in layers)
        {
            layer.Offset = Mathf.Repeat(layer.Offset + distance * layer.Factor + layer.Drift * deltaTime, layer.Period);
            for (int i = 0; i < layer.Copies.Length; i++)
            {
                Vector3 p = layer.Copies[i].localPosition;
                p.x = -layer.Offset + (i - 1) * layer.Period;
                layer.Copies[i].localPosition = p;
            }
        }
    }

    private void AddLayer(string layerName, Sprite sprite, float canvasBottomY, int order, float factor, float drift)
    {
        if (sprite == null) { Debug.LogError("Ending backdrop sprite is missing: " + layerName, this); return; }
        float period = sprite.bounds.size.x;
        var layer = new Layer { Period = period, Factor = factor, Drift = drift, Copies = new Transform[3] };
        for (int i = 0; i < 3; i++)
        {
            var go = new GameObject(layerName + " " + i);
            go.transform.SetParent(transform, false);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            AtmosphereMaterials.ApplyUnlit(renderer);
            // The sprite pivot is its centre: put its bottom edge on canvasBottomY.
            go.transform.localPosition = new Vector3((i - 1) * period, canvasBottomY + sprite.bounds.size.y * .5f, 0f);
            layer.Copies[i] = go.transform;
        }
        layers.Add(layer);
    }
}
