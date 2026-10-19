using System.Collections.Generic;
using System.Linq;
using BeachLife.Core;
using UnityEngine;

namespace BeachLife.Unity
{
    /// <summary>
    /// Renders the simulation with code-only placeholder art: textured terrain quad, per-type
    /// primitive buildings, capsules for guests and staff, cubes for litter.
    /// Includes bounce animation for tourists, sway for palm trees, and emissive windows.
    /// </summary>
    [RequireComponent(typeof(ResortRunner))]
    public sealed class WorldView : MonoBehaviour
    {
        private ResortRunner _runner;
        private Resort _resort;
        private Transform _buildingRoot;
        private Transform _touristRoot;
        private Transform _staffRoot;
        private Transform _litterRoot;
        private readonly Dictionary<int, GameObject> _buildingViews = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, GameObject> _touristViews = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, GameObject> _staffViews = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, GameObject> _litterViews = new Dictionary<int, GameObject>();
        private readonly HashSet<int> _alive = new HashSet<int>();
        private float _litterTimer;
        private const int MaxLitterViews = 250;

        // Animation state.
        private readonly Dictionary<int, float> _touristBounce = new Dictionary<int, float>();
        private readonly List<GameObject> _palmCanopies = new List<GameObject>();
        private float _animTime;

        private void Start()
        {
            _runner = GetComponent<ResortRunner>();
            _resort = _runner.Resort;
            _buildingRoot = new GameObject("Buildings").transform;
            _touristRoot = new GameObject("Tourists").transform;
            _staffRoot = new GameObject("Staff").transform;
            _litterRoot = new GameObject("Litter").transform;
            foreach (var root in new[] { _buildingRoot, _touristRoot, _staffRoot, _litterRoot })
                root.SetParent(transform, false);
            CreateTerrain();
        }

        private void Update()
        {
            if (_resort != _runner.Resort) Rebind(); // a new game replaced the simulation
            SyncBuildings();
            SyncTourists();
            SyncStaff();
            SyncLitter();
            UpdateAnimations();
        }

        private void UpdateAnimations()
        {
            _animTime += Time.deltaTime * 6f;

            // Tourist bounce — copy keys to avoid modification during iteration.
            var bounceKeys = new List<int>(_touristBounce.Keys);
            for (int i = bounceKeys.Count - 1; i >= 0; i--)
            {
                var id = bounceKeys[i];
                if (!_touristViews.TryGetValue(id, out var view) || view == null)
                {
                    _touristBounce.Remove(id);
                    continue;
                }
                // Only bounce tourists that are moving (not inside buildings).
                var tourist = _resort.Tourists.FirstOrDefault(t => t.Id == id);
                if (tourist != null && (tourist.State == TouristState.Wandering || tourist.State == TouristState.WalkingToBuilding))
                {
                    const float baseBodyY = 0.35f; // must match the resting height set in SyncTourists
                    float bounce = Mathf.Abs(Mathf.Sin(_animTime + _touristBounce[id])) * 0.06f;
                    var body = view.transform.GetChild(0);
                    var pos = body.localPosition;
                    body.localPosition = new Vector3(pos.x, baseBodyY + bounce, pos.z);
                }
            }

            // Palm tree sway.
            for (int i = 0; i < _palmCanopies.Count; i++)
            {
                var canopy = _palmCanopies[i];
                if (canopy == null) continue;
                float sway = Mathf.Sin(_animTime * 0.8f + i * 1.7f) * 0.04f;
                canopy.transform.Rotate(sway, 0f, 0f, Space.Self);
            }
        }

        private void Rebind()
        {
            _resort = _runner.Resort;
            foreach (var view in _buildingViews.Values) if (view != null) Destroy(view);
            foreach (var view in _touristViews.Values) if (view != null) Destroy(view);
            foreach (var view in _staffViews.Values) if (view != null) Destroy(view);
            foreach (var view in _litterViews.Values) if (view != null) Destroy(view);
            _buildingViews.Clear();
            _touristViews.Clear();
            _staffViews.Clear();
            _litterViews.Clear();
            _touristBounce.Clear();
            _palmCanopies.Clear();
        }

        private void CreateTerrain()
        {
            var map = _resort.Map;
            var texture = new Texture2D(map.Width, map.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "Terrain",
            };
            // Add subtle noise to tile colors for a more natural look.
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                {
                    var baseColor = Palette.Tile(map.GetTile(x, y));
                    // Simple hash-based noise — deterministic per tile.
                    float n = (Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f) % 1f;
                    n = Mathf.Abs(n) * 0.06f; // small variation
                    baseColor.r = Mathf.Clamp01(baseColor.r + n - 0.03f);
                    baseColor.g = Mathf.Clamp01(baseColor.g + n - 0.03f);
                    baseColor.b = Mathf.Clamp01(baseColor.b + n - 0.03f);
                    texture.SetPixel(x, y, baseColor);
                }
            texture.Apply();

            var ground = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ground.name = "Terrain";
            ground.transform.SetParent(transform, false);
            ground.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            ground.transform.position = new Vector3(map.Width / 2f, -0.01f, map.Height / 2f);
            ground.transform.localScale = new Vector3(map.Width, map.Height, 1f);

            var material = Palette.Solid(Color.white);
            var instance = new Material(material) { mainTexture = texture };
            if (instance.HasProperty("_BaseMap")) instance.SetTexture("_BaseMap", texture);
            // Make terrain cast and receive shadows.
            var renderer = ground.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
            ground.GetComponent<Renderer>().sharedMaterial = instance;
        }

        private void SyncBuildings()
        {
            foreach (var pair in _resort.Buildings)
            {
                if (!_buildingViews.ContainsKey(pair.Key))
                    _buildingViews[pair.Key] = CreateBuildingView(pair.Value);
            }

            _alive.Clear();
            foreach (var id in _resort.Buildings.Keys) _alive.Add(id);
            RemoveMissing(_buildingViews, _alive);
        }

        private GameObject CreateBuildingView(Building building)
        {
            var def = building.Def;
            var view = new GameObject($"{def.DisplayName}#{building.Id}");
            view.transform.SetParent(_buildingRoot, false);
            view.transform.position = new Vector3(building.Origin.X + def.Width / 2f, 0f, building.Origin.Y + def.Height / 2f);

            var color = Palette.Category(def.Category);
            switch (def.Id)
            {
                case "sun_lounger":
                    Box(view, 0.9f, 0.12f, 0.9f, 0.06f, color);
                    Box(view, 0.8f, 0.08f, 0.3f, 0.16f, Color.Lerp(color, Color.white, 0.5f), z: -0.25f);
                    break;
                case "parasol":
                    Pole(view, 0.1f, 1.7f, new Color(0.55f, 0.4f, 0.3f));
                    Disc(view, 1.15f, 0.07f, color, 1.7f);
                    break;
                case "pool":
                    Box(view, def.Width * 0.94f, 0.22f, def.Height * 0.94f, 0.11f, new Color(0.85f, 0.9f, 0.9f));
                    Box(view, def.Width * 0.74f, 0.24f, def.Height * 0.74f, 0.12f, new Color(0.2f, 0.6f, 0.85f));
                    break;
                case "volleyball_court":
                    Box(view, def.Width * 0.95f, 0.1f, def.Height * 0.95f, 0.05f, new Color(0.93f, 0.8f, 0.55f));
                    Box(view, 0.08f, 0.9f, def.Height * 0.9f, 0.45f, Color.white);
                    break;
                case "litter_bin":
                    Cylinder(view, 0.35f, 0.6f, new Color(0.2f, 0.55f, 0.25f), 0.3f);
                    break;
                case "palm_tree":
                    Cylinder(view, 0.16f, 1.9f, new Color(0.45f, 0.33f, 0.2f), 0.95f);
                    // Canopy discs for sway animation — store references.
                    var c1 = Disc(view, 1.1f, 0.16f, new Color(0.25f, 0.62f, 0.28f), 1.95f);
                    var c2 = Disc(view, 0.8f, 0.14f, new Color(0.3f, 0.7f, 0.33f), 2.1f);
                    if (c1 != null) _palmCanopies.Add(c1);
                    if (c2 != null) _palmCanopies.Add(c2);
                    break;
                case "fountain":
                    Cylinder(view, def.Width * 0.85f, 0.35f, new Color(0.8f, 0.82f, 0.85f), 0.17f);
                    Cylinder(view, def.Width * 0.6f, 0.36f, new Color(0.25f, 0.62f, 0.85f), 0.2f);
                    Cylinder(view, 0.3f, 0.9f, new Color(0.8f, 0.82f, 0.85f), 0.45f);
                    break;
                case "lifeguard_tower":
                    Box(view, 0.25f, 1.2f, 0.25f, 0.6f, new Color(0.75f, 0.7f, 0.6f));
                    Box(view, 0.9f, 0.8f, 0.9f, 1.6f, new Color(0.95f, 0.35f, 0.3f));
                    Box(view, 1.0f, 0.08f, 1.0f, 2.05f, color);
                    break;
                case "ice_cream_stand":
                    Box(view, def.Width * 0.85f, 1.2f, def.Height * 0.85f, 0.6f, color);
                    Disc(view, 0.9f, 0.35f, Color.white, 1.35f);
                    break;
                default:
                {
                    // Generic venue: body whose height scales with importance, plus a roof and windows.
                    float height = Mathf.Clamp(0.9f + def.Capacity * 0.1f, 0.9f, 3.2f);
                    Box(view, def.Width * 0.92f, height, def.Height * 0.92f, height / 2f, color);
                    // Roof overhang.
                    Box(view, def.Width * 1.04f, 0.18f, def.Height * 1.04f, height + 0.09f,
                        Color.Lerp(color, Color.black, 0.35f));
                    // Emissive windows (small bright panels).
                    AddWindows(view, def.Width, def.Height, height, color);
                    break;
                }
            }
            return view;
        }

        /// <summary>Shared warm-glow material for windows; kept separate so nothing else glows.</summary>
        private static Material _windowMaterial;

        /// <summary>Add small emissive panels as windows on a generic building.</summary>
        private static void AddWindows(GameObject parent, float bw, float bh, float height, Color baseColor)
        {
            if (_windowMaterial == null)
            {
                // Clone, do not mutate: Palette.Solid(Color.white) is cached and shared.
                _windowMaterial = new Material(Palette.Solid(Color.white));
                _windowMaterial.EnableKeyword("_EMISSION");
                if (_windowMaterial.HasProperty("_EmissionColor"))
                    _windowMaterial.SetColor("_EmissionColor", new Color(1f, 0.9f, 0.6f, 1f));
            }

            // Two windows on the front face.
            float windowW = 0.15f;
            float windowH = 0.2f;
            float windowY = height * 0.35f;
            float spacing = bw * 0.25f;
            for (int i = -1; i <= 1; i += 2)
            {
                var win = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(win.GetComponent<Collider>());
                win.transform.SetParent(parent.transform, false);
                win.transform.localScale = new Vector3(windowW, windowH, 0.05f);
                win.transform.localPosition = new Vector3(i * spacing, windowY, bh / 2f + 0.01f);
                var winRenderer = win.GetComponent<Renderer>();
                winRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                winRenderer.sharedMaterial = _windowMaterial;
            }
        }

        private static void Box(GameObject parent, float sx, float sy, float sz, float y, Color color, float z = 0f, float x = 0f)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(box.GetComponent<Collider>());
            box.transform.SetParent(parent.transform, false);
            box.transform.localScale = new Vector3(sx, sy, sz);
            box.transform.localPosition = new Vector3(x, y, z);
            var renderer = box.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.sharedMaterial = Palette.Solid(color);
        }

        private static void Pole(GameObject parent, float diameter, float height, Color color)
        {
            Cylinder(parent, diameter, height, color, height / 2f);
        }

        private static GameObject Cylinder(GameObject parent, float diameter, float height, Color color, float y)
        {
            var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(cyl.GetComponent<Collider>());
            cyl.transform.SetParent(parent.transform, false);
            cyl.transform.localScale = new Vector3(diameter, height / 2f, diameter);
            cyl.transform.localPosition = new Vector3(0f, y, 0f);
            var renderer = cyl.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.sharedMaterial = Palette.Solid(color);
            return cyl;
        }

        private static GameObject Disc(GameObject parent, float diameter, float thickness, Color color, float y)
        {
            return Cylinder(parent, diameter, thickness, color, y);
        }

        private void SyncTourists()
        {
            _alive.Clear();
            foreach (var tourist in _resort.Tourists)
            {
                _alive.Add(tourist.Id);
                if (!_touristViews.TryGetValue(tourist.Id, out var view) || view == null)
                {
                    // Group object for body + head.
                    var group = new GameObject($"{tourist.Name} ({tourist.GuestType})");
                    group.transform.SetParent(_touristRoot, false);
                    _touristViews[tourist.Id] = group;

                    // Body — capsule.
                    var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    Object.Destroy(body.GetComponent<Collider>());
                    body.transform.SetParent(group.transform, false);
                    float scale = tourist.GuestType == GuestType.Vip ? 0.42f : tourist.GuestType == GuestType.Budget ? 0.3f : 0.35f;
                    body.transform.localScale = new Vector3(scale, scale, scale);
                    var bodyRenderer = body.GetComponent<Renderer>();
                    bodyRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    bodyRenderer.receiveShadows = true;

                    // Head — small sphere.
                    var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Object.Destroy(head.GetComponent<Collider>());
                    head.transform.SetParent(group.transform, false);
                    float headScale = scale * 0.55f;
                    head.transform.localScale = new Vector3(headScale, headScale, headScale);
                    head.transform.localPosition = new Vector3(0f, scale * 0.7f, 0f);
                    var headRenderer = head.GetComponent<Renderer>();
                    headRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    headRenderer.receiveShadows = true;

                    // Store body reference for animation.
                    body.tag = group.tag; // preserve tag
                    group.SetActive(false); // disable group visibility

                    // Bounce offset — random phase per tourist.
                    _touristBounce[tourist.Id] = tourist.Id * 0.73f;
                }

                bool hidden = tourist.State == TouristState.UsingBuilding;
                var gv = _touristViews[tourist.Id];
                if (gv != null)
                {
                    // Toggle visibility of the group.
                    gv.SetActive(!hidden);
                    if (hidden) continue;

                    // Position the group at the tourist's location.
                    gv.transform.position = ResortRunner.WorldPos(tourist.X, tourist.Y, 0f);

                    var body = gv.transform.GetChild(0);
                    var head = gv.transform.GetChild(1);
                    // Base Y offset so feet sit on the ground.
                    body.localPosition = new Vector3(0f, 0.35f, 0f);
                    head.localPosition = new Vector3(0f, gv.transform.localScale.y * 0.7f, 0f);

                    // Color by mood.
                    Color moodColor = Palette.Mood(tourist.Mood);
                    body.GetComponent<Renderer>().sharedMaterial = Palette.Solid(moodColor);
                    head.GetComponent<Renderer>().sharedMaterial = Palette.Solid(moodColor);
                }
            }
            RemoveMissing(_touristViews, _alive);
        }

        private void SyncStaff()
        {
            _alive.Clear();
            foreach (var member in _resort.StaffMembers)
            {
                _alive.Add(member.Id);
                if (!_staffViews.TryGetValue(member.Id, out var view) || view == null)
                {
                    view = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    view.name = member.Name;
                    view.transform.SetParent(_staffRoot, false);
                    view.transform.localScale = new Vector3(0.38f, 0.38f, 0.38f);
                    Destroy(view.GetComponent<Collider>());
                    var staffRenderer = view.GetComponent<Renderer>();
                    staffRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    staffRenderer.receiveShadows = true;
                    _staffViews[member.Id] = view;
                }

                var color = member.Type == StaffType.Cleaner
                    ? new Color(0.3f, 0.5f, 0.95f)
                    : new Color(0.95f, 0.8f, 0.2f);
                view.transform.position = ResortRunner.WorldPos(member.X, member.Y, 0.38f);
                view.GetComponent<Renderer>().sharedMaterial = Palette.Solid(color);
            }
            RemoveMissing(_staffViews, _alive);
        }

        private void SyncLitter()
        {
            _litterTimer -= Time.deltaTime;
            if (_litterTimer > 0f) return;
            _litterTimer = 0.3f;

            _alive.Clear();
            int shown = 0;
            var map = _resort.Map;
            for (int i = 0; i < _resort.Litter.Length && shown < MaxLitterViews; i++)
            {
                float amount = _resort.Litter[i];
                if (amount < 0.1f) continue;
                _alive.Add(i);
                shown++;
                int x = i % map.Width, y = i / map.Width;
                if (!_litterViews.TryGetValue(i, out var view) || view == null)
                {
                    view = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    view.name = $"Litter {x},{y}";
                    view.transform.SetParent(_litterRoot, false);
                    Destroy(view.GetComponent<Collider>());
                    float jitterX = ((x * 31 + y * 17) % 10) / 20f - 0.25f;
                    float jitterZ = ((x * 13 + y * 41) % 10) / 20f - 0.25f;
                    view.transform.position = new Vector3(x + 0.5f + jitterX, 0.05f, y + 0.5f + jitterZ);
                    _litterViews[i] = view;
                }
                float s = Mathf.Clamp(0.1f + amount * 0.06f, 0.1f, 0.3f);
                view.transform.localScale = new Vector3(s, s * 0.6f, s);
                view.GetComponent<Renderer>().sharedMaterial =
                    Palette.Solid(Color.Lerp(new Color(0.72f, 0.65f, 0.4f), new Color(0.4f, 0.3f, 0.2f), amount / 3f));
            }
            RemoveMissing(_litterViews, _alive);
        }

        private void RemoveMissing(Dictionary<int, GameObject> views, HashSet<int> aliveIds)
        {
            List<int> missing = null;
            foreach (var id in views.Keys)
            {
                if (aliveIds.Contains(id)) continue;
                missing ??= new List<int>();
                missing.Add(id);
            }
            if (missing == null) return;
            foreach (var id in missing)
            {
                if (views[id] != null) Destroy(views[id]);
                views.Remove(id);
            }
        }
    }
}
