using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class BoidsManager : MonoBehaviour
{
    [SerializeField] private int boidsCount = 50;
    [SerializeField] private GameObject boidsPrefab;

    [SerializeField] private float2 bounds;

    private List<Cluster> clusters = new();
    private List<Boid> boids = new();

    void Start()
    {
        for(int i=0; i < boidsCount; i++ )
        {
            var gameObject = Instantiate(boidsPrefab, transform);
            gameObject.transform.position = UnityEngine.Random.insideUnitSphere.normalized * UnityEngine.Random.Range(bounds.x, bounds.y);
            ;
            boids.Add(new());
        }
    }

    private Vector3 GetAverageVelocity  (Cluster cluster)
    {
        Vector3 velocity = Vector3.zero;

        foreach(var boid in cluster.boids)
        {
            velocity += boid.Velocity;
        }

        return velocity;
    }

    private void CheckClusters()
    {
        foreach(var boid in boids)
        {

        }
    }

    private bool CheckWithinRange()
    {

    }

    void Update()
    {
        
    }
}

public class Cluster
{
    public List<Boid> boids;
}

public struct Boid
{
    public int index;

    public Vector3 position;

    public Vector3 Velocity;

    public Boid(int index, Vector3 position)
    {
        this.index = index;
        this.position = position;
        Velocity = Vector3.zero;
    }
}
