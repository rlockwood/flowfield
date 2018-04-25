using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClickToSwarm : MonoBehaviour {

    public GameObject field;
    public FlowFieldSolver ffs;
    public int width;
    public int height;
    
    private Dictionary<Vector2Int, bool> edits;

    private bool drawType;

    private RaycastHit hit;
    private Ray ray;

    // Use this for initialization
    void Start () {
        
	}
	
	// Update is called once per frame
	void Update () {
		if(Input.GetMouseButtonUp(0))
        {
            Debug.Log("A");
            ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if(Physics.Raycast(ray, out hit, 100.0f))
            {
                Debug.Log("hit " + hit.collider.gameObject.name);
                //if(hit.collider.gameObject == field)
                //{
                    Debug.Log((int)hit.point.x + ", " + (int)hit.point.z);
                    ffs.NewDest((int)hit.point.x, (int)hit.point.z);
                //}
            }
        }


        if(Input.GetMouseButtonDown(1))
        {
            Debug.Log("B");
            edits = new Dictionary<Vector2Int, bool>();
            ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out hit, 100.0f))
            {
                //if (hit.collider.gameObject == field)
                //{
                    Debug.Log((int)hit.point.x + ", " + (int)hit.point.z);
                    drawType = ffs.GetDifficulty((int)hit.point.x, (int)hit.point.z) != 0;
                //}
            }
        }

        if(Input.GetMouseButton(1))
        {
            Debug.Log("C");

            ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out hit, 100.0f))
            {
                //if (hit.collider.gameObject == field)
                //{
                    //obstacles[(int)hit.point.x, (int)hit.point.z].SetActive(drawType);
                    edits[new Vector2Int((int)hit.point.x, (int)hit.point.z)] = true;
                //}
            }
        }
        
        if(Input.GetMouseButtonUp(1))
        {
            Debug.Log("D");
            ffs.SetDifficulty((drawType ? 0u : 1u ), new List<Vector2Int>(edits.Keys));
        }
        
	}
}
