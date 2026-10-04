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

            AddReliablePointerLock(outputPath);
            File.WriteAllText(Path.Combine(outputPath, ".nojekyll"), string.Empty);
            Debug.Log($"WebGL build completed: {outputPath} ({summary.totalSize} bytes)");
        }

        private static void AddReliablePointerLock(string outputPath)
        {
            string indexPath = Path.Combine(outputPath, "index.html");
            string html = File.ReadAllText(indexPath);
            const string marker = "// Mini Bowling: keep relative mouse input away from screen edges.";
            if (html.Contains(marker))
            {
                return;
            }

            const string canvasDeclaration =
                "      var canvas = document.querySelector(\"#unity-canvas\");";
            if (!html.Contains(canvasDeclaration))
            {
                throw new InvalidOperationException(
                    "Could not add WebGL pointer-lock support because the canvas declaration was not found.");
            }

            const string pointerLockScript = @"

      // Mini Bowling: keep relative mouse input away from screen edges.
      canvas.addEventListener('contextmenu', function(event) {
        event.preventDefault();
      });
      canvas.addEventListener('mousedown', function(event) {
        if (event.button !== 2) return;
        canvas.focus();
        if (document.pointerLockElement !== canvas && canvas.requestPointerLock) {
          canvas.requestPointerLock();
        }
      });";

            html = html.Replace(canvasDeclaration, canvasDeclaration + pointerLockScript);
            File.WriteAllText(indexPath, html);
        }
    }
}
