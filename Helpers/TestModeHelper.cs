using CacheStressTester.Enums;

namespace CacheStressTester.Helpers;

public static class TestModeHelper
{
    public static TestExecutionMode DetermineMode(int threads, int requestsPerThread, int durationSeconds)
    {
        if (threads <= 0) return TestExecutionMode.Undefined;

        if (requestsPerThread > 0 && durationSeconds > 0)
            return TestExecutionMode.TimedAndBounded;

        if (requestsPerThread > 0 && durationSeconds == 0)
            return TestExecutionMode.RequestsBounded;

        if (requestsPerThread == 0 && durationSeconds > 0)
            return TestExecutionMode.TimedOnly;

        return TestExecutionMode.Undefined;
    }
}
