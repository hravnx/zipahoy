using System;
using System.Threading;

namespace ZipAhoy.Tests
{
    public class TestCancelProgressReporter : IProgress<float>
    {
        readonly CancellationTokenSource tokenSource;
        readonly int cancelAfterReports;

        public int ReportCount { get; private set; }

        public TestCancelProgressReporter(int cancelAfterReports, CancellationTokenSource tokenSource)
        {
            this.cancelAfterReports = cancelAfterReports;
            this.tokenSource = tokenSource;
        }

        public static implicit operator Action<float>(TestCancelProgressReporter reporter)
            => pct => reporter.Report(pct);

        public void Report(float value)
        {
            if (++ReportCount >= cancelAfterReports)
            {
                tokenSource.Cancel();
            }
        }
    }
}
