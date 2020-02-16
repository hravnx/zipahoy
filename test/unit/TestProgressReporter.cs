using System;
using System.Collections.Generic;

namespace ZipAhoy.Tests
{
    public class TestProgressReporter : IProgress<float>
    {
        public List<float> ReportedProgress { get; private set; }

        public TestProgressReporter()
        {
            ReportedProgress = new List<float>();
        }

        public void Report(float value)
        {
            ReportedProgress.Add(value);
        }

        public static implicit operator Action<float>(TestProgressReporter reporter) => pct => reporter.Report(pct);

    }
}
