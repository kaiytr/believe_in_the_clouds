using System;
using UnityEngine;

public static class DeathScreenCapture
{
    // Synchronous capture: no end-of-frame wait after objects/cameras can change,
    // and no result canvas exists yet. Capture the gameplay camera, not desktop UI.
    // Frame height relative to the player's body: the player fills roughly 45%
    // of the image, instead of appearing small in the full gameplay viewport.
    private const float PlayerFrameHeight = 2.2f;
    private const float CloseUpAspect = 16f / 9f;

    public static Texture2D Capture(Camera camera, out string error, PlayerHealth player = null)
    {
        error = null;
        if (camera == null) { error = "플레이 카메라를 찾을 수 없습니다."; return null; }
        RenderTexture target = null;
        Texture2D image = null;
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        Vector3 previousPosition = camera.transform.position;
        float previousSize = camera.orthographicSize;
        float previousFieldOfView = camera.fieldOfView;
        try
        {
            float aspect = Mathf.Max(.1f, camera.aspect);
            if (player != null)
            {
                Bounds bounds = player.BodyCollider != null ? player.BodyCollider.bounds :
                    new Bounds(player.transform.position, new Vector3(1f, 2f, 0f));
                // Leave enough height for a centered widescreen crop even when the
                // gameplay viewport is portrait or 4:3. Never change camera.aspect,
                // so automatic aspect handling survives subsequent scene resizing.
                float frameHeight = Mathf.Max(.5f, bounds.size.y * PlayerFrameHeight)
                    * Mathf.Max(1f, CloseUpAspect / aspect);
                Vector3 offset = bounds.center - camera.transform.position;
                // Center in the camera's plane, keeping its gameplay depth/rotation.
                camera.transform.position += camera.transform.right * Vector3.Dot(offset, camera.transform.right)
                    + camera.transform.up * Vector3.Dot(offset, camera.transform.up);
                if (camera.orthographic)
                    camera.orthographicSize = frameHeight * .5f;
                else
                {
                    float depth = Mathf.Max(.1f, Vector3.Dot(offset, camera.transform.forward));
                    float fieldOfView = Mathf.Clamp(2f * Mathf.Atan(frameHeight * .5f / depth) * Mathf.Rad2Deg, 1f, 179f);
                    camera.fieldOfView = fieldOfView;
                }
            }
            int width = Mathf.Clamp(camera.pixelWidth, 1, 960);
            int height = Mathf.Max(1, Mathf.RoundToInt(width / aspect));
            if (height > 960)
            {
                height = 960;
                width = Mathf.Max(1, Mathf.RoundToInt(height * aspect));
            }
            target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            int imageWidth = width, imageHeight = height;
            if (player != null)
            {
                if ((float)width / height > CloseUpAspect) imageWidth = Mathf.Max(1, Mathf.RoundToInt(height * CloseUpAspect));
                else imageHeight = Mathf.Max(1, Mathf.RoundToInt(width / CloseUpAspect));
            }
            image = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false) { name = "Death capture", filterMode = FilterMode.Point };
            image.ReadPixels(new Rect((width - imageWidth) / 2, (height - imageHeight) / 2, imageWidth, imageHeight), 0, 0);
            image.Apply(false, false);
            return image;
        }
        catch (Exception ex)
        {
            if (image != null) UnityEngine.Object.Destroy(image);
            error = "화면 캡처를 사용할 수 없습니다: " + ex.Message;
            return null;
        }
        finally
        {
            camera.targetTexture = previousTarget;
            camera.transform.position = previousPosition;
            camera.orthographicSize = previousSize;
            camera.fieldOfView = previousFieldOfView;
            RenderTexture.active = previousActive;
            if (target != null) RenderTexture.ReleaseTemporary(target);
        }
    }
}
