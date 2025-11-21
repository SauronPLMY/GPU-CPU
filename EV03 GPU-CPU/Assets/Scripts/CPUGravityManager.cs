using UnityEngine;
using System.Collections;

public class CPUGravityManager : MonoBehaviour
{
    [Header("Simulation Parameters")]
    public int shipCount = 100;
    public int planetCount = 10;
    public float areaSize = 50f;
    public float minPlanetRadius = 1f;
    public float maxPlanetRadius = 5f;
    public float minShipSpeed = 1f;
    public float maxShipSpeed = 5f;

    [Header("Prefabs")]
    public GameObject shipPrefab;
    public GameObject planetPrefab;

    // Arrays para almacenar datos de naves y planetas
    private Vector3[] shipPositions;
    private Vector3[] shipVelocities;
    private Vector3[] planetPositions;
    private float[] planetMasses;

    // Referencias a los objetos en escena
    private GameObject[] ships;
    private GameObject[] planets;

    // Constante gravitatoria (la misma que en GPU)
    private float G = 9.8f;

    void Start()
    {
        InitializeSimulation();
        AdjustCameraToSimulationArea();
    }

    void InitializeSimulation()
    {
        // Crear planetas y naves
        CreatePlanets();
        CreateShips();
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

    void Update()
    {
        // Calcular física para cada nave (EN SECUENCIA - CPU)
        CalculatePhysicsCPU();

        // Actualizar posiciones visuales
        UpdateShipTransforms();
    }

    void CalculatePhysicsCPU()
    {
        // PARA CADA NAVECITA (esto es lo que la CPU hace en secuencia)
        for (int shipIndex = 0; shipIndex < shipCount; shipIndex++)
        {
            Vector3 currentPosition = shipPositions[shipIndex];
            Vector3 currentVelocity = shipVelocities[shipIndex];
            
            Vector3 totalForce = Vector3.zero;
            
            // PARA CADA PLANETA (doble loop - esto es lo pesado)
            for (int planetIndex = 0; planetIndex < planetCount; planetIndex++)
            {
                // Vector desde la nave al planeta
                Vector3 toPlanet = planetPositions[planetIndex] - currentPosition;
                float distance = toPlanet.magnitude;
                
                // Evitar división por cero
                if (distance < 0.1f) distance = 0.1f;
                
                // Fuerza gravitatoria (F = G * m1 * m2 / r^2)
                float forceMagnitude = G * planetMasses[planetIndex] / (distance * distance);
                Vector3 forceDirection = toPlanet.normalized;
                
                totalForce += forceDirection * forceMagnitude;
            }
            
            // Integrar velocidad (F = m*a, asumimos m=1)
            Vector3 acceleration = totalForce;
            currentVelocity += acceleration * Time.deltaTime;
            
            // Integrar posición
            currentPosition += currentVelocity * Time.deltaTime;
            
            // Aplicar condiciones de contorno (rebote simple)
            if (currentPosition.x < -areaSize || currentPosition.x > areaSize)
            {
                currentVelocity.x *= -0.5f; // Rebote con pérdida de energía
                currentPosition.x = Mathf.Clamp(currentPosition.x, -areaSize, areaSize);
            }
            
            if (currentPosition.y < -areaSize || currentPosition.y > areaSize)
            {
                currentVelocity.y *= -0.5f;
                currentPosition.y = Mathf.Clamp(currentPosition.y, -areaSize, areaSize);
            }
            
            // Guardar resultados
            shipPositions[shipIndex] = currentPosition;
            shipVelocities[shipIndex] = currentVelocity;
        }
    }

    void UpdateShipTransforms()
    {
        // Actualizar transformadas de las naves
        for (int i = 0; i < shipCount; i++)
        {
            ships[i].transform.position = shipPositions[i];
        }
    }

    void AdjustCameraToSimulationArea()
    {
        Camera mainCamera = Camera.main;
        
        if (mainCamera == null)
        {
            Debug.LogError("No se encontró la cámara principal (Main Camera)");
            return;
        }
        
        // Configurar la cámara para que abarque todo el área de simulación
        mainCamera.orthographic = true;
        
        // Calcular el tamaño de la cámara considerando la relación de aspecto 16:9 (1920x1080)
        float aspectRatio = 16f / 9f;
        float requiredHorizontalSize = areaSize;
        float requiredVerticalSize = areaSize / aspectRatio;
        
        // Usar el mayor valor para asegurar que toda el área sea visible
        float cameraSize = Mathf.Max(requiredHorizontalSize, requiredVerticalSize) * 1.1f;
        
        mainCamera.orthographicSize = cameraSize;
        
        // Posicionar cámara en el centro
        mainCamera.transform.position = new Vector3(0, 0, -10);
        
        Debug.Log($"Cámara ajustada - AreaSize: {areaSize}, OrthoSize: {mainCamera.orthographicSize}");
    }

    void OnDrawGizmos()
    {
        // Dibujar el área de simulación en el Editor
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(areaSize * 2, areaSize * 2, 0));
        
        // Dibujar un punto en el centro para referencia
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(Vector3.zero, 0.3f);
    }
}