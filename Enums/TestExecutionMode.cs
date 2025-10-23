namespace CacheStressTester.Enums;

public enum TestExecutionMode
{
    Undefined = 0,
    TimedAndBounded = 1,  // Threads + RequestsPerThread + DurationSeconds
    RequestsBounded = 2,  // Threads + RequestsPerThread (no time limit)
    TimedOnly = 3         // Threads + DurationSeconds (no requests limit)
}
