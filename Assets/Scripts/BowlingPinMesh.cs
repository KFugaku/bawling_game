using UnityEngine;

/// <summary>
/// Builds a lathed tenpin profile from USBC pin-diameter stations.
/// The mesh is normalized to a height of 2 and a maximum diameter of 1,
/// so the existing pin transform scale remains the source of world size.
/// </summary>
public static class BowlingPinMesh
{
    private const int RadialSegments = 24;
    private const float PinHeightInches = 15f;
    private const float MaximumDiameterInches = 4.797f;

    // Height above the base and diameter at each USBC measurement station.
    private static readonly Vector2[] Profile =
    {
        new Vector2(0f, 2.031f),
        new Vector2(0.75f, 2.828f),
        new Vector2(2.25f, 3.906f),
        new Vector2(3.375f, 4.510f),
        new Vector2(4.5f, 4.766f),
        new Vector2(5.875f, 4.563f),
        new Vector2(7.25f, 3.703f),
        new Vector2(8.625f, 2.472f),
        new Vector2(9.375f, 1.965f),
        new Vector2(10f, 1.797f),
        new Vector2(10.875f, 1.870f),
        new Vector2(11.75f, 2.094f),
        new Vector2(12.625f, 2.406f),
        new Vector2(13.5f, 2.547f),
        new Vector2(14.25f, 2.322f),
        new Vector2(14.75f, 1.516f)
    };

    private static Mesh sharedMesh;

    public static Mesh SharedMesh
    {
        get
        {
            if (sharedMesh == null)
            {
                sharedMesh = BuildMesh();
            }

            return sharedMesh;
        }
    }

    private static Mesh BuildMesh()
    {
        int ringVertexCount = Profile.Length * RadialSegments;
        Vector3[] vertices = new Vector3[ringVertexCount + 2];
        Vector2[] uvs = new Vector2[vertices.Length];

        for (int ring = 0; ring < Profile.Length; ring++)
        {
            float normalizedHeight = Profile[ring].x / PinHeightInches;
            float y = normalizedHeight * 2f - 1f;
            float radius = Profile[ring].y / MaximumDiameterInches * 0.5f;
            for (int segment = 0; segment < RadialSegments; segment++)
            {
                float angle = segment / (float)RadialSegments * Mathf.PI * 2f;
                int index = ring * RadialSegments + segment;
                vertices[index] = new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
                uvs[index] = new Vector2(segment / (float)RadialSegments, normalizedHeight);
            }
        }

        int bottomCenter = ringVertexCount;
        int topCenter = ringVertexCount + 1;
        vertices[bottomCenter] = new Vector3(0f, -1f, 0f);
        vertices[topCenter] = new Vector3(0f, 1f, 0f);
        uvs[bottomCenter] = new Vector2(0.5f, 0f);
        uvs[topCenter] = new Vector2(0.5f, 1f);

        int sideTriangleCount = (Profile.Length - 1) * RadialSegments * 2;
        int[] triangles = new int[(sideTriangleCount + RadialSegments * 2) * 3];
        int triangleIndex = 0;
        for (int ring = 0; ring < Profile.Length - 1; ring++)
        {
            int currentRing = ring * RadialSegments;
            int nextRing = (ring + 1) * RadialSegments;
            for (int segment = 0; segment < RadialSegments; segment++)
            {
                int nextSegment = (segment + 1) % RadialSegments;
                triangles[triangleIndex++] = currentRing + segment;
                triangles[triangleIndex++] = nextRing + segment;
                triangles[triangleIndex++] = nextRing + nextSegment;
                triangles[triangleIndex++] = currentRing + segment;
                triangles[triangleIndex++] = nextRing + nextSegment;
                triangles[triangleIndex++] = currentRing + nextSegment;
            }
        }

        int firstRing = 0;
        int lastRing = (Profile.Length - 1) * RadialSegments;
        for (int segment = 0; segment < RadialSegments; segment++)
        {
            int nextSegment = (segment + 1) % RadialSegments;
            triangles[triangleIndex++] = bottomCenter;
            triangles[triangleIndex++] = firstRing + nextSegment;
            triangles[triangleIndex++] = firstRing + segment;
            triangles[triangleIndex++] = topCenter;
            triangles[triangleIndex++] = lastRing + segment;
            triangles[triangleIndex++] = lastRing + nextSegment;
        }

        Mesh mesh = new Mesh { name = "Regulation Bowling Pin" };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
