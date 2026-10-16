using BeachLife.Core;
using UnityEngine;

namespace BeachLife.Unity
{
    public enum PointerMode
    {
        Inspect,
        Build,
        Demolish,
    }

    /// <summary>Turns mouse input into build / demolish / inspect commands on the simulation.</summary>
    public sealed class BuildController : MonoBehaviour
    {
        public static BuildController Instance { get; private set; }

        public PointerMode Mode { get; private set; } = PointerMode.Inspect;
        public BuildingDef SelectedDef { get; private set; }
        public Building SelectedBuilding { get; private set; }
        public string LastMessage { get; private set; } = "";

        private Resort _resort;
        private Camera _camera;
        private GameObject _ghost;
        private readonly Plane _ground = new Plane(Vector3.up, Vector3.zero);

        private void Awake() => Instance = this;

        private void Start()
        {
            _resort = ResortRunner.Instance.Resort;
            _camera = Camera.main;
        }

        public void ChooseBuilding(BuildingDef def)
        {
            SelectedDef = def;
            Mode = PointerMode.Build;
            SelectedBuilding = null;
            RebuildGhost();
        }

        public void SetMode(PointerMode mode)
        {
            Mode = mode;
            if (mode != PointerMode.Build) SelectedDef = null;
            RebuildGhost();
        }

        private void Update()
        {
            if (ResortRunner.Instance != null && _resort != ResortRunner.Instance.Resort)
            {
                _resort = ResortRunner.Instance.Resort; // new game or loaded save
                SelectedBuilding = null;
                LastMessage = "";
            }

            if (Input.GetKeyDown(KeyCode.Escape)) SetMode(PointerMode.Inspect);
            if (Input.GetKeyDown(KeyCode.X)) SetMode(PointerMode.Demolish);

            if (!TryGetHoveredTile(out int tileX, out int tileY))
            {
                if (_ghost != null) _ghost.SetActive(false);
                return;
            }

            UpdateGhost(tileX, tileY);

            if (!Input.GetMouseButtonDown(0) || HudGui.PointerOverHud) return;

            switch (Mode)
            {
                case PointerMode.Build when SelectedDef != null:
                {
                    var origin = GhostOrigin(tileX, tileY, SelectedDef);
                    var result = _resort.TryBuild(SelectedDef, origin.X, origin.Y);
                    LastMessage = result.Success
                        ? $"Placed {SelectedDef.DisplayName}"
                        : Describe(result.Error);
                    break;
                }
                case PointerMode.Demolish:
                {
                    var building = _resort.BuildingAt(tileX, tileY);
                    LastMessage = building != null && _resort.Demolish(building.Id)
                        ? $"Demolished {building.Def.DisplayName}"
                        : "Nothing to demolish here";
                    break;
                }
                case PointerMode.Inspect:
                    SelectedBuilding = _resort.BuildingAt(tileX, tileY);
                    break;
            }
        }

        public bool TryGetHoveredTile(out int tileX, out int tileY)
        {
            tileX = tileY = -1;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return false;

            var ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (!_ground.Raycast(ray, out float distance)) return false;

            var point = ray.GetPoint(distance);
            tileX = Mathf.FloorToInt(point.x);
            tileY = Mathf.FloorToInt(point.z);
            return _resort.Map.InBounds(tileX, tileY);
        }

        /// <summary>Centres the footprint on the cursor.</summary>
        public static GridPos GhostOrigin(int tileX, int tileY, BuildingDef def) =>
            new GridPos(tileX - (def.Width - 1) / 2, tileY - (def.Height - 1) / 2);

        private void RebuildGhost()
        {
            if (_ghost != null) Destroy(_ghost);
            if (Mode != PointerMode.Build || SelectedDef == null) return;

            _ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ghost.name = "BuildGhost";
            Destroy(_ghost.GetComponent<Collider>());
            _ghost.transform.SetParent(transform, false);
        }

        private void UpdateGhost(int tileX, int tileY)
        {
            if (_ghost == null) return;
            var def = SelectedDef;
            var origin = GhostOrigin(tileX, tileY, def);
            bool valid = _resort.Map.CanPlace(origin.X, origin.Y, def.Width, def.Height)
                         && _resort.Economy.CanAfford(def.BuildCost)
                         && TerrainAllows(def, origin);

            _ghost.SetActive(true);
            _ghost.transform.position = new Vector3(
                origin.X + def.Width / 2f, 0.3f, origin.Y + def.Height / 2f);
            _ghost.transform.localScale = new Vector3(def.Width, 0.6f, def.Height);
            _ghost.GetComponent<Renderer>().sharedMaterial =
                Palette.Transparent(valid ? Color.green : Color.red, 0.35f);
        }

        private bool TerrainAllows(BuildingDef def, GridPos origin)
        {
            for (int dy = 0; dy < def.Height; dy++)
                for (int dx = 0; dx < def.Width; dx++)
                {
                    int x = origin.X + dx, y = origin.Y + dy;
                    if (!_resort.Map.InBounds(x, y)) return false;
                    if (!def.AllowsTile(_resort.Map.GetTile(x, y))) return false;
                }
            return true;
        }

        private static string Describe(BuildError error)
        {
            switch (error)
            {
                case BuildError.BlockedTiles: return "Something is already there";
                case BuildError.WrongTerrain: return "Wrong ground for this object";
                case BuildError.NotEnoughCash: return "Not enough cash";
                case BuildError.WouldTrapGuests: return "That would block guests in";
                case BuildError.UnknownBuilding: return "Unknown object";
                default: return "";
            }
        }
    }
}
