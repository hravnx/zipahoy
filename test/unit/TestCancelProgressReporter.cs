using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ZipAhoy.Tests
{
    public class TestCancelProgressReporter : IProgress<float>
    {
        CancellationTokenSource tokenSource;
        int cancelAfterReports;

        public int ReportCount { get; private set; }

        public TestCancelProgressReporter(int cancelAfterReports, CancellationTokenSource tokenSource)
        {
            this.cancelAfterReports = cancelAfterReports;
            this.tokenSource = tokenSource;
        }
        public void Report(float value)
        {
            if(++ReportCount >= cancelAfterReports)
            {
                tokenSource.Cancel();
            }
        }
    }
}
