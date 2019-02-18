﻿using IATK;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using UnityEngine.Rendering;
using VRTK.Examples;

public class BrushingAndLinking : Photon.PunBehaviour
{

    [SerializeField]
    public ComputeShader computeShader;

    ComputeBuffer buffer;
    ComputeBuffer brushedIndicesBuffer;
    ComputeBuffer filteredIndicesBuffer;

    [SerializeField]
    public Material myRenderMaterial;

    public RenderTexture BrushedIndicesTexture { get; private set; }

    private int kernelHandleBrushTexture;
    private int kernelHandleBrushArrayIndices;
    private int kernelResetBrushTexture;

    GameObject viewHolder;
    int texSize;

    [SerializeField]
    public List<Visualisation> brushingVisualisations;
    [SerializeField]
    public List<Visualisation> brushedVisualisations;
    [SerializeField]
    public List<LinkingVisualisations> brushedLinkingVisualisations;

    [SerializeField]
    public bool showBrush = false;
    [SerializeField]
    public bool shareBrushing = false;
    [SerializeField]
    public Color privateBrushColor = Color.yellow;
    [SerializeField]
    public Color sharedBrushColor = Color.red;

    [SerializeField]
    [Range(1f, 10f)] public float brushSizeFactor = 1f;

    [SerializeField]
    public Transform input1;
    [SerializeField]
    public Transform input2;

    [SerializeField] [Range(0f, 1f)]
    public float radiusSphere;
    [SerializeField]
    public bool brushButtonController;

    public struct VecIndexPair
    {
        public Vector3 point;
        public int index;
    }

    public enum BrushType
    {
        SPHERE = 0,
        BOX = 1
    };

    public BrushType BRUSH_TYPE;

    public enum SelectionType
    {
        FREE = 0,
        ADDITIVE,
        SUBTRACTIVE
    };

    public SelectionType SELECTION_TYPE;

    int computeTextureSize(int sizeDatast)
    {
        return NextPowerOf2((int) Mathf.Sqrt((float) sizeDatast));
    }

    public Material debugObjectTexture;

    private AsyncGPUReadbackRequest detailsOnDemandRequest;
    public List<int> brushedIndices;

    // private fields
    private bool activated = false;

    private void Awake()
    {
        // Create a copy of the compute shader that is specfic to this brushing and linking script
        computeShader = Instantiate(computeShader);

        InitialiseShaders();
        InitialiseBuffersAndTextures();

        ChartManager.Instance.ChartAdded.AddListener(ChartAdded);
    }

    // Use this for initialization
    void Start()
    {
        brushingVisualisations = new List<Visualisation>();

        ResetBrushTexture();
    }

    // Update is called once per frame
    void Update()
    {
        if (brushButtonController)
        {
            switch (BRUSH_TYPE)
            {
                case BrushType.SPHERE:
                    Collider[] colliders = Physics.OverlapSphere(input1.position, radiusSphere);
                    brushingVisualisations.Clear();
                    foreach (Collider col in colliders)
                    {
                        if (col.gameObject.CompareTag("Chart"))
                            brushingVisualisations.Add(col.gameObject.GetComponent<Chart>().Visualisation);
                    }
                    break;

                case BrushType.BOX:
                    break;
            }
        }

        if (brushingVisualisations.Count != 0 && (brushButtonController) && input1 != null && input2 != null)
        {
            updateBrushTexture();

            getDetailsOnDemand();
        }
    }

    public void InitialiseShaders()
    {
        kernelHandleBrushTexture = computeShader.FindKernel("CSMain");
        kernelHandleBrushArrayIndices = computeShader.FindKernel("ComputeBrushedIndicesArray");
        kernelResetBrushTexture = computeShader.FindKernel("ResetBrushTexture");
    }

    public void InitialiseBuffersAndTextures()
    {
        int datasetSize = ChartManager.Instance.DataSource.DataCount;

        buffer = new ComputeBuffer(datasetSize, 12);
        buffer.SetData(new Vector3[datasetSize]);
        computeShader.SetBuffer(kernelHandleBrushTexture, "dataBuffer", buffer);

        brushedIndicesBuffer = new ComputeBuffer(datasetSize, 4);
        int[] brushIni = new int[datasetSize];
        for (int i = 0; i < datasetSize; i++)
            brushIni[i] = -1;
        brushedIndicesBuffer.SetData(brushIni);

        filteredIndicesBuffer = new ComputeBuffer(datasetSize, 4);
        filteredIndicesBuffer.SetData(new float[datasetSize]);

        computeShader.SetBuffer(kernelHandleBrushArrayIndices, "dataBuffer", buffer);
        computeShader.SetBuffer(kernelHandleBrushArrayIndices, "brushedIndices", brushedIndicesBuffer);

        texSize = computeTextureSize(datasetSize);

        // Find all existing brushing and linking scripts and get any existing RenderTexture
        BrushingAndLinking[] bals = FindObjectsOfType<BrushingAndLinking>();
        foreach (BrushingAndLinking bal in bals)
        {
            if (bal.BrushedIndicesTexture != null && bal.BrushedIndicesTexture.IsCreated())
                BrushedIndicesTexture = bal.BrushedIndicesTexture;
        }

        // If no existing RenderTexture was found, create a new one
        if (BrushedIndicesTexture == null)
        {
            BrushedIndicesTexture = new RenderTexture(texSize, texSize, 24);
            BrushedIndicesTexture.enableRandomWrite = true;
            BrushedIndicesTexture.filterMode = FilterMode.Point;
            BrushedIndicesTexture.Create();
        }

        myRenderMaterial.SetTexture("_MainTex", BrushedIndicesTexture);

        computeShader.SetTexture(kernelHandleBrushTexture, "Result", BrushedIndicesTexture);
        computeShader.SetTexture(kernelHandleBrushArrayIndices, "Result", BrushedIndicesTexture);
        computeShader.SetTexture(kernelResetBrushTexture, "Result", BrushedIndicesTexture);
        computeShader.SetFloat("_size", (float)texSize);
        computeShader.SetFloats("brushColor", privateBrushColor.r, privateBrushColor.g, privateBrushColor.b, privateBrushColor.a);
        if (photonView.isMine)
            photonView.RPC("SetSharedColor", PhotonTargets.AllBuffered, PlayerPreferencesManager.Instance.SharedBrushColor);
    }

    [PunRPC]
    private void SetSharedColor(Color color)
    {
        sharedBrushColor = color;
        computeShader.SetFloats("sharedBrushColor", sharedBrushColor.r, sharedBrushColor.g, sharedBrushColor.b, sharedBrushColor.a);
    }

    /// <summary>
    /// finds the next power of 2 for 
    /// </summary>
    /// <param name="number"></param>
    /// <returns></returns>
    private int NextPowerOf2(int number)
    {
        int pos = 0;

        while (number > 0)
        {
            pos++;
            number = number >> 1;
        }

        return (int) Mathf.Pow(2, pos);
    }
    
    public void UpdateComputeBuffers(Visualisation visualisation)
    {
        buffer.SetData(visualisation.theVisualizationObject.viewList[0].BigMesh.getBigMeshVertices());
        computeShader.SetBuffer(kernelHandleBrushTexture, "dataBuffer", buffer);

        filteredIndicesBuffer.SetData(visualisation.theVisualizationObject.viewList[0].GetFilterChannel());
        computeShader.SetBuffer(kernelHandleBrushTexture, "filteredIndices", filteredIndicesBuffer);
    }


    public void ChartAdded(Chart chart)
    {
        if (photonView.isMine)
        {
            StartCoroutine(SetChartViewProperties(chart));
        }
    }

    /// <summary>
    /// Waits a frame before setting properties in the charts view. 
    /// </summary>
    /// <param name="chart"></param>
    /// <returns></returns>
    private IEnumerator SetChartViewProperties(Chart chart)
    {
        yield return null;

        while (chart.Visualisation.theVisualizationObject.viewList.Count == 0)
        {
            yield return null;
        }

        foreach (var v in chart.Visualisation.theVisualizationObject.viewList)
        {
            v.BigMesh.SharedMaterial.SetTexture("_BrushedTexture", BrushedIndicesTexture);
            v.BigMesh.SharedMaterial.SetFloat("_DataWidth", texSize);
            v.BigMesh.SharedMaterial.SetFloat("_DataHeight", texSize);
            v.BigMesh.SharedMaterial.SetFloat("showBrush", Convert.ToSingle(showBrush));
            v.BigMesh.SharedMaterial.SetColor("brushColor", privateBrushColor);
            v.BigMesh.SharedMaterial.SetColor("sharedBrushColor", sharedBrushColor);
        }
    }
    
    /// <summary>
    /// runs the compute shader kernel and updates the brushed indices
    /// </summary>
    /// Texture2D bla 
    Texture2D cachedTexture;

    public void updateBrushTexture()
    {
        //bla.ReadPixels(new Rect(),0,0).
        //set brushgin mode
        computeShader.SetInt("BrushMode", (int) (BRUSH_TYPE));
        computeShader.SetInt("SelectionMode", (int) (SELECTION_TYPE));

        Vector3 projectedPointer1;
        Vector3 projectedPointer2;

        foreach (Visualisation brushingVisualisation in brushingVisualisations)
        {
            UpdateComputeBuffers(brushingVisualisation);

            switch (BRUSH_TYPE)
            {
                case BrushType.SPHERE:
                    projectedPointer1 =
                        brushingVisualisation.transform.InverseTransformPoint(input1.transform.position);
                    //  Vector3 
                    computeShader.SetFloat("pointer1x", projectedPointer1.x);
                    computeShader.SetFloat("pointer1y", projectedPointer1.y);
                    computeShader.SetFloat("pointer1z", projectedPointer1.z);

                    break;
                case BrushType.BOX:
                    projectedPointer1 =
                        brushingVisualisation.transform.InverseTransformPoint(input1.transform.position);
                    projectedPointer2 =
                        brushingVisualisation.transform.InverseTransformPoint(input2.transform.position);

                    //  Vector3 
                    computeShader.SetFloat("pointer1x", projectedPointer1.x);
                    computeShader.SetFloat("pointer1y", projectedPointer1.y);
                    computeShader.SetFloat("pointer1z", projectedPointer1.z);

                    computeShader.SetFloat("pointer2x", projectedPointer2.x);
                    computeShader.SetFloat("pointer2y", projectedPointer2.y);
                    computeShader.SetFloat("pointer2z", projectedPointer2.z);
                    break;
                default:
                    break;
            }

            //set the filters and normalisation values of the brushing visualisation to the computer shader
            computeShader.SetFloat("_MinNormX", brushingVisualisation.xDimension.minScale);
            computeShader.SetFloat("_MaxNormX", brushingVisualisation.xDimension.maxScale);
            computeShader.SetFloat("_MinNormY", brushingVisualisation.yDimension.minScale);
            computeShader.SetFloat("_MaxNormY", brushingVisualisation.yDimension.maxScale);
            computeShader.SetFloat("_MinNormZ", brushingVisualisation.zDimension.minScale);
            computeShader.SetFloat("_MaxNormZ", brushingVisualisation.zDimension.maxScale);

            computeShader.SetFloat("_MinX", brushingVisualisation.xDimension.minFilter);
            computeShader.SetFloat("_MaxX", brushingVisualisation.xDimension.maxFilter);
            computeShader.SetFloat("_MinY", brushingVisualisation.yDimension.minFilter);
            computeShader.SetFloat("_MaxY", brushingVisualisation.yDimension.maxFilter);
            computeShader.SetFloat("_MinZ", brushingVisualisation.zDimension.minFilter);
            computeShader.SetFloat("_MaxZ", brushingVisualisation.zDimension.maxFilter);

            computeShader.SetFloat("RadiusSphere", radiusSphere);

            computeShader.SetFloat("width", brushingVisualisation.width);
            computeShader.SetFloat("height", brushingVisualisation.height);
            computeShader.SetFloat("depth", brushingVisualisation.depth);

            // Set whether or not the brushing is currently being shared
            computeShader.SetBool("shareBrushing", shareBrushing);

            //run the compute shader with all the filtering parameters
            computeShader.Dispatch(kernelHandleBrushTexture, Mathf.CeilToInt(texSize / 32), Mathf.CeilToInt(texSize / 32), 1);

            //brushingVisualisation.theVisualizationObject.viewList[0].BigMesh.SharedMaterial
            //    .SetTexture("_BrushedTexture", BrushedIndicesTexture);
            //brushingVisualisation.theVisualizationObject.viewList[0].BigMesh.SharedMaterial
            //    .SetFloat("_DataWidth", texSize);
            //brushingVisualisation.theVisualizationObject.viewList[0].BigMesh.SharedMaterial
            //    .SetFloat("_DataHeight", texSize);
            //brushingVisualisation.theVisualizationObject.viewList[0].BigMesh.SharedMaterial
            //    .SetFloat("showBrush", Convert.ToSingle(showBrush));
            //brushingVisualisation.theVisualizationObject.viewList[0].BigMesh.SharedMaterial
            //    .SetColor("brushColor", brushColor);
        }
        
        //foreach (var item in brushedLinkingVisualisations)
        //{
        //    item.View.BigMesh.SharedMaterial.SetTexture("_BrushedTexture", brushedIndicesTexture);
        //    item.View.BigMesh.SharedMaterial.SetFloat("_DataWidth", texSize);
        //    item.View.BigMesh.SharedMaterial.SetFloat("_DataHeight", texSize);
        //    item.View.BigMesh.SharedMaterial.SetFloat("showBrush", Convert.ToSingle(showBrush));
        //    item.View.BigMesh.SharedMaterial.SetColor("brushColor", brushColor);
        //}

        //cachedTexture = (brushingVisualisation.theVisualizationObject.viewList[0].BigMesh.SharedMaterial.GetTexture("_BrushedTexture") as Texture2D);
        //if (cachedTexture.GetPixel(0, 0).r > 0f) print("selected!!");
        //float t = Time.time;
        //brushedIndicesBuffer.GetData(brushIni);
        //  if (brushIni[0] > 0f) print("Selected");
        //getDetailsOnDemand();
        //debugObjectTexture.SetTexture("_MainTex", brushedIndicesTexture);

    }

    public void getDetailsOnDemand()
    {
        if (detailsOnDemandRequest.done)
        {
            if (!detailsOnDemandRequest.hasError)
            {
                // Get data
                int[] result = detailsOnDemandRequest.GetData<int>().ToArray();
                //Debug.Log(result.Length);
                //brushedIndices = Enumerable.Range(0, result.Length)
                //    .Where(x => result[x] == 1)
                //    .ToList();
                brushedIndices = result.ToList();
            }

            // Dispatch again
            computeShader.Dispatch(kernelHandleBrushArrayIndices, Mathf.CeilToInt(brushedIndicesBuffer.count / 16f), 1,
                1);
            detailsOnDemandRequest = AsyncGPUReadback.Request(brushedIndicesBuffer);
        }
    }

    public void ResetBrushTexture()
    {
        computeShader.Dispatch(kernelResetBrushTexture, Mathf.CeilToInt(texSize / 32), Mathf.CeilToInt(texSize / 32), 1);
    }

    /// <summary>
    /// on destroy release the buffers on the graphic card
    /// </summary>
    void OnDestroy()
    {
        if (buffer != null)
            buffer.Release();

        if (brushedIndicesBuffer != null)
            brushedIndicesBuffer.Release();

        //Visualisation.OnUpdateViewAction -= Visualisation_OnUpdateViewAction;
    }

    private void OnApplicationQuit()
    {
        if (buffer != null)
            buffer.Release();

        if (brushedIndicesBuffer != null)
            brushedIndicesBuffer.Release();

        //Visualisation.OnUpdateViewAction -= Visualisation_OnUpdateViewAction;
    }


    void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.isWriting)
        { 
            stream.SendNext(brushButtonController);
            stream.SendNext(shareBrushing);
            stream.SendNext(radiusSphere);
            stream.SendNext(BRUSH_TYPE);
            stream.SendNext(SELECTION_TYPE);
        }
        else
        {
            bool isBrushing = (bool) stream.ReceiveNext();
            shareBrushing = (bool) stream.ReceiveNext();
            radiusSphere = (float) stream.ReceiveNext();
            BRUSH_TYPE = (BrushType) stream.ReceiveNext();
            SELECTION_TYPE = (SelectionType) stream.ReceiveNext();

            // Only turn on the brush if the client who owns it is also sharing the brush
            if (isBrushing && shareBrushing)
                brushButtonController = true;
            else
                brushButtonController = false;
        }
    }
}