using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MiniBowling.Editor
{
    public static class CreateBowlingScene
    {
        [MenuItem("Mini Bowling/Create Regulation Scene")]
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
            Material gutterMaterial = CreateMaterial("Assets/Materials/Gutter.mat", new Color(0.035f, 0.04f, 0.055f));

            CreateCamera();
            CreateLight();
            CreateLaneGeometry(laneMaterial, gutterMaterial, wallMaterial);

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
            cameraObject.transform.position = new Vector3(
                0f,
                RegulationBowlingDimensions.PlayerCameraHeight,
                RegulationBowlingDimensions.PlayerCameraZ);
            cameraObject.transform.LookAt(new Vector3(
                0f,
                RegulationBowlingDimensions.PlayerCameraTargetHeight,
                RegulationBowlingDimensions.PlayerCameraTargetZ));
            camera.backgroundColor = new Color(0.06f, 0.08f, 0.13f);
            camera.fieldOfView = RegulationBowlingDimensions.PlayerCameraFieldOfView;
        }

        private static void CreateLight()
        {
            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.35f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -25f, 0f);
        }

        private static void CreateLaneGeometry(Material laneMaterial, Material gutterMaterial, Material wallMaterial)
        {
            const float surfaceThickness = 0.3f;
            CreateCube(
                "Approach",
                new Vector3(0f, -surfaceThickness * 0.5f, -RegulationBowlingDimensions.ApproachLength * 0.5f),
                new Vector3(RegulationBowlingDimensions.LaneWidth, surfaceThickness, RegulationBowlingDimensions.ApproachLength),
                laneMaterial);
            CreateCube(
                "Lane",
                new Vector3(0f, -surfaceThickness * 0.5f, RegulationBowlingDimensions.FoulLineToHeadPin * 0.5f),
                new Vector3(RegulationBowlingDimensions.LaneWidth, surfaceThickness, RegulationBowlingDimensions.FoulLineToHeadPin),
                laneMaterial);
            CreateCube(
                "Pin Deck",
                new Vector3(0f, -surfaceThickness * 0.5f, RegulationBowlingDimensions.FoulLineToHeadPin + RegulationBowlingDimensions.HeadPinToRearDeck * 0.5f),
                new Vector3(RegulationBowlingDimensions.LaneWidth, surfaceThickness, RegulationBowlingDimensions.HeadPinToRearDeck),
                laneMaterial);

            GameObject foulLine = CreateCube(
                "Foul Line",
                new Vector3(0f, 0.006f, 0f),
                new Vector3(RegulationBowlingDimensions.LaneWidth, 0.012f, RegulationBowlingDimensions.FoulLineWidth),
                wallMaterial);
            Object.DestroyImmediate(foulLine.GetComponent<Collider>());

            BowlingAlleyEnvironment.CreateSceneGeometry(gutterMaterial, wallMaterial);

            float kickbackStart = RegulationBowlingDimensions.FoulLineToHeadPin - 15f / RegulationBowlingDimensions.InchesPerUnit;
            float kickbackEnd = RegulationBowlingDimensions.LaneLength + RegulationBowlingDimensions.PitDepth;
            float kickbackLength = kickbackEnd - kickbackStart;
            float kickbackHalfWidth = RegulationBowlingDimensions.KickbackFaceSpacing * 0.5f;
            const float kickbackThickness = 0.15f;
            float kickbackX = kickbackHalfWidth + kickbackThickness * 0.5f;
            Vector3 kickbackScale = new Vector3(kickbackThickness, RegulationBowlingDimensions.KickbackHeight, kickbackLength);
            float kickbackZ = kickbackStart + kickbackLength * 0.5f;
            CreateCube("Left Kickback", new Vector3(-kickbackX, RegulationBowlingDimensions.KickbackHeight * 0.5f, kickbackZ), kickbackScale, wallMaterial);
            CreateCube("Right Kickback", new Vector3(kickbackX, RegulationBowlingDimensions.KickbackHeight * 0.5f, kickbackZ), kickbackScale, wallMaterial);

            CreateCube(
                "Pit Floor",
                new Vector3(0f, -RegulationBowlingDimensions.PitFloorDepth - 0.1f, RegulationBowlingDimensions.LaneLength + RegulationBowlingDimensions.PitDepth * 0.5f),
                new Vector3(RegulationBowlingDimensions.KickbackFaceSpacing, 0.2f, RegulationBowlingDimensions.PitDepth),
                gutterMaterial);
            CreateCube(
                "Rear Cushion",
                new Vector3(0f, RegulationBowlingDimensions.KickbackHeight * 0.5f, RegulationBowlingDimensions.LaneLength + RegulationBowlingDimensions.PitDepth),
                new Vector3(RegulationBowlingDimensions.KickbackFaceSpacing + kickbackThickness * 2f, RegulationBowlingDimensions.KickbackHeight, 0.2f),
                wallMaterial);
        }

        private static Rigidbody CreateBall(Material material)
        {
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Ball";
            ball.transform.position = new Vector3(0f, RegulationBowlingDimensions.BallRadius, RegulationBowlingDimensions.BallStartZ);
            ball.transform.localScale = Vector3.one;
            ball.GetComponent<Renderer>().sharedMaterial = material;
            Rigidbody body = ball.AddComponent<Rigidbody>();
            body.mass = RegulationBowlingDimensions.BallMass;
            body.linearDamping = 0f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            return body;
        }

        private static BowlingPin[] CreatePins(Material pinMaterial, Material stripeMaterial)
        {
            BowlingPin[] pins = new BowlingPin[10];
            int index = 0;
            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column <= row; column++)
                {
                    float x = (column - row * 0.5f) * RegulationBowlingDimensions.PinCenterSpacing;
                    float z = RegulationBowlingDimensions.FoulLineToHeadPin + row * RegulationBowlingDimensions.PinRowDepth;
                    GameObject pin = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    pin.name = $"Pin {index + 1}";
                    pin.transform.position = new Vector3(x, RegulationBowlingDimensions.PinHeight * 0.5f, z);
                    pin.transform.localScale = new Vector3(
                        RegulationBowlingDimensions.PinMaximumDiameter,
                        RegulationBowlingDimensions.PinHeight * 0.5f,
                        RegulationBowlingDimensions.PinMaximumDiameter);
                    pin.GetComponent<Renderer>().sharedMaterial = pinMaterial;
                    Rigidbody body = pin.AddComponent<Rigidbody>();
                    body.mass = RegulationBowlingDimensions.PinWeight;
                    body.linearDamping = 0.16f;
                    body.angularDamping = 0.24f;
                    body.centerOfMass = new Vector3(0f, RegulationBowlingDimensions.PinCenterOfMassLocalY, 0f);
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
