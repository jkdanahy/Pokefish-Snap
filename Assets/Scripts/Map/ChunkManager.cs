using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;

public class ChunkManager : MonoBehaviour
{
    //Chunks
    public const int CHUNK_WIDTH = 24;
    public const int CHUNK_HEIGHT = 12;
    public const int WATER_CHUNK_HEIGHT = 4;
    public static readonly int WorldSizeInChunks = 6;
    public static int WorldSizeInVoxels { get {return WorldSizeInChunks * CHUNK_WIDTH; } }
    public static int WorldSizeInBlocks
    {
        get { return WorldSizeInChunks * CHUNK_WIDTH; }
    }
    private Dictionary<Vector2, GroundAndSurface> chunks = new Dictionary<Vector2, GroundAndSurface>();
    [SerializeField] private GameObject chunkPrefab;
    public List<TerrainType> terrainTypes;
    public Vector3 spawn;

    //Chunk Visibility
    List<Chunk> chunksVisibleLastFrame = new List<Chunk>();
    public static Vector2 viewerPosition;
    public static readonly int ViewDistanceInChunks = 6;
    [SerializeField] private Transform viewer;

    //Noise
    [SerializeField] private float noiseScale;
    [SerializeField] private int octaves;
    [SerializeField] [Range(0, 1)] private float persistance;
    [SerializeField] private float lacunarity;
    [SerializeField] private int seed;
    [SerializeField] private Vector2 offset;
    public bool autoUpdate;

    //Other
    [Range(0.95f,0)]
    public float globalLightLevel;
    public static readonly float shadowLightLevel = 0.1f;
    public static Vector2 WaterSpeed = new Vector2(0.05f,1.0f);

    private void Start()
    {
        SpawnWorld();
    }
    private void Update()
    {
        viewerPosition = new Vector2(viewer.position.x, viewer.position.z);
        Shader.SetGlobalFloat("GlobalLightLevel", globalLightLevel);
        //UpdateVisibleChunks();
        UpdateWaterChunks();
    }

    private void SpawnWorld()
    {
        for (int x = WorldSizeInChunks / 2 - ViewDistanceInChunks / 2; x < WorldSizeInChunks / 2 + ViewDistanceInChunks / 2; x++)
        {
            for (int z = WorldSizeInChunks / 2 - ViewDistanceInChunks / 2; z < WorldSizeInChunks / 2 + ViewDistanceInChunks / 2; z++)
            {
                LoadChunk(new Vector2(x, z));
            }
        }

        spawn = new Vector3(WorldSizeInBlocks / 2, CHUNK_WIDTH + 2, WorldSizeInBlocks / 2);

        viewer.position = spawn;
    }

    private void LoadChunk(Vector2 coord)
    {
        Vector3 chunkPos = Vector3.zero;
        chunkPos.x = coord.x * CHUNK_WIDTH;
        chunkPos.z = coord.y * CHUNK_WIDTH;

        //Create a ground block chunk
        GameObject chunkOBJ = Instantiate(chunkPrefab);
        chunkOBJ.transform.parent = transform;
        chunkOBJ.transform.position = chunkPos;
        chunkOBJ.transform.localScale = Vector3.one;
        chunkOBJ.name = "Chunk " + (int)coord.x + "x" + (int)coord.y;
        
        GroundAndSurface gas = new GroundAndSurface();

        //Initialize it and add it to the world
        Chunk chunk = chunkOBJ.AddComponent<Chunk>() as Chunk;
        gas.groundChunk = chunk;

        //Create a water surface chunk
        chunkPos.y = CHUNK_HEIGHT * 4.0f; //We want the water surface to be high up
        GameObject waterChunkOBJ = Instantiate(chunkPrefab);
        waterChunkOBJ.transform.parent = chunkOBJ.transform;
        waterChunkOBJ.transform.position = chunkPos;
        waterChunkOBJ.transform.localScale = Vector3.one;
        waterChunkOBJ.name = "Water Chunk " + (int)coord.x + "x" + (int)coord.y;

        //Initialize the water chunk and add it to the world
        Chunk waterChunk = waterChunkOBJ.AddComponent<Chunk>() as Chunk;
        gas.waterSurfaceChunk = waterChunk;
        chunks.Add(coord, gas);

        float[,] groundHeightMap = Noise.GenerateNoiseMap(CHUNK_WIDTH, CHUNK_WIDTH, seed, noiseScale, octaves, persistance, lacunarity, coord * CHUNK_WIDTH);
        float[,] surfaceHeightMap = Noise.GenerateNoiseMap(CHUNK_WIDTH, CHUNK_WIDTH, seed, noiseScale, octaves, persistance, lacunarity, coord * CHUNK_WIDTH, true);

        gas.groundChunk.Init(coord, groundHeightMap);
        gas.waterSurfaceChunk.Init(coord, surfaceHeightMap, true);
    }

    private void UpdateVisibleChunks()
    {
        //Disable all chunks that were visible last frame (they'll be reenabled later if they should be viewed this frame)
        for (int i = 0; i < chunksVisibleLastFrame.Count; i++)
        {
            chunksVisibleLastFrame[i].Hide();
        }

        //Get the index of the current chunk the viewer is on
        int curChunkIndexX = Mathf.RoundToInt(viewerPosition.x / CHUNK_WIDTH);
        int curChunkIndexY = Mathf.RoundToInt(viewerPosition.y / CHUNK_WIDTH);

        //Go through and each possible chunk around the current one
        for (int y = -ViewDistanceInChunks; y < ViewDistanceInChunks; y++)
        {
            for (int x = -ViewDistanceInChunks; x < ViewDistanceInChunks; x++)
            {
                Vector2 viewedChunkIndex = new Vector2(curChunkIndexX + x, curChunkIndexY + y);

                if (chunks.ContainsKey(viewedChunkIndex)) //If there is a chunk at this index
                {
                    chunks[viewedChunkIndex].groundChunk.Show();
                    chunksVisibleLastFrame.Add(chunks[viewedChunkIndex].groundChunk); //Add it to our list of currently viewable chunks
                }
                else //If there isn't a chunk at this index
                {
                    LoadChunk(viewedChunkIndex); //Make one and add it to our dictionary
                    chunks[viewedChunkIndex].groundChunk.Show();
                }
            }
        }
    }

    private void UpdateWaterChunks()
    {
        foreach (KeyValuePair<Vector2, GroundAndSurface> kvp in chunks)
        {
            float[,] surfaceHeightMap = Noise.GenerateNoiseMap(CHUNK_WIDTH, CHUNK_WIDTH, seed, noiseScale, octaves, persistance, lacunarity, kvp.Key * CHUNK_WIDTH, true);
            kvp.Value.waterSurfaceChunk.UpdateChunk(surfaceHeightMap);
        }
    }

    public float[,] GetChunk(Vector3 coord, Vector3 dir)
    {
        Vector3 chunk = coord + dir;
        Vector2 chunkCoord = new Vector2(chunk.x, chunk.z);
        float[,] noise = new float[CHUNK_WIDTH,CHUNK_WIDTH];
        if (chunks.ContainsKey(chunkCoord))
        {
            noise = chunks[chunkCoord].groundChunk.noise;
        }
        else
        {
            noise = Noise.GenerateNoiseMap(CHUNK_WIDTH, CHUNK_WIDTH, seed, noiseScale, octaves, persistance, lacunarity, chunk * CHUNK_WIDTH);
        }
        return noise;
    }

    public byte GetTerrainType(int height, bool isWater = false)
    {
        float fHeight;

        if (isWater)
        {
            if (height >= WATER_CHUNK_HEIGHT - 1)
            {
                return 6;
            }
            return 5;
        }

        fHeight = (float)height / (float)(CHUNK_HEIGHT - 1);

        for (int i = 0; i < terrainTypes.Count; i++)
        {
            if (fHeight <= terrainTypes[i].height)
            {
                return (byte)i;
            }
        }

        return 0;
    }

    [System.Serializable]
    public class TerrainType
    {
        public string name;
        public float height;
        public Color color;
        public float transparency = 1;

        [Header("Texture Values")]
        public int backFaceTexture;
        public int frontFaceTexture;
        public int topFaceTexture;
        public int botFaceTexture;
        public int leftFaceTexture;
        public int rightFaceTexture;

        public int GetTextureID(int faceIndex)
        {
            switch (faceIndex)
            {
                case 0:
                    return backFaceTexture;                    
                case 1:
                    return frontFaceTexture;                    
                case 2:
                    return topFaceTexture;                    
                case 3:
                    return botFaceTexture;                    
                case 4:
                    return leftFaceTexture;                    
                case 5:
                    return rightFaceTexture;                    
                default: Debug.Log("How'd you do that? Theres no faceIndex there.");
                    return -1;          
            }
        }
    }

    struct GroundAndSurface
    {
        public Chunk groundChunk;
        public Chunk waterSurfaceChunk;
    }
}
