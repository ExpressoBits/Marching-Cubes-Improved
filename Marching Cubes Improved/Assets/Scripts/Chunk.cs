using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class Chunk : MonoBehaviour
{
    [HideInInspector] public bool readyForUpdate;
    [HideInInspector] public Point[,,] points;
    [HideInInspector] public int chunkSize;
    [HideInInspector] public Vector3Int position;

    private float _isolevel;
    private int _seed;

    private MeshFilter _meshFilter;
    private MeshCollider _meshCollider;
    private DensityGenerator _densityGenerator;
    private World _world;

    private void Awake()
    {
        _meshFilter = GetComponent<MeshFilter>();
        _meshCollider = GetComponent<MeshCollider>();
    }

    private void Start()
    {
        Generate();
    }

    private void Update()
    {
        if (readyForUpdate)
        {
            Generate();
            readyForUpdate = false;
        }
    }

    public void Initialize(World world, int chunkSize, Vector3Int position)
    {
        this.chunkSize = chunkSize;
        this.position = position;
        _isolevel = world.isolevel;
        _world = world;

        _densityGenerator = world.densityGenerator;

        int worldPosX = position.x;
        int worldPosY = position.y;
        int worldPosZ = position.z;

        points = new Point[chunkSize + 1, chunkSize + 1, chunkSize + 1];

        _seed = world.seed;

        for (int z = 0; z < points.GetLength(2); z++)
        {
            for (int y = 0; y < points.GetLength(1); y++)
            {
                for (int x = 0; x < points.GetLength(0); x++)
                {
                    points[x, y, z] = new Point(
                        new Vector3Int(x, y, z),
                        _densityGenerator.CalculateDensity(x + worldPosX, y + worldPosY, z + worldPosZ)
                    );
                }
            }
        }
    }

    public void Generate()
    {
        Mesh mesh = CreateMeshWithJobs();

        _meshFilter.sharedMesh = mesh;
        _meshCollider.sharedMesh = mesh;
    }

    Mesh CreateMeshWithJobs()
    {
        int[,,] cubeIndices = GenerateCubeIndices(this.points);

        int vertexCount = GenerateVertexCount(cubeIndices);

        // Inputs
        NativeArray<Point> points = this.points.ToNativeArray(Allocator.TempJob);

        // Outputs
        NativeArray<Vector3> vertices = new NativeArray<Vector3>(vertexCount, Allocator.TempJob);
        NativeArray<int> triangles = new NativeArray<int>(vertexCount, Allocator.TempJob);

        MarchingCubesJob job = new MarchingCubesJob
        {
            // Inputs
            points = points,
            chunkSize = chunkSize,
            isolevel = _isolevel,

            vertices = vertices,
            triangles = triangles
        };

        JobHandle jobHandle = job.Schedule();

        jobHandle.Complete();

        points.Dispose();

        Vector3[] meshVertices = vertices.ToArray();
        vertices.Dispose();

        int[] meshTriangles = triangles.ToArray();
        triangles.Dispose();

        Mesh mesh = new Mesh()
        {
            vertices = meshVertices,
            triangles = meshTriangles
        };

        mesh.RecalculateNormals();

        return mesh;
    }

    private int GenerateVertexCount(int[,,] cubeIndexes)
    {
        int vertexCount = 0;

        for (int z = 0; z < cubeIndexes.GetLength(2); z++)
        {
            for (int y = 0; y < cubeIndexes.GetLength(1); y++)
            {
                for (int x = 0; x < cubeIndexes.GetLength(0); x++)
                {
                    int cubeIndex = cubeIndexes[x, y, z];
                    int[] row = LookupTables.TriangleTable[cubeIndex];
                    vertexCount += row.Length;
                }
            }
        }

        return vertexCount;
    }

    private int[,,] GenerateCubeIndices(Point[,,] points)
    {
        int[,,] cubeIndexes = new int[points.GetLength(0), points.GetLength(1), points.GetLength(2)];

        for (int z = 0; z < points.GetLength(2) - 1; z++)
        {
            for (int y = 0; y < points.GetLength(1) - 1; y++)
            {
                for (int x = 0; x < points.GetLength(0) - 1; x++)
                {
                    cubeIndexes[x, y, z] = CalculateCubeIndex(GetPoints(x, y, z, points), _isolevel);
                }
            }
        }

        return cubeIndexes;
    }

    private int CalculateCubeIndex(Point[] points, float iso)
    {
        int cubeIndex = 0;

        for (int i = 0; i < 8; i++)
            if (points[i].density < iso)
                cubeIndex |= 1 << i;

        return cubeIndex;
    }

    private Point[] GetPoints(int x, int y, int z, Point[,,] points)
    {
        Point[] cubePoints = new Point[8];
        for (int i = 0; i < 8; i++)
        {
            cubePoints[i] = points[x + LookupTables.CubePoints[i].x, y + LookupTables.CubePoints[i].y, z + LookupTables.CubePoints[i].z];
        }

        return cubePoints;
    }

    public Point GetPoint(int x, int y, int z)
    {
        return points[x, y, z];
    }

    public void SetDensity(float density, int x, int y, int z)
    {
        points[x, y, z].density = density;
    }

    public void SetDensity(float density, Vector3Int pos)
    {
        SetDensity(density, pos.x, pos.y, pos.z);
    }
}
