using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class BoidsManager : MonoBehaviour
{
    [SerializeField] private int boidsCount = 50;
    [SerializeField] private GameObject boidsPrefab;

    [Header("Target")]
    [SerializeField] private Transform target;
    private Vector3 targetVelocity = Vector3.zero;
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

    [SerializeField] private float2 bounds;

    [SerializeField] private float distanceToCentre = 50.0f;
    [SerializeField] private float returnStrenght = 100.0f;

    [SerializeField] private float2 scaleBounds;

    private List<Cluster> clusters = new();
    private List<Transform> transforms = new();
    private List<Boid> boids = new();

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

    public void SetVelocity ( Vector3 velocity )
    {
        targetVelocity = velocity;
    }

    public Transform ReturnTarget ()
    {
        return target;
    }

    private void Start ()
    {
        Cluster defaultCluster = new(0);
        for ( int i = 0; i < boidsCount; i++ )
        {
            var gameObject = Instantiate(boidsPrefab, transform);
            var position = UnityEngine.Random.insideUnitSphere.normalized * UnityEngine.Random.Range(bounds.x, bounds.y);
            gameObject.transform.position = position;
            transforms.Add(gameObject.transform);

            var boid = new Boid(i, position, gameObject.transform.forward);

            defaultCluster.boids.Add(boid);
            boids.Add(boid);
        }

        defaultCluster = CalculateFlockData(defaultCluster);
        clusters.Add(defaultCluster);

        //await CheckClusters();

        for ( int i = 0; i < boidsCount; i++ )
        {
            var meshRenderer = transforms[i].GetComponent<MeshRenderer>();
            var material = meshRenderer.material;

            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", colourGradient.Evaluate(Mathf.InverseLerp(0.0f, boidsCount, i)));
            meshRenderer.material = material;

            var size = UnityEngine.Random.Range(scaleBounds.x, scaleBounds.y);

            transforms[i].localScale = Vector3.one * size;
        }
    }

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
        UpdateVisible();
    }

    private void UpdateClusters ()
    {
        for ( int i = 0; i < clusters.Count; i++ )
        {
            UpdateCluster(clusters[i]);
        }
    }

    private void Update ()
    {
        UpdateClusters();
    }

    [BurstCompile]
    private Cluster CalculateFlockData ( Cluster cluster )
    {
        NativeArray<Vector3> positions = new(cluster.boids.Length, Allocator.TempJob);
        NativeArray<Vector3> directions = new(cluster.boids.Length, Allocator.TempJob);
        NativeArray<Boid> boids = new(cluster.boids.Length, Allocator.TempJob);

        CalculateClusterData calculateClusterData = new()
        {
            seperationDistance = seperationDistance,
            boids = boids,
            position = positions,
            direction = directions,
            cluster = cluster
        };

        calculateClusterData.Schedule(cluster.boids.Length, 64).Complete();

        Cluster newCluster = new(0);
        newCluster.boids.AddRange(calculateClusterData.boids);

        for ( int i = 0; i < calculateClusterData.boids.Length; i++ )
        {
            newCluster.flockPosition += calculateClusterData.position[i];
            newCluster.flockDirection += calculateClusterData.direction[i];
        }

        calculateClusterData.boids.Dispose();
        calculateClusterData.direction.Dispose();
        calculateClusterData.position.Dispose();
        cluster.boids.Dispose();

        return newCluster;
    }

    private IEnumerator ResetControllerVelocity ()
    {
        yield return new WaitForSeconds(vrControllerVelocityPersistance);

        targetVelocity = Vector3.zero;
    }

    private void OnDisable ()
    {
        foreach ( var cluster in clusters )
        {
            cluster.boids.Dispose();
        }
    }

    [BurstCompile]
    private void UpdateCluster ( Cluster cluster )
    {
        clusters.Remove(cluster);
        cluster = CalculateFlockData(cluster);
        for ( int i = 0; i < cluster.boids.Length; i++ )
        {
            cluster.boids[i] = UpdateBoid(cluster.boids[i], cluster);
        }
        clusters.Add(cluster);
    }

    private Boid UpdateBoid ( Boid boid, Cluster cluster )
    {
        Vector3 acceleration = Vector3.zero;

        if ( target != null )
        {
            var direction = target.position - boid.position;
            var offSet = SteerTowards(direction, boid.Velocity) * targetWeight;

            acceleration += offSet;
        }

        if ( targetVelocity != Vector3.zero )
        {
            var offSet = SteerTowards(targetVelocity, boid.Velocity) * vrControllerWeight;

            acceleration += offSet;
        }

        if ( anchor != null && distanceToCentre > 0 && Vector3.Distance(boid.position, this.anchor.position) > distanceToCentre )
        {
            var direction = anchor.position - boid.position;
            var offSet = SteerTowards(direction, boid.Velocity) * returnStrenght;

            acceleration += offSet;
        }

        if ( cluster.boids.Length != 0 )
        {
            var flockCentre = cluster.flockPosition / cluster.boids.Length;

            Vector3 offSetToFlockCentre = flockCentre - boid.position;

            var alignmentForce = SteerTowards(cluster.flockDirection.normalized, boid.Velocity) * allignmentMultiplier;
            var cohesionForce = SteerTowards(offSetToFlockCentre, boid.Velocity) * cohesionStrenght;
            var seperationForce = SteerTowards(boid.avoidanceHeading, boid.Velocity) * seperationStrenght;

            acceleration += alignmentForce + cohesionForce + seperationForce;
        }

        boid.Velocity += acceleration * Time.deltaTime;

        float speed = boid.Velocity.magnitude;
        var dir = boid.Velocity / speed;
        speed = Mathf.Clamp(speed, minMaxSpeed.x, minMaxSpeed.y);
        boid.Velocity = dir * speed;

        var transform = transforms[boid.index];

        transform.position += boid.Velocity * Time.deltaTime;
        transform.forward = dir;
        boid.position = transform.position;
        boid.forward = dir;

        return boid;
    }

    private void OnDrawGizmos ()
    {
        Color color = Color.white;
        color.a = 0.1f;
        Gizmos.color = color;
        Gizmos.DrawSphere(anchor.position, distanceToCentre);

        color.a = 1;
        Gizmos.color = color;
        if ( clusters.Count > 0 )
            Gizmos.DrawCube(clusters[0].flockPosition / clusters[0].boids.Length, Vector3.one * 10.0f);
    }

    private Vector3 SteerTowards ( Vector3 vector, Vector3 velocity )
    {
        Vector3 v = vector.normalized * minMaxSpeed.y - velocity;
        return Vector3.ClampMagnitude(v, maxSteerForce);
    }
}

[BurstCompile]
public struct CalculateClusterData : IJobParallelFor
{
    [ReadOnly] public Cluster cluster;
    public NativeArray<Boid> boids;
    public float seperationDistance;

    public NativeArray<Vector3> direction;
    public NativeArray<Vector3> position;

    public void Execute ( int index )
    {
        var boid = cluster.boids[index];

        direction[index] += cluster.boids[index].forward;
        position[index] += cluster.boids[index].position;

        for ( int t = 0; t < cluster.boids.Length; t++ )
        {
            var currentBoid = cluster.boids[t];
            if ( boid.index == currentBoid.index )
                continue;

            var boidDirection = currentBoid.position - boid.position;
            var distance = boidDirection.magnitude;

            if ( distance < seperationDistance * seperationDistance)
            {
                boid.avoidanceHeading -= boidDirection / distance;
            }
        }
        boids[index] = boid;
    }
}

public struct Cluster
{
    public NativeList<Boid> boids;

    public Vector3 flockDirection;

    public Vector3 flockPosition;

    public Cluster ( int _ )
    {
        flockDirection = Vector3.zero;
        boids = new(Allocator.Persistent);
        flockPosition = Vector3.zero;
    }
}

public struct Boid
{
    public int index;

    public Vector3 forward;

    public Vector3 avoidanceHeading;

    public Vector3 position;

    public Vector3 Velocity;

    public Boid ( int index, Vector3 position, Vector3 forward )
    {
        this.index = index;
        this.position = position;
        Velocity = Vector3.zero;
        this.forward = forward;
        avoidanceHeading = Vector3.zero;
    }
}