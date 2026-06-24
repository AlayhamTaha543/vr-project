using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static helper class that builds procedural meshes for the bucket, rims, and paper.
/// It is not attached to any GameObject.
/// </summary>
public static class BucketMeshBuilder
{
    public static Mesh BuildBucket(float height, float topWidth, float bottomWidth, float wallThickness, float holeDiameter, int segments)
    {
        segments = Mathf.Clamp(segments, 12, 96);

        float topRadius = Mathf.Max(0.01f, topWidth * 0.5f);
        float bottomRadius = Mathf.Max(0.01f, bottomWidth * 0.5f);
        float holeRadius = Mathf.Max(0.0025f, holeDiameter * 0.5f);

        float topY = height * 0.5f;
        float bottomY = -height * 0.5f;

        float wall = Mathf.Clamp(wallThickness, 0.005f, Mathf.Min(topRadius, bottomRadius) * 0.45f);
        float innerTopRadius = Mathf.Max(0.01f, topRadius - wall);
        float innerBottomRadius = Mathf.Max(holeRadius + 0.02f, bottomRadius - wall);
        holeRadius = Mathf.Clamp(holeRadius, 0.0025f, innerBottomRadius - 0.01f);

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        for (int i = 0; i < segments; i++)
        {
            float a0 = i * Mathf.PI * 2f / segments;
            float a1 = (i + 1) * Mathf.PI * 2f / segments;

            Vector3 outerTop0 = Circle(topRadius, topY, a0);
            Vector3 outerTop1 = Circle(topRadius, topY, a1);
            Vector3 outerBottom0 = Circle(bottomRadius, bottomY, a0);
            Vector3 outerBottom1 = Circle(bottomRadius, bottomY, a1);

            Vector3 innerTop0 = Circle(innerTopRadius, topY, a0);
            Vector3 innerTop1 = Circle(innerTopRadius, topY, a1);
            Vector3 innerBottom0 = Circle(innerBottomRadius, bottomY, a0);
            Vector3 innerBottom1 = Circle(innerBottomRadius, bottomY, a1);

            Vector3 hole0 = Circle(holeRadius, bottomY, a0);
            Vector3 hole1 = Circle(holeRadius, bottomY, a1);
            Vector3 holeLower0 = Circle(holeRadius, bottomY - wall * 0.45f, a0);
            Vector3 holeLower1 = Circle(holeRadius, bottomY - wall * 0.45f, a1);

            Quad(vertices, triangles, outerTop0, outerTop1, outerBottom1, outerBottom0);      // outer wall
            Quad(vertices, triangles, innerTop0, innerBottom0, innerBottom1, innerTop1);      // inner wall
            Quad(vertices, triangles, innerBottom0, hole0, hole1, innerBottom1);              // bottom ring around hole
            Quad(vertices, triangles, hole0, holeLower0, holeLower1, hole1);                  // visible hole thickness
        }

        return FinishMesh("bucket mesh", vertices, triangles);
    }

    public static Mesh BuildTorus(float majorRadius, float tubeRadius, int radialSegments, int tubeSegments)
    {
        radialSegments = Mathf.Max(12, radialSegments);
        tubeSegments = Mathf.Max(6, tubeSegments);
        majorRadius = Mathf.Max(0.005f, majorRadius);
        tubeRadius = Mathf.Max(0.004f, tubeRadius);

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        for (int r = 0; r < radialSegments; r++)
        {
            float a0 = r * Mathf.PI * 2f / radialSegments;
            float a1 = (r + 1) * Mathf.PI * 2f / radialSegments;

            for (int t = 0; t < tubeSegments; t++)
            {
                float b0 = t * Mathf.PI * 2f / tubeSegments;
                float b1 = (t + 1) * Mathf.PI * 2f / tubeSegments;

                Quad(vertices, triangles,
                    Torus(majorRadius, tubeRadius, a0, b0),
                    Torus(majorRadius, tubeRadius, a1, b0),
                    Torus(majorRadius, tubeRadius, a1, b1),
                    Torus(majorRadius, tubeRadius, a0, b1));
            }
        }

        return FinishMesh("torus mesh", vertices, triangles);
    }

    public static Mesh BuildPaper(float width, float length)
    {
        float halfWidth = Mathf.Max(0.1f, width) * 0.5f;
        float halfLength = Mathf.Max(0.1f, length) * 0.5f;

        List<Vector3> vertices = new List<Vector3>
        {
            new Vector3(-halfWidth, 0f, -halfLength),
            new Vector3(-halfWidth, 0f,  halfLength),
            new Vector3( halfWidth, 0f,  halfLength),
            new Vector3( halfWidth, 0f, -halfLength)
        };

        List<int> triangles = new List<int> { 0, 1, 2, 0, 2, 3 };
        return FinishMesh("paper mesh", vertices, triangles);
    }

    private static Vector3 Circle(float radius, float y, float angle)
    {
        return new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
    }

    private static Vector3 Torus(float majorRadius, float tubeRadius, float majorAngle, float tubeAngle)
    {
        float ringRadius = majorRadius + Mathf.Cos(tubeAngle) * tubeRadius;
        float x = Mathf.Cos(majorAngle) * ringRadius;
        float y = Mathf.Sin(tubeAngle) * tubeRadius;
        float z = Mathf.Sin(majorAngle) * ringRadius;
        return new Vector3(x, y, z);
    }

    private static void Quad(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        int start = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        vertices.Add(d);

        triangles.Add(start + 0);
        triangles.Add(start + 1);
        triangles.Add(start + 2);
        triangles.Add(start + 0);
        triangles.Add(start + 2);
        triangles.Add(start + 3);
    }

    private static Mesh FinishMesh(string name, List<Vector3> vertices, List<int> triangles)
    {
        Mesh mesh = new Mesh();
        mesh.name = name;
        mesh.hideFlags = HideFlags.HideAndDontSave;
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
