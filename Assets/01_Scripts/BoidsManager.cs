using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

public class BoidsManager : MonoBehaviour
{
    [SerializeField] private int boidsCount = 50;
    [SerializeField] private GameObject boidsPrefab;

    [SerializeField] private float2 bounds;

    private float seperationDistance = 10.0f;

    private List<Cluster> clusters = new();
    private List<Transform> transforms = new();
    private List<Boid> boids = new();

    [SerializeField] private Gradient colourGradient;

    private async void Start()
    {
        Cluster defaultCluster = new Cluster();

        clusters.Add(defaultCluster);

        for ( int i = 0; i < boidsCount; i++ )
        {
            var gameObject = Instantiate(boidsPrefab, transform);
            var position = UnityEngine.Random.insideUnitSphere.normalized * UnityEngine.Random.Range(bounds.x, bounds.y);
            gameObject.transform.position = position;
            transforms.Add(gameObject.transform);

            var boid = new Boid(i, position, defaultCluster);

            defaultCluster.boids.Add(boid);
            boids.Add(boid);
        }

        await CheckClusters();

        int count = 0;
        foreach ( var cluster in clusters )
        {
            Debug.Log(cluster.boids.Count);
            foreach(var boid in cluster.boids)
            {
                transforms[boid.index].GetComponent<MeshRenderer>().material.color = colourGradient.Evaluate(Mathf.InverseLerp(0.0f, boidsCount, count));
            }
            count++;
        }
    }

    private Vector3 GetAveragePosition ( Cluster cluster )
    {
        Vector3 velocity = Vector3.zero;

        foreach ( var boid in cluster.boids )
        {
            velocity += boid.position;
        }

        return velocity / cluster.boids.Count;
    }

    private Vector3 GetAverageVelocity ( Cluster cluster )
    {
        Vector3 velocity = Vector3.zero;

        foreach ( var boid in cluster.boids )
        {
            velocity += boid.Velocity;
        }

        return velocity / cluster.boids.Count;
    }

    //Optimize That.
    private async Task CheckClusters ()
    {
        await Awaitable.BackgroundThreadAsync();

        for ( int i = 0; i < boids.Count; i++ )
        {
            var currentBoid = boids[i];

            var distanceFirst = Vector3.Distance(GetAveragePosition(currentBoid.currentCluster), currentBoid.position);
            if ( distanceFirst > seperationDistance )
            {
                bool hasPlace = false;
                foreach ( var cluster in clusters )
                {
                    var distance = Vector3.Distance(GetAveragePosition(cluster), currentBoid.position);
                    if ( distance < seperationDistance )
                    {
                        currentBoid.currentCluster.boids.Remove(currentBoid);
                        currentBoid.currentCluster = cluster;
                        currentBoid.currentCluster.boids.Add(currentBoid);

                        hasPlace = true;
                    }
                }

                if ( !hasPlace )
                {
                    currentBoid.currentCluster.boids.Remove(currentBoid);
                    currentBoid.currentCluster = new Cluster();
                    currentBoid.currentCluster.boids.Add(currentBoid);
                    clusters.Add(currentBoid.currentCluster);
                }
            }
        }
    }

    private async void UpdateClusterCallBack(Cluster cluster)
    {
        await Awaitable.MainThreadAsync();

        foreach(var boid in cluster.boids)
        {
            transforms[boid.index].position = boid.position;
        }
    }

    private void UpdateCluster(Cluster cluster, Action<Cluster> updateCallBack)
    {
        updateCallBack?.Invoke(cluster);
    }
}

public class Cluster
{
    public List<Boid> boids = new();
}

public struct Boid
{
    public int index;

    public Cluster currentCluster;

    public Vector3 position;

    public Vector3 Velocity;

    public Boid ( int index, Vector3 position, Cluster defaultCluster )
    {
        this.index = index;
        this.position = position;
        Velocity = Vector3.zero;
        currentCluster = defaultCluster;
    }
}
