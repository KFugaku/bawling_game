using UnityEngine;

/// <summary>
/// Creates the visible, playable surroundings for a single bowling lane.
/// It also upgrades scenes made before the gutter geometry was added.
/// </summary>
public static class BowlingAlleyEnvironment
{
    private const string RootName = "Alley Environment";
    private const float WallThickness = 0.18f;
    // About three metres in this project's real-world scale, tall enough to keep the outside out of view.
    private const float WallHeight = 14f;
    private const float GutterSurfaceThickness = 0.1f;

    public static void EnsureCreated()
    {
        if (GameObject.Find(RootName) != null)
        {
            return;
        }

        RemoveLegacyFlatGutters();
        CreateGeometry(CreateRuntimeMaterial("Gutter Runtime Material", new Color(0.055f, 0.07f, 0.09f)),
            CreateRuntimeMaterial("Wall Runtime Material", new Color(0.14f, 0.18f, 0.25f)));
    }

    public static void CreateSceneGeometry(Material gutterMaterial, Material wallMaterial)
    {
        if (GameObject.Find(RootName) == null)
        {
            CreateGeometry(gutterMaterial, wallMaterial);
        }
    }

    private static void CreateGeometry(Material gutterMaterial, Material wallMaterial)
    {
        GameObject root = new GameObject(RootName);
        Transform parent = root.transform;

        CreateGutter(-1f, gutterMaterial, parent);
        CreateGutter(1f, gutterMaterial, parent);
        CreateBuilding(wallMaterial, gutterMaterial, parent);
    }

    private static void CreateGutter(float side, Material material, Transform parent)
    {
        float laneHalfWidth = RegulationBowlingDimensions.LaneWidth * 0.5f;
        float gutterStartZ = RegulationBowlingDimensions.BallStartZ - RegulationBowlingDimensions.BallRadius;
        float gutterEndZ = RegulationBowlingDimensions.LaneLength + RegulationBowlingDimensions.PitDepth;
        float gutterLength = gutterEndZ - gutterStartZ;
        float gutterCenterZ = gutterStartZ + gutterLength * 0.5f;
        float gutterDepth = (RegulationBowlingDimensions.GutterFrontDepth + RegulationBowlingDimensions.GutterRearDepth) * 0.5f;
        float slopeWidth = RegulationBowlingDimensions.GutterWidth * 0.28f;
        float floorWidth = RegulationBowlingDimensions.GutterWidth - slopeWidth * 2f;
        float floorCenterX = laneHalfWidth + slopeWidth + floorWidth * 0.5f;
        float slopeAngle = Mathf.Atan2(gutterDepth, slopeWidth) * Mathf.Rad2Deg;

        // The inner and outer slopes plus the flat bed create a physical U-shaped trough.
        CreateBox(
            side < 0f ? "Left Gutter Inner Slope" : "Right Gutter Inner Slope",
            new Vector3(side * (laneHalfWidth + slopeWidth * 0.5f), -gutterDepth * 0.5f, gutterCenterZ),
            new Vector3(slopeWidth, GutterSurfaceThickness, gutterLength),
            material,
            parent,
            new Vector3(0f, 0f, side < 0f ? slopeAngle : -slopeAngle));
        CreateBox(
            side < 0f ? "Left Gutter Floor" : "Right Gutter Floor",
            new Vector3(side * floorCenterX, -gutterDepth, gutterCenterZ),
            new Vector3(floorWidth, GutterSurfaceThickness, gutterLength),
            material,
            parent,
            Vector3.zero);
        CreateBox(
            side < 0f ? "Left Gutter Outer Slope" : "Right Gutter Outer Slope",
            new Vector3(side * (laneHalfWidth + RegulationBowlingDimensions.GutterWidth - slopeWidth * 0.5f), -gutterDepth * 0.5f, gutterCenterZ),
            new Vector3(slopeWidth, GutterSurfaceThickness, gutterLength),
            material,
            parent,
            new Vector3(0f, 0f, side < 0f ? -slopeAngle : slopeAngle));
    }

    private static void CreateBuilding(Material wallMaterial, Material floorMaterial, Transform parent)
    {
        // Start behind the camera so the player is already inside the alley when play begins.
        float alleyStartZ = RegulationBowlingDimensions.PlayerCameraZ - 2f;
        float alleyEndZ = RegulationBowlingDimensions.LaneLength + RegulationBowlingDimensions.PitDepth + WallThickness;
        float alleyLength = alleyEndZ - alleyStartZ;
        float alleyCenterZ = alleyStartZ + alleyLength * 0.5f;
        float halfInteriorWidth = RegulationBowlingDimensions.LaneWidth * 0.5f + RegulationBowlingDimensions.GutterWidth;
        float outerWallX = halfInteriorWidth + WallThickness * 0.5f;

        CreateBox("Alley Floor", new Vector3(0f, -0.58f, alleyCenterZ),
            new Vector3(halfInteriorWidth * 2f + WallThickness * 2f, 0.12f, alleyLength), floorMaterial, parent, Vector3.zero);
        CreateBox("Left Alley Wall", new Vector3(-outerWallX, WallHeight * 0.5f, alleyCenterZ),
            new Vector3(WallThickness, WallHeight, alleyLength), wallMaterial, parent, Vector3.zero);
        CreateBox("Right Alley Wall", new Vector3(outerWallX, WallHeight * 0.5f, alleyCenterZ),
            new Vector3(WallThickness, WallHeight, alleyLength), wallMaterial, parent, Vector3.zero);
        CreateBox("Alley Back Wall", new Vector3(0f, WallHeight * 0.5f, alleyEndZ),
            new Vector3(halfInteriorWidth * 2f + WallThickness * 2f, WallHeight, WallThickness), wallMaterial, parent, Vector3.zero);
        CreateBox("Alley Ceiling", new Vector3(0f, WallHeight, alleyCenterZ),
            new Vector3(halfInteriorWidth * 2f + WallThickness * 2f, WallThickness, alleyLength), wallMaterial, parent, Vector3.zero);
    }

    private static void CreateBox(string objectName, Vector3 position, Vector3 scale, Material material, Transform parent, Vector3 rotation)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = objectName;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = position;
        box.transform.localRotation = Quaternion.Euler(rotation);
        box.transform.localScale = scale;
        box.GetComponent<Renderer>().sharedMaterial = material;
    }

    private static void RemoveLegacyFlatGutters()
    {
        RemoveObjectIfPresent("Left Gutter");
        RemoveObjectIfPresent("Right Gutter");
    }

    private static void RemoveObjectIfPresent(string objectName)
    {
        GameObject legacyObject = GameObject.Find(objectName);
        if (legacyObject != null)
        {
            Object.Destroy(legacyObject);
        }
    }

    private static Material CreateRuntimeMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material material = new Material(shader) { name = materialName, color = color };
        return material;
    }
}
