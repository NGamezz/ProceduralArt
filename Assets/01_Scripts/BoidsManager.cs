using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class SpatialGrid : IDisposable
{
    public int3 gridSize;
    public int cellSize;

    private Cluster[] cells;

    private int size;
    private int numberOfCells;

    public int Get1DIndexFrom3 ( int3 position, int size )
    {
        return Mathf.RoundToInt((float)(position.z * Math.Pow(numberOfCells, 2)) + (position.y * numberOfCells) + position.x);
    }

    public SpatialGrid ( int3 gridSize, int cellSize )
    {
        this.gridSize = gridSize;
        this.cellSize = cellSize;

        numberOfCells = gridSize.x / cellSize;

        size = numberOfCells * numberOfCells * numberOfCells;

        cells = new Cluster[size];

        for ( int i = 0; i < size; i++ )
        {
            cells[i] = new Cluster(1);
        }
    }

    public Cluster[] GetCells ()
    {
        return cells;
    }

    public void SetCells ( Cluster[] cells )
    {
        this.cells = cells;
    }

    public Cluster GetElementsAtPosition ( float3 position )
    {
        int cellX = Mathf.RoundToInt(position.x / cellSize);
        int cellZ = Mathf.RoundToInt(position.z / cellSize);
        int cellY = Mathf.RoundToInt(position.y / cellSize);

        return cells[Get1DIndexFrom3(new(cellX, cellY, cellZ), numberOfCells)];
    }

    public Boid AddElement ( float3 position, Boid boid )
    {
        int x = Mathf.RoundToInt(position.x / cellSize);
        int z = Mathf.RoundToInt(position.z / cellSize);
        int y = Mathf.RoundToInt(position.y / cellSize);

        var cluster = cells[Get1DIndexFrom3(new(x, y, z), numberOfCells)];

        cluster.boids.Add(boid);
        boid.index = Array.IndexOf(cluster.boids.ToArray(), boid);
        cluster.boids[boid.index] = boid;

        cells[Get1DIndexFrom3(new(x, y, z), numberOfCells)] = cluster;

        return boid;
    }

    public void RemoveElement ( float3 position, Boid boid )
    {
        var currentboid = boid;

        int cellX = (int)(position.x / cellSize);
        int cellZ = (int)(position.z / cellSize);
        int cellY = (int)(position.y / cellSize);

        cells[Get1DIndexFrom3(new(cellX, cellY, cellZ), numberOfCells)].boids.RemoveAt(currentboid.index);
    }

    public void Dispose ()
    {
        foreach ( var cell in cells )
        {
            cell.Dispose();
        }
    }
}

public class BoidsManager : MonoBehaviour
{
    [SerializeField] private int boidsCount = 1000;
    [SerializeField] private List<GameObject> boidsPrefab;

    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 targetVelocity = Vector3.zero;
    private bool controllerActive = false;

    [SerializeField] private float vrControllerWeight = 10.0f;
    [SerializeField] private float vrControllerVelocityPersistance = 2.0f;

    [SerializeField] private float fovOfTarget = 70;

    [SerializeField] private Transform anchor;

    [Header("Boids Pars")]
    [SerializeField] private float seperationDistance = 2.0f;
    [SerializeField] private float seperationStrenght = 2.0f;

    [SerializeField] private float targetWeight = 100.0f;

    [SerializeField] private float allignmentMultiplier = 0.1f;

    [SerializeField] private float2 minMaxSpeed;

    [SerializeField] private float cohesionStrenght = 2.0f;

    [SerializeField] private float maxSteerForce = 4.0f;

    [SerializeField] private float boidViewDistance = 25.0f;

    [SerializeField] private float2 bounds;

    [SerializeField] private float distanceToCentre = 50.0f;
    [SerializeField] private float returnStrenght = 100.0f;

    [SerializeField] private float2 scaleBounds;

    [SerializeField] private int3 gridSize;
    [SerializeField] private int cellSize;

    private List<Cluster> clusters = new();
    private List<Transform> transforms = new();
    private NativeList<Boid> boids;

    [SerializeField] private Gradient colourGradient;

    public void SetTarget ( Transform newTarget, bool controller )
    {
        StopAllCoroutines();

        target = newTarget;

        if ( controllerActive && newTarget == null )
        {
            StartCoroutine(nameof(ResetControllerVelocity));
        }

        controllerActive = controller;
    }

    public void SetTargetVelocity ( Vector3 vel )
    {
        targetVelocity = vel;
    }

    public Transform ReturnTarget ()
    {
        return target;
    }

    private void Start ()
    {
        boids = new(boidsCount, Allocator.Persistent);

        Cluster defaultCluster = new(0);
        for ( int i = 0; i < boidsCount; i++ )
        {
            var gameObject = Instantiate(boidsPrefab[UnityEngine.Random.Range(0, boidsPrefab.Count)], transform);
            var position = UnityEngine.Random.insideUnitSphere.normalized * UnityEngine.Random.Range(bounds.x, bounds.y);
            gameObject.transform.position = position;
            transforms.Add(gameObject.transform);

            var boid = new Boid(i, position, gameObject.transform.forward);

            defaultCluster.boids.Add(boid);

            var size = UnityEngine.Random.Range(scaleBounds.x, scaleBounds.y);
            transforms[i].localScale = Vector3.one * size;

            boids.Add(boid);
        }

        defaultCluster = CalculateFlockData(defaultCluster);
        clusters.Add(defaultCluster);
        //for ( int i = 0; i < boidsCount; i++ )
        //{
        //    //var meshRenderer = transforms[i].GetComponent<MeshRenderer>();
        //    //var material = meshRenderer.material;

        //    //material.EnableKeyword("_EMISSION");
        //    //material.SetColor("_EmissionColor", colourGradient.Evaluate(Mathf.InverseLerp(0.0f, boidsCount, i)));
        //    //meshRenderer.material = material;
        //}
    }

    [BurstCompile]
    private void UpdateVisible ()
    {
        if ( anchor == null )
            return;

        Vector3 playerPosition = anchor.position;
        Vector3 playerForward = anchor.forward;
        foreach ( var boid in boids )
        {
            var direction = boid.position - playerPosition;

            float angle = Vector3.Angle(direction, playerForward);

            if ( angle >= -fovOfTarget && angle <= fovOfTarget )
            {
                transforms[boid.index].gameObject.SetActive(true);
            }
            else
            {
                transforms[boid.index].gameObject.SetActive(false);
            }
        }
    }

    private void FixedUpdate ()
    {
        //UpdateVisible();
    }

    private void Update ()
    {
        UpdateClusters();
    }

    private void UpdateClusters ()
    {
        for ( int i = 0; i < clusters.Count; i++ )
        {
            var updated = UpdateCluster(clusters[i], i);
            clusters[i] = updated;
        }
    }

    [BurstCompile]
    private Cluster CalculateFlockData ( Cluster cluster )
    {
        NativeArray<Boid> boids = new(cluster.boids.Length, Allocator.TempJob);

        CalculateClusterData calculateClusterData = new()
        {
            seperationDistance = seperationDistance,
            boids = boids,
            viewDistance = boidViewDistance,
            cluster = cluster,
        };

        calculateClusterData.Schedule(cluster.boids.Length, 64).Complete();

        Cluster newCluster = new(0);
        newCluster.boids.AddRange(calculateClusterData.boids);

        calculateClusterData.Dispose();
        cluster.Dispose();

        return newCluster;
    }

    private IEnumerator ResetControllerVelocity ()
    {
        yield return new WaitForSeconds(vrControllerVelocityPersistance);

        targetVelocity = Vector3.zero;
    }

    private void OnDisable ()
    {
        //spatialGrid.Dispose();

        foreach ( var cluster in clusters )
        {
            cluster.Dispose();
        }
        boids.Dispose();
    }

    [BurstCompile]
    private Cluster UpdateCluster ( Cluster cluster, int index )
    {
        var newCluster = CalculateFlockData(cluster);

        NativeArray<Boid> boids = new(newCluster.boids.Length, Allocator.TempJob);

        UpdateBoids updateBoids = new()
        {
            cluster = newCluster,
            boids = boids,
            anchorPosition = anchor.position,
            distanceToCentre = distanceToCentre,
            targetPosition = target == null ? Vector3.zero : target.position,
            maxSpeed = minMaxSpeed.y,
            minSpeed = minMaxSpeed.x,
            targetVelocity = targetVelocity,
            maxSteerForce = maxSteerForce,
            deltaTime = Time.deltaTime,
            targetWeight = targetWeight,
            vrControllerWeight = vrControllerWeight,
            returnStrength = returnStrenght,
            allignmentMultiplier = allignmentMultiplier,
            cohesionStrenght = cohesionStrenght,
            seperationStrenght = seperationStrenght
        };

        updateBoids.Schedule(newCluster.boids.Length, 64).Complete();

        for ( int i = 0; i < newCluster.boids.Length; i++ )
        {
            var boid = updateBoids.boids[i];
            newCluster.boids[i] = boid;

            var transform = transforms[boid.index];
            transform.position = boid.position;
            transform.forward = boid.forward == Vector3.zero ? transform.forward : boid.forward;
        }

        updateBoids.boids.Dispose();
        return newCluster;
    }

    private void OnDrawGizmos ()
    {
        Color color = Color.white;
        color.a = 0.1f;
        Gizmos.color = color;
        Gizmos.DrawSphere(anchor.position, distanceToCentre);
    }
}

[BurstCompile(FloatPrecision.Medium, FloatMode.Fast)]
public struct UpdateBoids : IJobParallelFor, IDisposable
{
    [ReadOnly] public Cluster cluster;
    [WriteOnly] public NativeArray<Boid> boids;
    [ReadOnly] public Vector3 anchorPosition;

    [ReadOnly] public Vector3 targetPosition;

    [ReadOnly] public Vector3 targetVelocity;

    [ReadOnly] public float distanceToCentre;

    [ReadOnly] public float maxSteerForce;
    [ReadOnly] public float maxSpeed;
    [ReadOnly] public float minSpeed;

    [ReadOnly] public float deltaTime;

    [ReadOnly] public float targetWeight;
    [ReadOnly] public float returnStrength;
    [ReadOnly] public float vrControllerWeight;
    [ReadOnly] public float allignmentMultiplier;
    [ReadOnly] public float cohesionStrenght;
    [ReadOnly] public float seperationStrenght;

    public void Dispose ()
    {
        boids.Dispose();
    }

    [BurstCompatible]
    public void Execute ( int index )
    {
        Vector3 acceleration = Vector3.zero;

        var boid = cluster.boids[index];

        if ( targetPosition != Vector3.zero )
        {
            var directionTo = targetPosition - boid.position;
            var offSet = SteerTowards(directionTo, boid.Velocity) * targetWeight;

            acceleration += offSet;
        }

        if ( distanceToCentre > 0 && Vector3.Distance(boid.position, anchorPosition) > distanceToCentre )
        {
            var directionTo = anchorPosition - boid.position;
            var offSet = SteerTowards(directionTo, boid.Velocity) * returnStrength;

            acceleration += offSet;
        }

        if ( targetVelocity != Vector3.zero )
        {
            var offSet = SteerTowards(targetVelocity, boid.Velocity) * vrControllerWeight;
            acceleration += offSet;
        }

        if ( boid.flockMates != 0 )
        {
            var flockCentre = boid.flockPosition / boid.flockMates;

            Vector3 offSetToFlockCentre = flockCentre - boid.position;

            var alignmentForce = SteerTowards(boid.flockDirection.normalized, boid.Velocity) * allignmentMultiplier;
            var cohesionForce = SteerTowards(offSetToFlockCentre, boid.Velocity) * cohesionStrenght;
            var seperationForce = SteerTowards(boid.avoidanceHeading, boid.Velocity) * seperationStrenght;

            acceleration += alignmentForce + cohesionForce + seperationForce;
        }

        boid.Velocity += acceleration * deltaTime;

        //Clamp Speed.
        float speed = boid.Velocity.magnitude;
        var direction = boid.Velocity.normalized;
        speed = Mathf.Clamp(speed, minSpeed, maxSpeed);
        boid.Velocity = boid.Velocity.normalized * speed;
        boid.position += boid.Velocity * deltaTime;
        boid.forward = direction;

        boid.flockMates = 0;
        boid.flockDirection = Vector3.zero;
        boid.flockPosition = Vector3.zero;

        boids[index] = boid;
    }

    [BurstCompatible]
    private readonly Vector3 SteerTowards ( Vector3 vector, Vector3 velocity )
    {
        Vector3 v = vector.normalized * maxSpeed - velocity;
        return Vector3.ClampMagnitude(v, maxSteerForce);
    }
}

[BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
public struct CalculateClusterData : IJobParallelFor, IDisposable
{
    [ReadOnly] public Cluster cluster;
    [WriteOnly] public NativeArray<Boid> boids;
    [ReadOnly] public float seperationDistance;
    [ReadOnly] public float viewDistance;

    public void Dispose ()
    {
        boids.Dispose();
    }

    [BurstCompatible]
    public void Execute ( int index )
    {
        var boid = cluster.boids[index];
        for ( int t = 0; t < cluster.boids.Length; t++ )
        {
            if ( index == t )
                continue;

            var secondBoid = cluster.boids[t];
            var boidDirection = secondBoid.position - boid.position;
            var distance = boidDirection.sqrMagnitude;

            if ( distance < viewDistance * viewDistance )
            {
                boid.flockPosition += secondBoid.forward;
                boid.flockMates++;
                boid.flockPosition += secondBoid.position;
            }

            if ( distance < seperationDistance * seperationDistance )
            {
                boid.avoidanceHeading -= boidDirection / distance;
            }
        }
        boids[index] = boid;
    }
}

[BurstCompatible]
public struct Cluster : IDisposable
{
    public NativeList<Boid> boids;

    public Cluster ( int _ )
    {
        boids = new(Allocator.Persistent);
    }

    public void Dispose ()
    {
        boids.Dispose();
    }
}

[BurstCompatible]
public struct Boid
{
    public int index;

    public Vector3 forward;

    public Vector3 avoidanceHeading;

    public Vector3 position;

    public Vector3 flockDirection;

    public int flockMates;

    public Vector3 flockPosition;

    public Vector3 Velocity;

    public Boid ( int index, Vector3 position, Vector3 forward )
    {
        flockMates = 0;
        this.index = index;
        this.position = position;
        Velocity = Vector3.zero;
        this.forward = forward;
        avoidanceHeading = Vector3.zero;
        flockDirection = Vector3.zero;
        flockPosition = Vector3.zero;
    }
}