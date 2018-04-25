using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwarmController : MonoBehaviour {

    public float maxSpeed;
    public float acceleration;

    private Vector2 velocity;

    private FlowFieldSolver ffs;
    private CharacterController cc;

	// Use this for initialization
	void Start () {
        velocity = new Vector2();

        Object[] srch = Object.FindObjectsOfType<FlowFieldSolver>();

        cc = GetComponent<CharacterController>();

        if (srch.Length > 0)
            ffs = (FlowFieldSolver)srch[0];
        else
            Debug.LogError("FlowFieldSolver NOT FOUND");
	}
	
	// Update is called once per frame
	void Update () {
        Vector2Int dir = ffs.GetFlow(transform.position.x, transform.position.z);

        velocity.x += dir.x * acceleration;
        velocity.y += dir.y * acceleration;

        if (velocity.SqrMagnitude() > maxSpeed * maxSpeed)
        {
            velocity.Normalize();
            velocity *= maxSpeed;
        }

        cc.Move(new Vector3(velocity.x / 100.0f, 0.0f, velocity.y / 100.0f));
    }
}
