using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//TODO: if you delete obstacles, the toggle on them will need to be reset

[System.Serializable]
public struct pathInfo
{
    public uint toggle;
    public int xDir;
    public int yDir;
    public uint cost;

    public pathInfo(uint t, int x, int y, uint c)
    {
        toggle = t;
        xDir = x;
        yDir = y;
        cost = c;
    }
};

public class FlowFieldSolver : MonoBehaviour {

    public ComputeShader shader;
    public float pathshare = 0.01f;
    public GameObject basecube;

    private int flowWidth = 256;
    private int flowHeight = 256;

    private int flowSize;
    private int activePath;

    private ComputeBuffer difficultyBuffer;
    private uint[] difficultyData;

    private ComputeBuffer pathBufferA;
    private ComputeBuffer pathBufferB;
    private byte[] pathDataRaw;
    private pathInfo[] pathData;

    private ComputeBuffer unfulfilledBuffer;
    private int[] unfulfilledData;

    private int PathSolverHandle;

    private bool fulfilled;
    private bool pathreturn;
    private int runsBeforeCheck;
    private int currentRuns;

    private bool waitingForRetrieval;

    private bool AtoB;

    private int size_of(System.Type mytype)
    {
        return System.Runtime.InteropServices.Marshal.SizeOf(mytype);
    }

    void Start () {

        fulfilled = true;
        pathreturn = true;
        AtoB = true;

        SetupHandles();
        SetupConsts();

        SetupDifficulty();
        SetupPath();
        SetupUnfulfilled();

        NewDest(15, 3);
    }

    private void SetupHandles()
    {
        PathSolverHandle = shader.FindKernel("PathSolve");
    }

    private void SetupConsts()
    {
        activePath = 1;
        flowSize = flowWidth * flowHeight;

        shader.SetInt("flowBufferWidth", flowWidth);
        shader.SetInt("flowBufferHeight", flowHeight);
        shader.SetInt("activePath", activePath);
    }

    private void SetupDifficulty()
    {
        difficultyData = new uint[flowSize];

        for (int i = 0; i < flowSize; i++)
            difficultyData[i] = 1;

        difficultyBuffer = new ComputeBuffer(flowSize, sizeof(uint));
        difficultyBuffer.SetData(difficultyData);

        shader.SetBuffer(PathSolverHandle, "difficultyBuffer", difficultyBuffer);
    }

    private void SetupPath()
    {
        pathData = new pathInfo[flowSize];
        for (int i = 0; i < flowSize; i++)
            pathData[i] = new pathInfo(0, 0, 0, 0);

        pathDataRaw = new byte[flowSize * size_of(typeof(pathInfo))];

        pathBufferA = new ComputeBuffer(flowSize, size_of(typeof(pathInfo)));
        pathBufferB = new ComputeBuffer(flowSize, size_of(typeof(pathInfo)));
        //pathBuffer.SetData(pathData);
    }

    private void SetupUnfulfilled()
    {
        unfulfilledData = new int[1];

        unfulfilledBuffer = new ComputeBuffer(1, sizeof(int));

        shader.SetBuffer(PathSolverHandle, "unfulfilledBuffer", unfulfilledBuffer);
    }
	
    public void PathCompute()
    {
        float startTime = Time.realtimeSinceStartup;

        unfulfilledData[0] = 0;
        unfulfilledBuffer.SetData(unfulfilledData);

        do
        {
            if (AtoB)
            {
                shader.SetBuffer(PathSolverHandle, "pathBufferFrom", pathBufferA);
                shader.SetBuffer(PathSolverHandle, "pathBufferTo", pathBufferB);
            }
            else
            {
                shader.SetBuffer(PathSolverHandle, "pathBufferFrom", pathBufferB);
                shader.SetBuffer(PathSolverHandle, "pathBufferTo", pathBufferA);
            }

            shader.Dispatch(PathSolverHandle, flowWidth / 8, flowHeight / 8, 1);
            currentRuns++;

            AtoB = !AtoB;
        } while (Time.realtimeSinceStartup - startTime < pathshare);

        if(currentRuns > runsBeforeCheck)
        {
            if(!waitingForRetrieval)
            {
                AsyncTextureReader.RequestBufferData(unfulfilledBuffer);
                waitingForRetrieval = true;
                Debug.Log("B");
            }
            else
            {
                Debug.Log("C");
                AsyncTextureReader.Status status = AsyncTextureReader.RetrieveBufferData(unfulfilledBuffer, unfulfilledData);
                if(status == AsyncTextureReader.Status.Succeeded)
                {
                    waitingForRetrieval = false;
                    fulfilled = true;
                    if(unfulfilledData[0] == 0)
                    {
                        fulfilled = true;
                    }
                }
            }
        }
    }

    public void NewDest(int x, int y)
    {
        pathData[x + y * flowWidth] = new pathInfo((uint)activePath, 0, 0, 0);
        if (AtoB)
            pathBufferA.SetData(pathData);
        else
            pathBufferB.SetData(pathData);

        fulfilled = false;
        pathreturn = false;
        waitingForRetrieval = false;

        runsBeforeCheck = 256; //make this actually something according to x and y

        currentRuns = 0;

        PathCompute();
    }

	// Update is called once per frame
	void Update () {
		if(!fulfilled)
        {
            PathCompute();
            Debug.Log("A");
        }
        else if(!pathreturn)
        {
            Debug.Log("D");
            if (!waitingForRetrieval)
            {
                if(AtoB)
                    AsyncTextureReader.RequestBufferData(pathBufferB);
                else
                    AsyncTextureReader.RequestBufferData(pathBufferA);
                waitingForRetrieval = true;
                Debug.Log("E");
            }
            else
            {
                Debug.Log("F");
                AsyncTextureReader.Status status;

                if (AtoB)
                    status = AsyncTextureReader.RetrieveBufferData(pathBufferB, pathDataRaw);
                else
                    status = AsyncTextureReader.RetrieveBufferData(pathBufferA, pathDataRaw);

                if (status == AsyncTextureReader.Status.Succeeded)
                {
                    waitingForRetrieval = false;
                    pathreturn = true;

                    Debug.Log("G");
                    /*BinaryFormatter bf = new BinaryFormatter();
                    using (MemoryStream ms = new MemoryStream(pathDataRaw))
                    {
                        Debug.Log("H");
                        pathData = (pathInfo[])bf.Deserialize(ms);
                        Debug.Log("I");
                    }*/

                    for (int i = 0; i < pathData.Length; i++)
                    {
                        pathData[i] = new pathInfo(System.BitConverter.ToUInt32(pathDataRaw, i * 16), System.BitConverter.ToInt32(pathDataRaw, i * 16 + 4), System.BitConverter.ToInt32(pathDataRaw, i * 16 + 8), System.BitConverter.ToUInt32(pathDataRaw, i * 16 + 12));
                        GameObject bc = (GameObject)Object.Instantiate(basecube, new Vector3(i % flowWidth, i / flowWidth, 0.0f), Quaternion.identity);
                        bc.transform.localScale = new Vector3(1.0f, 1.0f, pathData[i].cost / 10.0f);
                    }

                    if (activePath == 1)
                        activePath = 0;
                    else
                        activePath = 1;

                    shader.SetInt("activePath", activePath);

                    Debug.Log("hey now");
                }
            }
        }
	}
}
