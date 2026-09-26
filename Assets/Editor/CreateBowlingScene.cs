using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MiniBowling.Editor
{
    public static class CreateBowlingScene
    {
        public static void Create()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            {
                AssetDatabase.CreateFolder("Assets", "Materials");
            }
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }
            Material laneMaterial = CreateMaterial("Assets/Materials/Lane.mat", new Color(0.62f, 0.33f, 0.12f));
            Material ballMaterial = CreateMaterial("Assets/Materials/Ball.mat", new Color(0.05f, 0.2f, 0.75f));
            Material pinMaterial = CreateMaterial("Assets/Materials/Pin.mat", Color.white);
            Material stripeMaterial = CreateMaterial("Assets/Materials/Stripe.mat", new Color(0.85f, 0.05f, 0.05f));
            Material wallMaterial = CreateMaterial("Assets/Materials/Wall.mat", new Color(0.08f, 0.1f, 0.16f));

            CreateCamera();
            CreateLight();
            CreateCube("Lane", new Vector3(0f, -0.15f, 7f), new Vector3(4.8f, 0.3f, 31f), laneMaterial);
            CreateCube("Left Rail", new Vector3(-2.55f, 0.1f, 7f), new Vector3(0.18f, 0.5f, 31f), wallMaterial);
            CreateCube("Right Rail", new Vector3(2.55f, 0.1f, 7f), new Vector3(0.18f, 0.5f, 31f), wallMaterial);
            CreateCube("Backstop", new Vector3(0f, 1.5f, 22.5f), new Vector3(5.4f, 3f, 0.3f), wallMaterial);

            Rigidbody ball = CreateBall(ballMaterial);
            BowlingPin[] pins = CreatePins(pinMaterial, stripeMaterial);

            GameObject gameObject = new GameObject("Bowling Game");
            BowlingGame game = gameObject.AddComponent<BowlingGame>();
            SerializedObject serializedGame = new SerializedObject(game);
            serializedGame.FindProperty("ball").objectReferenceValue = ball;
            SerializedProperty pinProperty = serializedGame.FindProperty("pins");
            pinProperty.arraySize = pins.Length;
            for (int i = 0; i < pins.Length; i++)
            {
                pinProperty.GetArrayElementAtIndex(i).objectReferenceValue = pins[i];
            }
            serializedGame.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Bowling.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Bowling.unity", true) };
            AssetDatabase.SaveAssets();
        }

        private static void CreateCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 5.8f, -11.5f);
            cameraObject.transform.LookAt(new Vector3(0f, 0.7f, 8f));
            camera.backgroundColor = new Color(0.06f, 0.08f, 0.13f);
            camera.fieldOfView = 53f;
        }

        private static void CreateLight()
        {
            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.35f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -25f, 0f);
        }

        private static Rigidbody CreateBall(Material material)
        {
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Ball";
            ball.transform.position = new Vector3(0f, 0.5f, -6f);
            ball.transform.localScale = Vector3.one;
            ball.GetComponent<Renderer>().sharedMaterial = material;
            Rigidbody body = ball.AddComponent<Rigidbody>();
            body.mass = 6f;
            body.linearDamping = 0.02f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            return body;
        }

        private static BowlingPin[] CreatePins(Material pinMaterial, Material stripeMaterial)
        {
            BowlingPin[] pins = new BowlingPin[10];
            int index = 0;
            const float spacing = 0.55f;
            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column <= row; column++)
                {
                    float x = (column - row * 0.5f) * spacing;
                    float z = 16f + row * spacing * 0.9f;
                    GameObject pin = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    pin.name = $"Pin {index + 1}";
                    pin.transform.position = new Vector3(x, 0.75f, z);
                    pin.transform.localScale = new Vector3(0.42f, 0.75f, 0.42f);
                    pin.GetComponent<Renderer>().sharedMaterial = pinMaterial;
                    Rigidbody body = pin.AddComponent<Rigidbody>();
                    body.mass = 0.7f;
                    body.linearDamping = 0.08f;
                    BowlingPin bowlingPin = pin.AddComponent<BowlingPin>();
                    pins[index++] = bowlingPin;

                    GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    stripe.name = "Red Stripe";
                    stripe.transform.SetParent(pin.transform);
                    stripe.transform.localPosition = new Vector3(0f, 0.45f, 0f);
                    stripe.transform.localScale = new Vector3(0.98f, 0.06f, 0.98f);
                    stripe.GetComponent<Renderer>().sharedMaterial = stripeMaterial;
                    Object.DestroyImmediate(stripe.GetComponent<Collider>());
                }
            }
            return pins;
        }

        private static GameObject CreateCube(string objectName, Vector3 position, Vector3 scale, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = objectName;
            cube.transform.position = position;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            return cube;
        }

        private static Material CreateMaterial(string path, Color color)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            return material;
        }
    }
}
