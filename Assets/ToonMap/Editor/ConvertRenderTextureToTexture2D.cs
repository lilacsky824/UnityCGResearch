using UnityEngine;

public static class ConvertRenderTextureToTexture2D
{
    public static void Convert(RenderTexture renderTex, Texture2D Tex2D, bool directCopy)
    {
        if (directCopy)
        {
            Graphics.CopyTexture(renderTex, Tex2D);
        }
        else
        {

            RenderTexture oldActiveRenderTexture = RenderTexture.active;
            RenderTexture.active = renderTex;
            Tex2D.ReadPixels(new Rect(0, 0, Tex2D.width, Tex2D.height), 0, 0, false);
            Tex2D.Apply();
            RenderTexture.active = oldActiveRenderTexture;
        }
    }
}
