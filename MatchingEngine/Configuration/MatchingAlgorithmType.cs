namespace Valkyrie.MatchingEngine.Configuration;

public enum MatchingAlgorithmType
{
    Fifo, // aka price-time priority
    ProRata,
    TimeProRata, // fifo + pro rata hybrid
    LMM, // lead market maker
}