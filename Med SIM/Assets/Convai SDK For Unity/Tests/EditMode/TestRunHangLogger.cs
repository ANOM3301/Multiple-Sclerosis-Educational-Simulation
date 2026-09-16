using System;
using System.IO;
using Convai.Tests.EditMode;
using NUnit.Framework.Interfaces;
using UnityEngine;
using UnityEngine.TestRunner;

[assembly: TestRunCallback(typeof(TestRunHangLogger))]

namespace Convai.Tests.EditMode
{
    internal sealed class TestRunHangLogger : ITestRunCallback
    {
        private static readonly object SyncRoot = new();

        private static string LogFilePath => Path.GetFullPath(
            Path.Combine(UnityEngine.Application.dataPath, "..", "Logs", "test-run-hang-trace.log"));

        public void RunStarted(ITest testsToRun) => WriteLine($"RUN-START|{testsToRun.FullName}");

        public void RunFinished(ITestResult testResults) =>
            WriteLine($"RUN-FINISH|{GetFullName(testResults)}|{testResults.ResultState}|{testResults.Duration:F3}s");

        public void TestStarted(ITest test)
        {
            if (test.IsSuite) return;

            WriteLine($"TEST-START|{test.FullName}");
        }

        public void TestFinished(ITestResult result)
        {
            if (result.Test != null && result.Test.IsSuite) return;

            WriteLine($"TEST-FINISH|{GetFullName(result)}|{result.ResultState}|{result.Duration:F3}s");
        }

        private static string GetFullName(ITestResult result)
        {
            if (result.Test != null && !string.IsNullOrEmpty(result.Test.FullName)) return result.Test.FullName;

            return result.Name;
        }

        private static void WriteLine(string message)
        {
            try
            {
                string logFilePath = LogFilePath;
                string directory = Path.GetDirectoryName(logFilePath);

                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                string line = $"{DateTime.UtcNow:O}|{message}{Environment.NewLine}";

                lock (SyncRoot) File.AppendAllText(logFilePath, line);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"TestRunHangLogger failed to write diagnostic output: {exception.Message}");
            }
        }
    }
}
