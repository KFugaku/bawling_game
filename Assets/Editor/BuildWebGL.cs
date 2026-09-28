using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MiniBowling.Editor
{
    public static class BuildWebGL
    {
        private const string OutputDirectoryName = "docs";

        [MenuItem("Mini Bowling/Build WebGL")]
        public static void Build()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("Unity project root could not be found.");
            string outputPath = Path.Combine(projectRoot, OutputDirectoryName);

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && File.Exists(scene.path))
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                throw new InvalidOperationException("No enabled scenes were found in Build Settings.");
            }

            Directory.CreateDirectory(outputPath);

            PlayerSettings.productName = "Mini Bowling";

            // GitHub Pages does not attach Unity's required Content-Encoding headers,
            // so an uncompressed build is the most reliable option for this project.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"WebGL build failed: {summary.result} ({summary.totalErrors} errors)");
            }

            File.WriteAllText(Path.Combine(outputPath, ".nojekyll"), string.Empty);
            Debug.Log($"WebGL build completed: {outputPath} ({summary.totalSize} bytes)");
        }
    }
}
