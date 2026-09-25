using System.Collections.Generic;
using BeachLife.Core;
using UnityEngine;

namespace BeachLife.Unity
{
    /// <summary>
    /// Renders the simulation with placeholder primitives: the terrain becomes a single textured
    /// quad, buildings become coloured boxes and guests become capsules tinted by mood.
    /// </summary>
    [RequireComponent(typeof(ResortRunner))]
    public sealed class WorldView : MonoBehaviour
    {
        private Resort _resort;
        private Transform _buildingRoot;
        private Transform _touristRoot;
        private readonly Dictionary<int, GameObject> _buildingViews = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, GameObject> _touristViews = new Dictionary<int, GameObject>();
        private readonly List<int> _scratch = new List<int>();

        private void Start()
        {
            _resort = GetComponent<ResortRunner>().Resort;
            _buildingRoot = new GameObject("Buildings").transform;
            _touristRoot = new GameObject("Tourists").transform;
            _buildingRoot.SetParent(transform, false);
            _touristRoot.SetParent(transform, false);
            CreateTerrain();
        }

        private void Update()
        {
            SyncBuildings();
            SyncTourists();
        }

        private void CreateTerrain()
        {
            var map = _resort.Map;
            var texture = new Texture2D(map.Width, map.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Terrain",
            };
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                    texture.SetPixel(x, y, Palette.Tile(map.GetTile(x, y)));
            texture.Apply();

            var ground = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ground.name = "Terrain";
            ground.transform.SetParent(transform, false);
            ground.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            ground.transform.position = new Vector3(map.Width / 2f, 0f, map.Height / 2f);
            ground.transform.localScale = new Vector3(map.Width, map.Height, 1f);

            var material = Palette.Solid(Color.white);
            var instance = new Material(material) { mainTexture = texture };
            if (instance.HasProperty("_BaseMap")) instance.SetTexture("_BaseMap", texture);
            ground.GetComponent<Renderer>().sharedMaterial = instance;
            ground.layer = LayerMask.NameToLayer("Default");
        }

        private void SyncBuildings()
        {
            foreach (var pair in _resort.Buildings)
            {
                if (_buildingViews.ContainsKey(pair.Key)) continue;
                _buildingViews[pair.Key] = CreateBuildingView(pair.Value);
            }

            _scratch.Clear();
            foreach (var id in _buildingViews.Keys)
                if (!_resort.Buildings.ContainsKey(id)) _scratch.Add(id);
            foreach (var id in _scratch)
            {
                Destroy(_buildingViews[id]);
                _buildingViews.Remove(id);
            }
        }

        private GameObject CreateBuildingView(Building building)
        {
            var def = building.Def;
            float height = Mathf.Clamp(0.4f + def.Capacity * 0.12f, 0.4f, 3.5f);
            var view = GameObject.CreatePrimitive(PrimitiveType.Cube);
            view.name = $"{def.DisplayName}#{building.Id}";
            view.transform.SetParent(_buildingRoot, false);
            view.transform.position = new Vector3(
                building.Origin.X + def.Width / 2f,
                height / 2f,
                building.Origin.Y + def.Height / 2f);
            view.transform.localScale = new Vector3(def.Width * 0.92f, height, def.Height * 0.92f);
            view.GetComponent<Renderer>().sharedMaterial = Palette.Solid(Palette.Category(def.Category));
            return view;
        }

        private void SyncTourists()
        {
            foreach (var tourist in _resort.Tourists)
            {
                if (!_touristViews.TryGetValue(tourist.Id, out var view) || view == null)
                {
                    view = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    view.name = tourist.Name;
                    view.transform.SetParent(_touristRoot, false);
                    view.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
                    Destroy(view.GetComponent<Collider>());
                    _touristViews[tourist.Id] = view;
                }

                bool hidden = tourist.State == TouristState.UsingBuilding;
                if (view.activeSelf == hidden) view.SetActive(!hidden);
                if (hidden) continue;

                view.transform.position = ResortRunner.WorldPos(tourist.X, tourist.Y, 0.35f);
                view.GetComponent<Renderer>().sharedMaterial = Palette.Solid(Palette.Mood(tourist.Mood));
            }

            _scratch.Clear();
            foreach (var id in _touristViews.Keys)
            {
                bool alive = false;
                foreach (var tourist in _resort.Tourists)
                {
                    if (tourist.Id != id) continue;
                    alive = true;
                    break;
                }
                if (!alive) _scratch.Add(id);
            }
            foreach (var id in _scratch)
            {
                if (_touristViews[id] != null) Destroy(_touristViews[id]);
                _touristViews.Remove(id);
            }
        }
    }
}
