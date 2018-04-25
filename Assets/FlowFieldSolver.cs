using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//TODO: if you delete obstacles, the toggle on them will need to be reset


public struct editStep
{
    public List<Vector2Int> blocks;
    public uint newVal;

    public editStep(List<Vector2Int> b, uint nv)
    {
        blocks = b;
        newVal = nv;
    }
}

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
    public int MaxRunsPerPass = 256;
    public GameObject obstacle;

    private int flowWidth = 64;
    private int flowHeight = 64;

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
    private bool operating;

    private bool waitingForRetrieval;

    private bool AtoB;

    //private GameObject[] debugArray;

    private float computestarttime;

    private Vector2Int lastTarget;

    private List<editStep> edits;

    private GameObject[,] obstacles;

    private int size_of(System.Type mytype)
    {
        return System.Runtime.InteropServices.Marshal.SizeOf(mytype);
    }

    public uint GetDifficulty(int x, int y)
    {
        if (x < 0 || x >= flowWidth || y < 0 || y >= flowHeight)
            return 0;
        else
            return difficultyData[x + y * flowWidth];
    }

    public void SetDifficulty(uint newvalue, List<Vector2Int> squares)
    {
        edits.Add(new editStep(squares, newvalue));
    }

    void Start () {

        lastTarget = new Vector2Int();
        fulfilled = true;
        pathreturn = true;
        operating = false;
        AtoB = true;
        edits = new List<editStep>();

        SetupHandles();
        SetupConsts();

        obstacles = new GameObject[flowWidth, flowHeight];
        for (int j = 0; j < flowHeight; j++)
            for (int i = 0; i < flowWidth; i++)
            {
                obstacles[i, j] = (GameObject)Object.Instantiate(obstacle, new Vector3(i + 0.5f, 0.5f, j + 0.5f), Quaternion.identity);
                obstacles[i, j].SetActive(false);
            }

        //debugArray = new GameObject[flowSize];
        //for (int i = 0; i < flowSize; i++)
        //{
        //debugArray[i] = (GameObject)Object.Instantiate(basecube, new Vector3(i % flowWidth, i / flowWidth, 0.0f), Quaternion.identity);
        //}

        SetupDifficulty();
        SetupPath();
        SetupUnfulfilled();

        //NewDest(15, 3);
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
        {
            difficultyData[i] = 1;
        }

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

        int runsThisPass = 0;

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
            runsThisPass++;

            AtoB = !AtoB;
        } while (Time.realtimeSinceStartup - startTime < pathshare && runsThisPass < MaxRunsPerPass);

        if(currentRuns > runsBeforeCheck)
        {
            if(!waitingForRetrieval)
            {
                AsyncTextureReader.RequestBufferData(unfulfilledBuffer);
                waitingForRetrieval = true;
            }
            else
            {
                AsyncTextureReader.Status status = AsyncTextureReader.RetrieveBufferData(unfulfilledBuffer, unfulfilledData);
                if(status == AsyncTextureReader.Status.Succeeded)
                {
                    waitingForRetrieval = false;
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
        operating = true;
        lastTarget = new Vector2Int(x, y);

        computestarttime = Time.realtimeSinceStartup;
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

    public Vector2Int GetFlow(float x, float y)
    {
        pathInfo pi = pathData[(int)x + (int)y * flowWidth];
        return new Vector2Int(pi.xDir, pi.yDir);
    }

	// Update is called once per frame
	void Update () {

        if (operating)
        {
            if (!fulfilled)
            {
                PathCompute();
            }
            else if (!pathreturn)
            {
                if (!waitingForRetrieval)
                {
                    if (AtoB)
                        AsyncTextureReader.RequestBufferData(pathBufferB);
                    else
                        AsyncTextureReader.RequestBufferData(pathBufferA);
                    waitingForRetrieval = true;
                }
                else
                {
                    AsyncTextureReader.Status status;

                    if (AtoB)
                        status = AsyncTextureReader.RetrieveBufferData(pathBufferB, pathDataRaw);
                    else
                        status = AsyncTextureReader.RetrieveBufferData(pathBufferA, pathDataRaw);

                    if (status == AsyncTextureReader.Status.Succeeded)
                    {
                        Debug.Log(Time.realtimeSinceStartup - computestarttime);
                        waitingForRetrieval = false;
                        pathreturn = true;

                        for (int i = 0; i < pathData.Length; i++)
                        {
                            pathData[i] = new pathInfo(System.BitConverter.ToUInt32(pathDataRaw, i * 16), System.BitConverter.ToInt32(pathDataRaw, i * 16 + 4), System.BitConverter.ToInt32(pathDataRaw, i * 16 + 8), System.BitConverter.ToUInt32(pathDataRaw, i * 16 + 12));
                            //debugArray[i].transform.localScale = new Vector3(1.0f, 1.0f, pathData[i].cost / 10.0f);
                        }

                        if (activePath == 1)
                            activePath = 0;
                        else
                            activePath = 1;

                        shader.SetInt("activePath", activePath);

                        operating = false;
                        //Debug.Log("hey now");
                    }
                }
            }
        }
        else
        {
            //edit 
            if(edits.Count > 0)
            {
                while(edits.Count > 0)
                {
                    for(int i = 0; i < edits[0].blocks.Count; i++)
                    {
                        if (edits[0].blocks[i].x > 0 && edits[0].blocks[i].x < flowWidth && edits[0].blocks[i].y > 0 && edits[0].blocks[i].y < flowHeight)
                        {
                            difficultyData[edits[0].blocks[i].x + edits[0].blocks[i].y * flowWidth] = edits[0].newVal;
                            obstacles[edits[0].blocks[i].x, edits[0].blocks[i].y].SetActive(edits[0].newVal == 0u);
                        }
                    }
                    edits.RemoveAt(0);
                }

                difficultyBuffer.SetData(difficultyData);

                NewDest(lastTarget.x, lastTarget.y);
            }
        }
	}
}
