using BeachLife.Core;
using UnityEngine;

namespace BeachLife.Unity
{
    /// <summary>Rain particles and lighting tint driven by the simulation's weather.</summary>
    public sealed class WeatherFx : MonoBehaviour
    {
        private Resort _resort;
        private ParticleSystem _rain;
        private Light _sun;
        private WeatherType _applied = (WeatherType)(-1);

        public void Bind(Resort resort) => _resort = resort;

        private void Start()
        {
            _sun = FindObjectOfType<Light>();
            _resort = _resort ?? (ResortRunner.Instance != null ? ResortRunner.Instance.Resort : null);
        }

        private void Update()
        {
            if (_resort == null) return;
            if (_applied != _resort.CurrentWeather) Apply(_resort.CurrentWeather);
        }

        private void Apply(WeatherType weather)
        {
            _applied = weather;
            var info = WeatherTable.Get(weather);

            if (_sun != null)
            {
                _sun.intensity = info.SunIntensity;
                _sun.color = weather == WeatherType.Rainy
                    ? new Color(0.75f, 0.78f, 0.85f)
                    : weather == WeatherType.Cloudy
                        ? new Color(0.9f, 0.92f, 0.95f)
                        : new Color(1f, 0.97f, 0.88f);
            }
            RenderSettings.ambientLight = weather == WeatherType.Rainy
                ? new Color(0.4f, 0.43f, 0.5f)
                : new Color(0.55f, 0.57f, 0.6f) * Mathf.Lerp(0.8f, 1.05f, info.SunIntensity);

            if (_rain == null) _rain = CreateRain();
            var emission = _rain.emission;
            emission.enabled = weather == WeatherType.Rainy;
        }

        private ParticleSystem CreateRain()
        {
            var host = new GameObject("Rain");
            host.transform.SetParent(transform, false);
            var map = _resort.Map;
            host.transform.position = new Vector3(map.Width / 2f, 14f, map.Height / 2f);

            var ps = host.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startSize = 0.06f;
            main.startLifetime = 1.6f;
            main.startSpeed = 9f;
            main.maxParticles = 2000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.4f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(map.Width + 12f, 1f, map.Height + 12f);

            var emission = ps.emission;
            emission.rateOverTime = 900f;
            emission.enabled = false;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Particles/Standard Unlit"))
            {
                color = new Color(0.65f, 0.75f, 0.9f, 0.55f),
                name = "Rain",
            };
            return ps;
        }
    }
}
