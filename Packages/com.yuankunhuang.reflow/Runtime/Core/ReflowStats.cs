namespace Reflow
{
    /// <summary> Counters for tests, benchmarks and profiling. Cheap enough to stay on in builds. </summary>
    public static class ReflowStats
    {
        public static int FlushCount { get; internal set; }
        public static int RootLayoutCount { get; internal set; }
        public static int MeasureCount { get; internal set; }
        public static int ArrangeCount { get; internal set; }
        public static int SolveCount { get; internal set; }
        public static int RectWriteCount { get; internal set; }
        public static int SpawnCount { get; internal set; }
        public static int DespawnCount { get; internal set; }
        public static int CreateCount { get; internal set; }

        public static void Reset()
        {
            FlushCount = 0;
            RootLayoutCount = 0;
            MeasureCount = 0;
            ArrangeCount = 0;
            SolveCount = 0;
            RectWriteCount = 0;
            SpawnCount = 0;
            DespawnCount = 0;
            CreateCount = 0;
        }
    }
}
