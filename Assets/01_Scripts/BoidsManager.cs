using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class BoidsManager : MonoBehaviour
{
    [SerializeField] private int boidsCount = 50;
    [SerializeField] private GameObject boidsPrefab;

    [SerializeField] private float seperationDistance = 2.0f;
    [SerializeField] private float seperationStrenght = 2.0f;

    [SerializeField] private float targetWeight = 100.0f;

    [SerializeField] private float allignmentMultiplier = 0.1f;

    [SerializeField] private Transform target;

    [SerializeField] private float2 minMaxSpeed;

    [SerializeField] private float cohesionStrenght = 2.0f;

    [SerializeField] private float maxSteerForce = 4.0f;

    [SerializeField] private float2 bounds;

    private float chunkDistance = 50.0f;

    private List<Cluster> clusters = new();
    private List<Transform> transforms = new();
    private List<Boid> boids = new();

    [SerializeField] private Gradient colourGradient;

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

        //int count = 0;
        //for ( int i = 0; i < clusters.Count; i++ )
        //{
        //    var cluster = clusters[i];

        //    CalculateFlockData(ref cluster);

        //    Debug.Log(cluster.boids.Count);
        //    foreach ( var boid in cluster.boids )
        //    {
        //        transforms[boid.index].GetComponent<MeshRenderer>().material.color = colourGradient.Evaluate(Mathf.InverseLerp(0.0f, boidsCount, count));
        //    }
        //    count++;
        //}
    }

    //////Optimize That.
    //private async Task CheckClusters ()
    //{
    //    await Awaitable.BackgroundThreadAsync();

    //    for ( int i = 0; i < boids.Count; i++ )
    //    {
    //        var currentBoid = boids[i];

    //        CalculateFlockData(ref currentBoid.currentCluster);

    //        var distanceFirst = Vector3.Distance(currentBoid.currentCluster.flockPosition / currentBoid.currentCluster.boids.Count, currentBoid.position);

    //        if ( distanceFirst > chunkDistance )
    //        {
    //            bool hasPlace = false;
    //            for ( int t = 0; t < clusters.Count; t++ )
    //            {
    //                var cluster = clusters[t];
    //                CalculateFlockData(ref cluster);

    //                var distance = Vector3.Distance(cluster.flockPosition / cluster.boids.Count, currentBoid.position);
    //                if ( distance < chunkDistance )
    //                {
    //                    currentBoid.currentCluster.boids.Remove(currentBoid);
    //                    currentBoid.currentCluster = cluster;
    //                    currentBoid.currentCluster.boids.Add(currentBoid);

    //                    hasPlace = true;
    //                }
    //            }

    //            if ( !hasPlace )
    //            {
    //                currentBoid.currentCluster.boids.Remove(currentBoid);
    //                currentBoid.currentCluster = new Cluster();
    //                currentBoid.currentCluster.boids.Add(currentBoid);
    //                clusters.Add(currentBoid.currentCluster);
    //            }
    //        }
    //    }
    //}

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

    //[BurstCompile]
    //private struct UpdateBoidJob : IJobParallelFor
    //{
    //    public NativeArray<Boid> boids;
    //    [ReadOnly] public Cluster cluster;

    //    [ReadOnly] public float deltaTime;
    //    [ReadOnly] public float2 minMaxSpeed;
    //    [ReadOnly] public float maxSteerForce;
    //    [ReadOnly] public float targetWeight;
    //    [ReadOnly] public float cohesionStrenght;
    //    [ReadOnly] public float allignmentMultiplier;
    //    [ReadOnly] public float seperationStrenght;

    //    [WriteOnly] public NativeArray<Vector3> positions;
    //    [WriteOnly] public NativeArray<Vector3> directions;

    //    public void Dispose ()
    //    {
    //        positions.Dispose();
    //        directions.Dispose();
    //        boids.Dispose();
    //    }

    //    public void Execute ( int index )
    //    {
    //        var boid = cluster.boids[index];
    //        Vector3 acceleration = Vector3.zero;

    //        //if ( target != null )
    //        //{
    //        //    var direction = target.position - boid.position;
    //        //    var offSet = SteerTowards(direction, boid.Velocity) * targetWeight;

    //        //    acceleration += offSet;
    //        //}

    //        if ( cluster.boids.Length != 0 )
    //        {
    //            var flockCentre = cluster.flockPosition / cluster.boids.Length;

    //            Vector3 offSetToFlockCentre = flockCentre - boid.position;

    //            var alignmentForce = SteerTowards(cluster.flockDirection.normalized, boid.Velocity) * allignmentMultiplier;
    //            var cohesionForce = SteerTowards(offSetToFlockCentre, boid.Velocity) * cohesionStrenght;
    //            var seperationForce = SteerTowards(boid.avoidanceHeading, boid.Velocity) * seperationStrenght;

    //            acceleration += alignmentForce + cohesionForce + seperationForce;
    //        }

    //        boid.Velocity += acceleration * Time.deltaTime;

    //        float speed = boid.Velocity.magnitude;
    //        var dir = boid.Velocity / speed;
    //        speed = math.clamp(speed, minMaxSpeed.x, minMaxSpeed.y);
    //        boid.Velocity = dir * speed;

    //        boid.position += boid.Velocity * deltaTime;
    //        boid.forward = dir;

    //        boids[index] = boid;
    //        positions[index] = boid.position;
    //        directions[index] = boid.forward;
    //    }

    //    private readonly Vector3 SteerTowards ( Vector3 vector, Vector3 velocity )
    //    {
    //        Vector3 v = vector.normalized * minMaxSpeed.y - velocity;
    //        return Vector3.ClampMagnitude(v, maxSteerForce);
    //    }
    //}

    private Boid UpdateBoid ( Boid boid, Cluster cluster )
    {
        Vector3 acceleration = Vector3.zero;

        if ( target != null )
        {
            var direction = target.position - boid.position;
            var offSet = SteerTowards(direction, boid.Velocity) * targetWeight;

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
        if ( clusters.Count != 0 )
            Gizmos.DrawCube(clusters[0].flockPosition, Vector3.one * 5.0f);
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
            var boidDirection = boid.position - currentBoid.position;
            var distance = boidDirection.x * boidDirection.x + boidDirection.y * boidDirection.y + boidDirection.z + boidDirection.z;

            if ( distance < seperationDistance * seperationDistance )
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