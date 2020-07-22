using System.Collections.Generic;
using UnityEngine;

public class RayTracingMaster : MonoBehaviour
{
    public ComputeShader rayTracingShader;
    private RenderTexture _target;

    private Camera _camera;

    public Texture SkyboxTexture;

    private uint _currentSample = 0;
    private Material _addMaterial;
    public Light directionLight;

    private ComputeBuffer _sphereBuffer;

    [Header("Sphere")]
    public Vector2 SphereRadius = new Vector2(3.0f, 8.0f);
    public uint SpheresMax = 100;
    public float SpherePlacementRadius = 100.0f;
    private float _lastFieldOfView;
    private List<Transform> _tranformsToWatch = new List<Transform>();


    struct Sphere {
        public Vector3 position;
        public float radius;
        public Vector3 albedo;
        public Vector3 specular;
    }
    private void Awake()
    {
        _camera = GetComponent<Camera>();

        _tranformsToWatch.Add(transform);
        _tranformsToWatch.Add(directionLight.transform);
    }
    private void OnEnable()
    {
        _currentSample = 0;
        SetUpScene();
    }
    private void OnDisable()
    {
        if (_sphereBuffer != null)
            _sphereBuffer.Release();
    }
    private void SetUpScene()
    {
        List<Sphere> spheres = new List<Sphere>();
        for (int i = 0; i < SpheresMax; i++)
        {
            Sphere sphere = new Sphere();

            sphere.radius = SphereRadius.x + Random.value * (SphereRadius.y - SphereRadius.x);
            Vector2 randomPos = Random.insideUnitCircle * SpherePlacementRadius;
            sphere.position = new Vector3(randomPos.x, sphere.radius, randomPos.y);

            foreach (Sphere other in spheres)
            {
                float minDist = sphere.radius + other.radius;
                if (Vector3.SqrMagnitude(sphere.position - other.position) < minDist * minDist)
                {
                    goto SkipSphere;
                }
            }
            Color color = Random.ColorHSV();
            bool metal = Random.value < 0.5f;
            sphere.albedo = metal ? Vector3.zero : new Vector3(color.r, color.g, color.b);
            sphere.specular = metal ? new Vector3(color.r, color.g, color.b) : Vector3.one * 0.04f;

            //Add the sphere to the list
            spheres.Add(sphere);

            SkipSphere:
                continue;
        }
        //Asign to compute buffer
        if (_sphereBuffer != null)
            _sphereBuffer.Release();
        if (spheres.Count > 0)
        {
            _sphereBuffer = new ComputeBuffer(spheres.Count, 40);
            _sphereBuffer.SetData(spheres);
        }
    }
    private void Update()
    {
        if (_camera.fieldOfView != _lastFieldOfView)
        {
            _currentSample = 0;
            _lastFieldOfView = _camera.fieldOfView;
        }
        foreach (Transform t in _tranformsToWatch)
        {
            if (t.hasChanged)
            {
                _currentSample = 0;
                t.hasChanged = false;
            }
        }
    }
    private void SetShaderParameters()
    {
        rayTracingShader.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);
        rayTracingShader.SetMatrix("_CameraInverseProjection", _camera.projectionMatrix.inverse);
        rayTracingShader.SetTexture(0, "_SkyboxTexture", SkyboxTexture);
        rayTracingShader.SetVector("_PixelOffset", new Vector2(Random.value, Random.value));

        var l = directionLight.transform.forward;
        rayTracingShader.SetVector("_DirectionalLight", new Vector4(l.x, l.y,l.z, directionLight.intensity));

        if (_sphereBuffer != null)
            rayTracingShader.SetBuffer(0, "_Spheres", _sphereBuffer);


    }
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        SetShaderParameters();
        Render(destination);
    }

    private void Render(RenderTexture destination)
    {
        InitRenderTexture();

        //Set the target and dispatch the compute shader
        rayTracingShader.SetTexture(0, "Result", _target);
        int threadGroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(Screen.height / 8.0f);
        rayTracingShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);

        //Blit the result texture to the screen
        if (_addMaterial == null)
            _addMaterial = new Material(Shader.Find("Hidden/AddShader"));
        _addMaterial.SetFloat("_Sample", _currentSample);
        Graphics.Blit(_target, destination,_addMaterial);
        _currentSample++;
    }

    private void InitRenderTexture()
    {
        if (_target == null || _target.width != Screen.width || _target.height != Screen.height)
        {
            //release old render texture
            if (_target != null)
                _target.Release();

            _target = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            _target.enableRandomWrite = true;
            _target.Create();
            _currentSample = 0;
        }
    }
}
