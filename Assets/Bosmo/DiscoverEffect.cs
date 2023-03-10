using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bosmo
{
    public class DiscoverEffect
    {
        public ComputeBuffer output { get; }
        private ComputeShader discoverMeshShader;
        private int speed;
        private int speedWhenReverse;
        private float timeUntilReverse = 30;
        private float timeDecayBeforeReverse = 20;
        private float decaySpeed;
    
        private GraphicsBuffer gpuVertices;
        private GraphicsBuffer gpuIndices;
        private ComputeBuffer gpuAdjacentTriangle;
        private ComputeBuffer gpuTrianglesShouldCheck;
        private ComputeBuffer gpuAmountTrianglesToCheck;


        private List<ComputeBuffer> gpuAdjacentTrianglesIndexList;
        private List<ComputeBuffer> gpuTrianglesShouldCheckAppendList;
        private List<int> amountTrianglesToCheckList;
        private List<bool> reverseDirectionList;
        private List<float> timeUntilReverseList;
        private List<float> timeToDecayUntilReverseList;
        private List<float> startTimeList;

        private Mesh mesh;
        private bool firstTimeStart;
        private int kernelID;
        private int threadGroupSize;
        private int checkedIndicesCache;
        private bool doneDiscover;
        private int[] whichTrianglesToCheck;
        private int[] emptyArray;
        private int vertexStride;
        private int indexStride;
        private AdjacentTriangles[] adjacentTrianglesArray;
        private int amountTriangles => mesh.triangles.Length / 3;
        private int amountEffectsRunning => gpuAdjacentTrianglesIndexList.Count;
    
        public DiscoverEffect(Mesh mesh, int speed = 1, float decaySpeed = 1f, int speedWhenReverse = 2, float timeUntilReverse = 0.5f, float timeDecayBeforeReverse = 0.75f)
        {
            this.mesh = mesh;
            this.speed = speed;
            this.speedWhenReverse = speedWhenReverse;
            this.timeUntilReverse = timeUntilReverse;
            this.timeDecayBeforeReverse = timeDecayBeforeReverse;
            this.decaySpeed = decaySpeed;
        
            discoverMeshShader = (ComputeShader)Resources.Load("MeshEffects/DiscoverMesh");
        
            emptyArray = new int[1];
            amountTrianglesToCheckList = new List<int>();
            gpuAdjacentTrianglesIndexList = new List<ComputeBuffer>();
            gpuTrianglesShouldCheckAppendList = new List<ComputeBuffer>();
            reverseDirectionList = new List<bool>();
            timeUntilReverseList = new List<float>();
            timeToDecayUntilReverseList = new List<float>();
            startTimeList = new List<float>();

            gpuAdjacentTriangle = new ComputeBuffer(amountTriangles, sizeof(int) * 3, ComputeBufferType.Structured);
            gpuAmountTrianglesToCheck = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Structured);
            output = new ComputeBuffer(mesh.vertexCount, sizeof(float), ComputeBufferType.Structured);

            float[] empty = new float[mesh.vertexCount];
            output.SetData(empty);
        }

        public void Destory()
        {
            gpuVertices?.Dispose();
            gpuVertices = null;
            gpuIndices?.Dispose();
            gpuIndices = null;
            gpuAdjacentTriangle?.Dispose();
            gpuAdjacentTriangle = null;
            gpuTrianglesShouldCheck?.Dispose();
            gpuTrianglesShouldCheck = null;
            gpuAmountTrianglesToCheck?.Dispose();
            gpuAmountTrianglesToCheck = null;

            foreach (var buffer in gpuAdjacentTrianglesIndexList)
            {
                buffer?.Release();
            }
            foreach (var buffer in gpuTrianglesShouldCheckAppendList)
            {
                buffer?.Release();
            }
        }
    
        public void DiscoverAllAdjacentTriangle()
        {
            gpuVertices ??= mesh.GetVertexBuffer(0);
            gpuIndices ??= mesh.GetIndexBuffer();
            
            vertexStride = mesh.GetVertexBufferStride(0);
            indexStride = MeshExtensions.GetIndexBufferStride(mesh);
        
            kernelID = discoverMeshShader.FindKernel("FindAdjacentTriangles");
            discoverMeshShader.GetKernelThreadGroupSizes(kernelID, out uint threadGroupSizeX, out _, out _);
            threadGroupSize = Mathf.CeilToInt((float)amountTriangles / threadGroupSizeX);
        
            discoverMeshShader.SetBuffer(kernelID,"gpuVertices", gpuVertices);
            discoverMeshShader.SetBuffer(kernelID,"gpuIndices", gpuIndices);
            discoverMeshShader.SetBuffer(kernelID,"gpuAdjacentTriangle", gpuAdjacentTriangle);
            discoverMeshShader.SetFloat("currentTime", Time.timeSinceLevelLoad);
            discoverMeshShader.SetInt("amountTriangles", amountTriangles);
            discoverMeshShader.SetInt("vertexStride", vertexStride);
            discoverMeshShader.SetInt("indexStride", indexStride);
            discoverMeshShader.Dispatch(kernelID, threadGroupSize, 1, 1);
        }
    
        public void FirstTriangleToCheck(int triangleToStart, bool reverseDirection = false)
        {
            ComputeBuffer adjacentTrianglesIndex = new ComputeBuffer(amountTriangles, sizeof(int), ComputeBufferType.Structured);
            adjacentTrianglesIndex.SetData(new int[amountTriangles]);
            gpuAdjacentTrianglesIndexList.Add(adjacentTrianglesIndex);
        
            ComputeBuffer gpuTrianglesShouldCheckAppends = new ComputeBuffer(amountTriangles / 2, sizeof(int), ComputeBufferType.Append);
            gpuTrianglesShouldCheckAppendList.Add(gpuTrianglesShouldCheckAppends);
        
            whichTrianglesToCheck = new int[1];
            whichTrianglesToCheck[0] = triangleToStart;
            gpuTrianglesShouldCheckAppendList[amountEffectsRunning - 1].SetData(whichTrianglesToCheck);
            amountTrianglesToCheckList.Add(1);
        
            reverseDirectionList.Add(reverseDirection);
            timeUntilReverseList.Add(timeUntilReverse);
            timeToDecayUntilReverseList.Add(timeDecayBeforeReverse);
            startTimeList.Add(Time.time);
        }

        public void IncrementTriangles()
        {
            gpuVertices ??= mesh.GetVertexBuffer(0);
            gpuIndices ??= mesh.GetIndexBuffer();
            gpuAmountTrianglesToCheck.SetData(emptyArray);
            indexStride = MeshExtensions.GetIndexBufferStride(mesh);
            
            // adjacentTrianglesArray = new AdjacentTriangles[amountTriangles];
            // gpuAdjacentTriangle.GetData(adjacentTrianglesArray);
            // int[] debugIndex = new int[amountTriangles];
            // gpuAdjacentTrianglesIndexList[0].GetData(debugIndex);
        
            for (int i = 0; i < amountEffectsRunning; i++)
            {
                IncrementTriangle(i);
            }
        }

        private void IncrementTriangle(int i)
        {
            bool shouldReverseDirection = false;
            int incrementSpeed = speed;
            
            if (reverseDirectionList[i] && Time.time > startTimeList[i] + timeUntilReverseList[i])
            {
                if (Time.time > startTimeList[i] + timeToDecayUntilReverseList[i] + timeUntilReverseList[i])
                {
                    incrementSpeed = speedWhenReverse;
                    shouldReverseDirection = true;
                }
                else
                {
                    return;
                }
            }
            for (int j = 0; j < incrementSpeed; j++)
            {
                gpuTrianglesShouldCheck?.Dispose();
                gpuTrianglesShouldCheck = null;
                gpuTrianglesShouldCheck = new ComputeBuffer(amountTrianglesToCheckList[i], sizeof(int), ComputeBufferType.Structured);

                whichTrianglesToCheck = new int[amountTrianglesToCheckList[i]];
                gpuTrianglesShouldCheckAppendList[i].GetData(whichTrianglesToCheck);
                whichTrianglesToCheck = whichTrianglesToCheck.Distinct().ToArray();
                amountTrianglesToCheckList[i] = whichTrianglesToCheck.Length;
                discoverMeshShader.SetInt("amountTrianglesToCheck", amountTrianglesToCheckList[i]);
                gpuTrianglesShouldCheck.SetData(whichTrianglesToCheck);

                kernelID = discoverMeshShader.FindKernel("DiscoverMesh");
                discoverMeshShader.GetKernelThreadGroupSizes(kernelID, out uint threadGroupSizeX, out _, out _);
                threadGroupSize = Mathf.CeilToInt(amountTrianglesToCheckList[i] / (float)threadGroupSizeX);

                //const
                discoverMeshShader.SetBuffer(kernelID, "gpuVertices", gpuVertices);
                discoverMeshShader.SetBuffer(kernelID, "gpuIndices", gpuIndices);
                discoverMeshShader.SetBuffer(kernelID, "gpuAdjacentTriangleRead", gpuAdjacentTriangle);
                discoverMeshShader.SetInt("amountTriangles", amountTriangles);
                discoverMeshShader.SetInt("indexStride", indexStride);
                //changes
                discoverMeshShader.SetBuffer(kernelID, "gpuTrianglesShouldCheck", gpuTrianglesShouldCheck);
                discoverMeshShader.SetBuffer(kernelID, "gpuTrianglesShouldCheckAppend", gpuTrianglesShouldCheckAppendList[i]);
                discoverMeshShader.SetBuffer(kernelID, "gpuAmountTrianglesToCheck", gpuAmountTrianglesToCheck);
                discoverMeshShader.SetBuffer(kernelID, "gpuAdjacentTrianglesIndex", gpuAdjacentTrianglesIndexList[i]);
                discoverMeshShader.SetBuffer(kernelID, "output", output);
                discoverMeshShader.SetInt("amountTrianglesToCheck", amountTrianglesToCheckList[i]);
                discoverMeshShader.SetBool("reverseDirection", shouldReverseDirection);
                gpuTrianglesShouldCheckAppendList[i].SetCounterValue(0);
                discoverMeshShader.Dispatch(kernelID, threadGroupSize, 1, 1);

                int[] amountTrianglesToCheckArray = new int[1];
                gpuAmountTrianglesToCheck.GetData(amountTrianglesToCheckArray);
                amountTrianglesToCheckList[i] = amountTrianglesToCheckArray[0];
                gpuAmountTrianglesToCheck.SetData(emptyArray);

                if (amountTrianglesToCheckList[i] == 0)
                {
                    gpuAdjacentTrianglesIndexList[i]?.Release();
                    gpuAdjacentTrianglesIndexList.RemoveAt(i);
                    gpuTrianglesShouldCheckAppendList[i]?.Release();
                    gpuTrianglesShouldCheckAppendList.RemoveAt(i);
                    amountTrianglesToCheckList.RemoveAt(i);
                    reverseDirectionList.RemoveAt(i);
                    timeUntilReverseList.RemoveAt(i);
                    timeToDecayUntilReverseList.RemoveAt(i);
                    startTimeList.RemoveAt(i);
                    return;
                }
            }
        }

        public void DecayMesh()
        {
            kernelID = discoverMeshShader.FindKernel("DecayMesh");
            discoverMeshShader.GetKernelThreadGroupSizes(kernelID, out uint threadGroupSizeX, out _, out _);
            threadGroupSize = Mathf.CeilToInt(mesh.vertexCount / (float)threadGroupSizeX);
            
            indexStride = MeshExtensions.GetIndexBufferStride(mesh);

            //const
            discoverMeshShader.SetBuffer(kernelID, "gpuVertices", gpuVertices);
            discoverMeshShader.SetBuffer(kernelID, "gpuIndices", gpuIndices);
            discoverMeshShader.SetBuffer(kernelID, "output", output);
            discoverMeshShader.SetInt("amountVerts", mesh.vertexCount);
            discoverMeshShader.SetInt("indexStride", indexStride);
            //changes
            discoverMeshShader.SetFloat("decaySpeed", decaySpeed);

            discoverMeshShader.Dispatch(kernelID, threadGroupSize, 1, 1);
        }
    }
}

struct AdjacentTriangles
{
    public int tri1TriangleIndex;
    public int tri2TriangleIndex;
    public int tri3TriangleIndex;
}
