using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace WitchTower.Tests
{
    public static class WitchTowerEditModeTestRunner
    {
        private static TestRunnerApi activeRunner;
        private static ResultCallbacks activeCallbacks;

        [MenuItem("WitchTower/Run Edit Mode Tests")]
        public static void Run()
        {
            if (activeRunner != null)
            {
                Debug.LogWarning("[WitchTowerTests] A test run is already active.");
                return;
            }

            activeRunner = ScriptableObject.CreateInstance<TestRunnerApi>();
            activeCallbacks = new ResultCallbacks();
            activeRunner.RegisterCallbacks(activeCallbacks);
            activeRunner.Execute(new ExecutionSettings(new Filter
            {
                assemblyNames = new[] { "WitchTower.EditModeTests" },
                testMode = TestMode.EditMode
            }));
        }

        private sealed class ResultCallbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
                Debug.Log("[WitchTowerTests] START");
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                string summary =
                    $"[WitchTowerTests] COMPLETE status={result.TestStatus} " +
                    $"passed={result.PassCount} failed={result.FailCount} " +
                    $"skipped={result.SkipCount} inconclusive={result.InconclusiveCount}";
                if (result.FailCount > 0 || result.TestStatus == TestStatus.Failed)
                {
                    Debug.LogError(summary);
                }
                else
                {
                    Debug.Log(summary);
                }

                if (activeRunner != null)
                {
                    activeRunner.UnregisterCallbacks(activeCallbacks);
                    Object.DestroyImmediate(activeRunner);
                }

                activeRunner = null;
                activeCallbacks = null;
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.TestStatus == TestStatus.Failed)
                {
                    Debug.LogError($"[WitchTowerTests] FAIL {result.FullName}: {result.Message}\n{result.StackTrace}");
                }
            }
        }
    }
}
