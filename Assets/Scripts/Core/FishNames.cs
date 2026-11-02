namespace FishBreeder.Core
{
    public static class FishNames
    {
        static readonly string[] Starts = { "Bub", "Fin", "Gil", "Splash", "Cor", "Mar", "Nem", "Pip", "Shel", "Wav", "Dor", "Zig", "Lum", "Kel" };
        static readonly string[] Ends = { "bles", "ny", "bert", "y", "al", "lin", "o", "sy", "ka", "ette", "ius", "ra", "mo", "ix" };

        public static string Random()
        {
            return Starts[GameRandom.Range(0, Starts.Length)] + Ends[GameRandom.Range(0, Ends.Length)];
        }
    }
}
