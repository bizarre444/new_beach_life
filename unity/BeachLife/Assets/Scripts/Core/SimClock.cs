namespace BeachLife.Core
{
    /// <summary>Game calendar: minutes inside a day, plus day rollover.</summary>
    public sealed class SimClock
    {
        private readonly SimConfig _config;

        public int Day { get; private set; } = 1;
        public float MinuteOfDay { get; private set; }
        public float Speed = 1f;

        public SimClock(SimConfig config)
        {
            _config = config;
            MinuteOfDay = config.DayStartMinute;
        }

        public int Hour => ((int)MinuteOfDay / 60) % 24;
        public int Minute => (int)MinuteOfDay % 60;
        public bool IsNight => Hour >= 21 || Hour < 7;
        public bool IsOpen => MinuteOfDay >= _config.DayStartMinute && MinuteOfDay < _config.DayEndMinute;

        public string TimeLabel => $"Day {Day}  {Hour:00}:{Minute:00}";

        /// <summary>Advances the clock and returns elapsed game minutes; sets <paramref name="dayRolled"/> on rollover.</summary>
        public float Advance(float realSeconds, out bool dayRolled)
        {
            float minutes = realSeconds / _config.RealSecondsPerGameMinute * Speed;
            MinuteOfDay += minutes;
            dayRolled = false;
            if (MinuteOfDay >= _config.DayEndMinute)
            {
                MinuteOfDay = _config.DayStartMinute;
                Day++;
                dayRolled = true;
            }
            return minutes;
        }
    }
}
