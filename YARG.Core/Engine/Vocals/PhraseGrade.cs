namespace YARG.Core.Engine.Vocals
{
    /// <summary>
    /// Per-phrase outcome for Party Vocals scoring. Counts how many canonical HARM meters
    /// crossed the awesome threshold by phrase end. Capped by the number of HARM parts that
    /// actually appear in the phrase (a 2-part phrase tops out at DoubleAwesome).
    /// </summary>
    /// <remarks>
    /// Drives display banner and end-of-song stat tracking only. Does NOT multiply score or
    /// bump the score multiplier — those are existing systems unchanged. Combo continues
    /// iff the grade is not Miss.
    /// </remarks>
    public enum PhraseGrade
    {
        Miss,
        Awesome,
        DoubleAwesome,
        TripleAwesome,
    }
}
