#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

public static class DecalGenerator
{
    [MenuItem("The Chimera Protocol/Generate Decals")]
    public static void GenerateAllDecals()
    {
        string decalDir = "Assets/_Project/Environments/Level1_Admin/Decals";
        string fullDir = Path.Combine(Application.dataPath, "_Project/Environments/Level1_Admin/Decals");
        Directory.CreateDirectory(fullDir);

        File.WriteAllBytes(Path.Combine(fullDir, "T_Decal_BloodPool.png"), CreateBloodPoolTex().EncodeToPNG());
        File.WriteAllBytes(Path.Combine(fullDir, "T_Decal_BloodSplatter.png"), CreateBloodSplatterTex().EncodeToPNG());
        File.WriteAllBytes(Path.Combine(fullDir, "T_Decal_HazardStripe.png"), CreateHazardStripeTex().EncodeToPNG());

        AssetDatabase.Refresh();

        var decalShader = Shader.Find("Shader Graphs/Decal");
        if (decalShader != null)
        {
            var tPool = AssetDatabase.LoadAssetAtPath<Texture2D>(decalDir + "/T_Decal_BloodPool.png");
            var tSplat = AssetDatabase.LoadAssetAtPath<Texture2D>(decalDir + "/T_Decal_BloodSplatter.png");
            var tHazard = AssetDatabase.LoadAssetAtPath<Texture2D>(decalDir + "/T_Decal_HazardStripe.png");

            CreateDecalMaterial(decalShader, tPool, decalDir + "/M_Decal_BloodPool.mat", 0.95f);
            CreateDecalMaterial(decalShader, tSplat, decalDir + "/M_Decal_BloodSplatter.mat", 0.9f);
            CreateDecalMaterial(decalShader, tHazard, decalDir + "/M_Decal_HazardStripe.mat", 0.3f);

            AssetDatabase.SaveAssets();
        }
        Debug.Log("Decals successfully generated!");
    }

    private static void CreateDecalMaterial(Shader shader, Texture2D tex, string path, float smoothness)
    {
        var mat = new Material(shader);
        mat.SetTexture("_BaseMap", tex);
        mat.SetFloat("_Smoothness", smoothness);
        AssetDatabase.CreateAsset(mat, path);
    }

    private static Texture2D CreateBloodPoolTex()
    {
        int size = 512;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        Color darkBlood = new Color(0.20f, 0.01f, 0.01f, 0.96f);
        Color freshBlood = new Color(0.40f, 0.02f, 0.02f, 0.92f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (float)x / size - 0.5f;
                float ny = (float)y / size - 0.5f;
                float dist = Mathf.Sqrt(nx * nx + ny * ny) * 2.0f;

                float noise = Mathf.PerlinNoise(nx * 4.0f + 5.0f, ny * 4.0f + 5.0f) * 0.35f;
                noise += Mathf.PerlinNoise(nx * 8.0f + 10.0f, ny * 8.0f + 10.0f) * 0.15f;
                float threshold = 0.55f + noise;

                if (dist < threshold)
                {
                    float t = dist / threshold;
                    Color col = Color.Lerp(darkBlood, freshBlood, 1.0f - t);
                    col.a = Mathf.Clamp01((threshold - dist) * 12.0f);
                    tex.SetPixel(x, y, col);
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }
        tex.Apply();
        return tex;
    }

    private static Texture2D CreateBloodSplatterTex()
    {
        int size = 512;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        for (int i = 0; i < size; i++)
            for (int j = 0; j < size; j++)
                tex.SetPixel(i, j, Color.clear);

        Color blood = new Color(0.32f, 0.015f, 0.015f, 0.95f);
        System.Random rand = new System.Random(1337);

        for (int i = 0; i < 40; i++)
        {
            int cx = 256 + rand.Next(-35, 35);
            int cy = 256 + rand.Next(-35, 35);
            int radius = rand.Next(8, 28);
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                for (int y = cy - radius; y <= cy + radius; y++)
                {
                    if (x >= 0 && x < size && y >= 0 && y < size)
                    {
                        if (Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) <= radius)
                            tex.SetPixel(x, y, blood);
                    }
                }
            }
        }

        for (int d = 0; d < 120; d++)
        {
            float angle = (float)(rand.NextDouble() * Mathf.PI * 2.0f);
            float dist = (float)(rand.Next(35, 230));
            int dx = (int)(256 + Mathf.Cos(angle) * dist);
            int dy = (int)(256 + Mathf.Sin(angle) * dist);
            int r = rand.Next(1, 6);
            for (int x = dx - r; x <= dx + r; x++)
            {
                for (int y = dy - r; y <= dy + r; y++)
                {
                    if (x >= 0 && x < size && y >= 0 && y < size)
                    {
                        if (Vector2.Distance(new Vector2(x, y), new Vector2(dx, dy)) <= r)
                            tex.SetPixel(x, y, blood);
                    }
                }
            }
        }
        tex.Apply();
        return tex;
    }

    private static Texture2D CreateHazardStripeTex()
    {
        int size = 512;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        Color yellow = new Color(0.85f, 0.70f, 0.05f, 0.95f);
        Color black = new Color(0.08f, 0.08f, 0.08f, 0.95f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int stripe = (x + y) / 48 % 2;
                Color c = stripe == 0 ? yellow : black;
                float edgeNoise = Mathf.PerlinNoise(x * 0.1f, y * 0.1f);
                if (edgeNoise > 0.85f) c *= 0.6f;
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return tex;
    }
}
#endif
