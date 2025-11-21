using UnityEngine;
using System.Collections;

public class GPUGravityManager : MonoBehaviour
{
    [Header("Simulation Parameters")]
    public int shipCount = 100;
    public int planetCount = 10;
    public float areaSize = 50f;
    public float minPlanetRadius = 1f;
    public float maxPlanetRadius = 5f;
    public float minShipSpeed = 1f;
    public float maxShipSpeed = 5f;

    [Header("Compute Shader")]
    public ComputeShader gravityComputeShader;

    [Header("Prefabs")]
    public GameObject shipPrefab;
    public GameObject planetPrefab;

    // Buffers de datos para la GPU
    private ComputeBuffer shipPositionBuffer;
    private ComputeBuffer shipVelocityBuffer;
    private ComputeBuffer planetPositionBuffer;
    private ComputeBuffer planetMassBuffer;

    // Arrays para mantener datos en CPU
    private Vector3[] shipPositions;
    private Vector3[] shipVelocities;
    private Vector3[] planetPositions;
    private float[] planetMasses;

    // Referencias a los objetos en escena
    private GameObject[] ships;
    private GameObject[] planets;

    // Kernel del Compute Shader
    private int gravityKernel;

    void Start()
    {
        InitializeSimulation();
        AdjustCameraToSimulationArea(); // Llamar después de inicializar la simulación
    }

    void InitializeSimulation()
    {
        // Crear planetas y naves
        CreatePlanets();
        CreateShips();

        // Inicializar buffers
        InitializeBuffers();

        // Encontrar el kernel del Compute Shader
        gravityKernel = gravityComputeShader.FindKernel("CSMain");
    }

    void CreatePlanets()
    {
        planets = new GameObject[planetCount];
        planetPositions = new Vector3[planetCount];
        planetMasses = new float[planetCount];

        for (int i = 0; i < planetCount; i++)
        {
            // Posición aleatoria
            Vector3 randomPos = new Vector3(
                Random.Range(-areaSize, areaSize),
                Random.Range(-areaSize, areaSize),
                0
            );

            // Radio aleatorio y masa proporcional al radio
            float radius = Random.Range(minPlanetRadius, maxPlanetRadius);
            float mass = radius * radius; // Masa proporcional al área

            // Instanciar planeta
            planets[i] = Instantiate(planetPrefab, randomPos, Quaternion.identity);
            planets[i].transform.localScale = Vector3.one * radius * 2f; // Diámetro visual

            // Guardar datos
            planetPositions[i] = randomPos;
            planetMasses[i] = mass;
        }
    }

    void CreateShips()
    {
        ships = new GameObject[shipCount];
        shipPositions = new Vector3[shipCount];
        shipVelocities = new Vector3[shipCount];

        for (int i = 0; i < shipCount; i++)
        {
            // Posición aleatoria
            Vector3 randomPos = new Vector3(
                Random.Range(-areaSize, areaSize),
                Random.Range(-areaSize, areaSize),
                0
            );

            // Velocidad aleatoria
            Vector3 randomVel = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f),
                0
            ).normalized * Random.Range(minShipSpeed, maxShipSpeed);

            // Instanciar nave
            ships[i] = Instantiate(shipPrefab, randomPos, Quaternion.identity);

            // Guardar datos
            shipPositions[i] = randomPos;
            shipVelocities[i] = randomVel;
        }
    }

    void InitializeBuffers()
    {
        // Crear buffers para la GPU
        shipPositionBuffer = new ComputeBuffer(shipCount, sizeof(float) * 3);
        shipVelocityBuffer = new ComputeBuffer(shipCount, sizeof(float) * 3);
        planetPositionBuffer = new ComputeBuffer(planetCount, sizeof(float) * 3);
        planetMassBuffer = new ComputeBuffer(planetCount, sizeof(float));

        // Poblar buffers con datos iniciales
        shipPositionBuffer.SetData(shipPositions);
        shipVelocityBuffer.SetData(shipVelocities);
        planetPositionBuffer.SetData(planetPositions);
        planetMassBuffer.SetData(planetMasses);
    }

    void Update()
    {
        // Ejecutar el Compute Shader cada frame
        ExecuteComputeShader();

        // Leer resultados de vuelta a CPU y actualizar posiciones visuales
        UpdateShipTransforms();
    }

    void ExecuteComputeShader()
    {
        // Establecer los buffers en el Compute Shader
        gravityComputeShader.SetBuffer(gravityKernel, "ShipPositions", shipPositionBuffer);
        gravityComputeShader.SetBuffer(gravityKernel, "ShipVelocities", shipVelocityBuffer);
        gravityComputeShader.SetBuffer(gravityKernel, "PlanetPositions", planetPositionBuffer);
        gravityComputeShader.SetBuffer(gravityKernel, "PlanetMasses", planetMassBuffer);

        // Pasar parámetros uniformes
        gravityComputeShader.SetInt("planetCount", planetCount);
        gravityComputeShader.SetFloat("deltaTime", Time.deltaTime);
        gravityComputeShader.SetFloat("areaSize", areaSize);

        // Ejecutar el Compute Shader (1 thread por nave)
        gravityComputeShader.Dispatch(gravityKernel, Mathf.CeilToInt(shipCount / 64f), 1, 1);
    }

    void UpdateShipTransforms()
    {
        // Leer posiciones actualizadas desde la GPU
        shipPositionBuffer.GetData(shipPositions);

        // Actualizar transformadas de las naves
        for (int i = 0; i < shipCount; i++)
        {
            ships[i].transform.position = shipPositions[i];
        }
    }

    void OnDestroy()
    {
        // Liberar buffers cuando se destruya el objeto
        shipPositionBuffer?.Release();
        shipVelocityBuffer?.Release();
        planetPositionBuffer?.Release();
        planetMassBuffer?.Release();
    }

    void AdjustCameraToSimulationArea() //Metodo para ajustar bien la escena en la pantalla
    {
        Camera mainCamera = Camera.main;
        
        // Asegurarse de que la cámara existe
        if (mainCamera == null)
        {
            Debug.LogError("No se encontró la cámara principal (Main Camera)");
            return;
        }
        
        // Configurar la cámara para que abarque todo el área de simulación
        mainCamera.orthographic = true;
        float aspectRatio = 16f / 9f; //Aspecto Full HD
        float requiredHorizontalSize = areaSize; // Tamaño necesario en horizontal
        float requiredVerticalSize = areaSize / aspectRatio; // Tamaño necesario en vertical
        
        // Usar el mayor valor para asegurar que toda el área sea visible
        float cameraSize = Mathf.Max(requiredHorizontalSize, requiredVerticalSize) * 1.1f;
        
        mainCamera.orthographicSize = cameraSize;
        
        // Posicionar cámara en el centro
        mainCamera.transform.position = new Vector3(0, 0, -10);
        
        Debug.Log($"Cámara ajustada - AreaSize: {areaSize}, OrthoSize: {mainCamera.orthographicSize}");
    }

    void OnDrawGizmos() //Esto dibuja el area de simulación en la escena como referencia 
    {
        // Dibujar el área de simulación
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(areaSize * 2, areaSize * 2, 0));
        
        // Dibujar un punto en el centro para referencia
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(Vector3.zero, 0.3f);
    }
}