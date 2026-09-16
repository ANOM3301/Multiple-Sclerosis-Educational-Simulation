using System;
using System.Linq;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Convai.Tests.EditMode.AI
{
    internal static class ConvaiMcpSceneFixture
    {
        public static Scene CreateTestScene(out bool singleSceneMode)
        {
            singleSceneMode = Environment.GetCommandLineArgs().Any(argument =>
                string.Equals(argument, "-batchmode", StringComparison.OrdinalIgnoreCase));
            return singleSceneMode
                ? EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single)
                : CreateAdditiveTestScene();
        }

        public static Scene CreateAdditiveTestScene()
        {
            Scene active = SceneManager.GetActiveScene();
            bool restoreDirty = active.IsValid() && active.isDirty;
            if (restoreDirty) ClearSceneDirtiness(active);
            try
            {
                return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            }
            finally
            {
                if (restoreDirty && active.IsValid() && active.isLoaded)
                    EditorSceneManager.MarkSceneDirty(active);
            }
        }

        private static void ClearSceneDirtiness(Scene scene)
        {
            MethodInfo method = typeof(EditorSceneManager).GetMethod(
                "ClearSceneDirtiness",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null) throw new InvalidOperationException("Unity Editor has no ClearSceneDirtiness API.");
            method.Invoke(null, new object[] { scene });
        }
    }
}
