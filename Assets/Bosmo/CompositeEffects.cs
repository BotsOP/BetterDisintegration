using UnityEngine;

namespace Bosmo
{
    public class CompositeEffects
    {
        public ComputeBuffer input1;
    
        private ComputeShader compositeShader;
        private float distThreshold;

        private GraphicsBuffer gpuVertices;
        private GraphicsBuffer gpuIndices;

        private Mesh mesh;
        private Transform meshTransform;
        private int kernelID;
        private int threadGroupSize;
        private int vertexStride;

        private int amountTriangles => mesh.triangles.Length / 3;

        public CompositeEffects(Mesh mesh)
        {
            this.mesh = mesh;

            compositeShader = (ComputeShader)Resources.Load("MeshEffects/CompositeShader");
        
            kernelID = compositeShader.FindKernel("CompositeEffects");
            compositeShader.GetKernelThreadGroupSizes(kernelID, out uint threadGroupSizeX, out _, out _);
            threadGroupSize = Mathf.CeilToInt(mesh.vertexCount / (float)threadGroupSizeX);
        }
    
        public void Destroy()
        {
            gpuVertices?.Dispose();
            gpuVertices = null;
            gpuIndices?.Dispose();
            gpuIndices = null;
            input1?.Dispose();
            input1 = null;
        }

        public void Compositing()
        {
            gpuVertices ??= mesh.GetVertexBuffer(1);
            gpuIndices ??= mesh.GetIndexBuffer();
            
            vertexStride = mesh.GetVertexBufferStride(1);
        
            compositeShader.SetBuffer(kernelID, "gpuVertices", gpuVertices);
            compositeShader.SetBuffer(kernelID, "gpuIndices", gpuIndices);
            compositeShader.SetBuffer(kernelID, "input1", input1);
            compositeShader.SetInt("vertexStride", vertexStride);
            compositeShader.SetInt("amountVerts", mesh.vertexCount);
            compositeShader.Dispatch(kernelID, threadGroupSize, 1, 1);
        }
    }
}
