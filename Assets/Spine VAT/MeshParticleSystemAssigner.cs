using UnityEngine;

[ExecuteInEditMode]
public class MeshParticleSystemAssigner : MonoBehaviour
{
    [SerializeField]
    private MeshFilter _meshFilter;
    [SerializeField]
    private ParticleSystem _particleSystem;

    private void OnEnable() => AssignMeshToRenderer();

    private void AssignMeshToRenderer()
    {
        if (_meshFilter == null || _particleSystem == null)
        {
            Debug.LogError("MeshFilter or ParticleSystem component is missing.");
            return;
        }

        Mesh mesh = _meshFilter.sharedMesh;
        if (mesh == null)
        {
            Debug.LogError("MeshFilter does not have a mesh.");
            return;
        }

        ParticleSystemRenderer renderer = _particleSystem.GetComponent<ParticleSystemRenderer>();
        if (renderer == null)
        {
            Debug.LogError("ParticleSystem does not have a ParticleSystemRenderer.");
            return;
        }

        renderer.mesh = new Mesh()
        {
            vertices = mesh.vertices,
            triangles = mesh.triangles,
            normals = mesh.normals,
            tangents = mesh.tangents,
            bounds = mesh.bounds,
            uv = mesh.uv
        }; ;
    }
}
