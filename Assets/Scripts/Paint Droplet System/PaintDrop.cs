using UnityEngine;
using System;

[RequireComponent(typeof(MeshRenderer))] 
public class PaintDrop : MonoBehaviour
{
    [Header("Physics")]
    public float gravity = 9.81f;
    public float dropletDiameter = 0.01f;      
    public float density = 1200f;              
    public float surfaceTension = 0.03f;       
    public float criticalWeber = 10f;          
    public static Vector3 WindForce = Vector3.zero; 
    private Vector3 velocity;
    private Vector3 startPos;
    private float timeSinceLaunch;
    private bool isActive;

    public event Action<Vector3, float, float, float, bool> OnImpact; 
    

    public void Launch(Vector3 startPosition, Vector3 initialVelocity)
    {
        Debug.Log($"PaintDrop.Launch() called! Pos: {startPosition}, Vel: {initialVelocity}"); 
        transform.position = startPosition;
        velocity = initialVelocity;
        startPos = startPosition;
        timeSinceLaunch = 0f;
        isActive = true;
        gameObject.SetActive(true);
    }

    private void Update()
    {
         
        Debug.Log($"PaintDrop.Update() running, Y={transform.position.y}");
        if (!isActive) return;

        velocity.y -= gravity * Time.deltaTime;

        
        velocity.x += WindForce.x * Time.deltaTime;
        velocity.z += WindForce.z * Time.deltaTime;


        transform.position += velocity * Time.deltaTime;

        if (transform.position.y <= 0f)
        {
            Debug.Log($"DROP HIT GROUND at {transform.position} !!!");

            float speed = velocity.magnitude;
            Vector3 horizontalVel = new Vector3(velocity.x, 0f, velocity.z);
            float impactAngle = Vector3.Angle(-Vector3.up, velocity.normalized);
            float mass = density * (4f / 3f) * Mathf.PI * Mathf.Pow(dropletDiameter / 2f, 3f);
            float kineticEnergy = 0.5f * mass * speed * speed;
            float weber = (density * speed * speed * dropletDiameter) / surfaceTension;
            bool splatter = weber > criticalWeber;

            Vector3 hitPoint = transform.position;
            hitPoint.y = 0f;
            OnImpact?.Invoke(hitPoint, speed, impactAngle, kineticEnergy, splatter);

            
            isActive = false;
            velocity = Vector3.zero;
            
        }
    }
    private void OnDisable()
    {
        isActive = false;
        OnImpact = null; 
    }
}