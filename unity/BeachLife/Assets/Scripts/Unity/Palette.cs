using System.Collections.Generic;
using BeachLife.Core;
using UnityEngine;

namespace BeachLife.Unity
{
    /// <summary>Colours and materials used by the placeholder art, so the game runs with zero imported assets.</summary>
    public static class Palette
    {
        private static readonly Dictionary<Color, Material> Cache = new Dictionary<Color, Material>();
        private static Shader _shader;

        public static Color Tile(TileType type)
        {
            switch (type)
            {
                case TileType.Water: return new Color(0.16f, 0.52f, 0.78f);
                case TileType.Sand: return new Color(0.94f, 0.87f, 0.66f);
                case TileType.Grass: return new Color(0.42f, 0.64f, 0.33f);
                case TileType.Promenade: return new Color(0.78f, 0.75f, 0.70f);
                default: return new Color(0.45f, 0.44f, 0.42f);
            }
        }

        public static Color Category(BuildingCategory category)
        {
            switch (category)
            {
                case BuildingCategory.Beach: return new Color(0.98f, 0.72f, 0.25f);
                case BuildingCategory.Food: return new Color(0.87f, 0.35f, 0.25f);
                case BuildingCategory.Drink: return new Color(0.30f, 0.68f, 0.72f);
                case BuildingCategory.Facility: return new Color(0.62f, 0.62f, 0.70f);
                case BuildingCategory.Entertainment: return new Color(0.62f, 0.35f, 0.78f);
                case BuildingCategory.Accommodation: return new Color(0.92f, 0.92f, 0.88f);
                default: return new Color(0.35f, 0.55f, 0.35f);
            }
        }

        public static Color Mood(float mood) =>
            Color.Lerp(new Color(0.85f, 0.2f, 0.2f), new Color(0.25f, 0.85f, 0.35f), Mathf.Clamp01(mood));

        public static Material Solid(Color color)
        {
            if (Cache.TryGetValue(color, out var cached) && cached != null) return cached;

            var material = new Material(ResolveShader()) { name = $"BeachLife/{color}" };
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.1f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.1f);
            Cache[color] = material;
            return material;
        }

        public static Material Transparent(Color color, float alpha)
        {
            var material = new Material(Solid(color));
            var faded = new Color(color.r, color.g, color.b, alpha);
            material.color = faded;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", faded);
            EnableTransparency(material);
            return material;
        }

        private static void EnableTransparency(Material material)
        {
            material.SetFloat("_Surface", 1f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private static Shader ResolveShader()
        {
            if (_shader != null) return _shader;
            _shader = Shader.Find("Universal Render Pipeline/Lit")
                      ?? Shader.Find("Standard")
                      ?? Shader.Find("Legacy Shaders/Diffuse")
                      ?? Shader.Find("Sprites/Default");
            return _shader;
        }
    }
}
