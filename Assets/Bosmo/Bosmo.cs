using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bosmo
{
    public class Bosmo : MonoBehaviour, IHittable
    {
        [SerializeField] private MeshFilter meshFilter;
        [SerializeField] private bool reverseDirection;
        [SerializeField] private bool run = true;
        
        private DiscoverEffect discoverMesh;
        private GetTriangle closestTriangle;
        private CompositeEffects compositer;
        private Mesh mesh;

        void Awake()
        {
            mesh = meshFilter.sharedMesh;
            mesh = MeshExtensions.CopyMesh(mesh);
            meshFilter.sharedMesh = mesh;

            mesh.uv4 = new Vector2[mesh.vertexCount];
        
            mesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
            mesh.indexBufferTarget |= GraphicsBuffer.Target.Raw;
            
            MeshExtensions.AddVertexAttribute(mesh, new VertexAttributeDescriptor(VertexAttribute.TexCoord3, VertexAttributeFormat.Float32, 2, 1));
            MeshExtensions.AddVertexAttribute(mesh, new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 2));

            //MeshExtensions.LogAllVertexAttributes(mesh);
            Debug.Log(MeshExtensions.GetIndexBufferStride(mesh));
        
            discoverMesh = new DiscoverEffect(mesh);
            discoverMesh.DiscoverAllAdjacentTriangle();
            closestTriangle = new GetTriangle(mesh, meshFilter.transform);
            compositer = new CompositeEffects(mesh)
            {
                input1 = discoverMesh.output,
            };

        }

        private void OnDisable()
        {
            discoverMesh.Destory();
            closestTriangle.Destroy();
            compositer.Destroy();
        }

        void FixedUpdate()
        {
            if (Input.GetKeyDown(KeyCode.F) || run)
            {
                discoverMesh.IncrementTriangles();
                discoverMesh.DecayMesh();
                compositer.Compositing();
            }
            
        }
        public void Hit(Vector3 hitPos)
        {
            int closestTriangleID = closestTriangle.GetClosestTriangle(hitPos);
            discoverMesh.FirstTriangleToCheck(closestTriangleID, reverseDirection);
        }
    }
}
