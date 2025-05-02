using UnityEngine;
using UnityEngine.Experimental.Rendering;

/// <summary>
/// Provides utility functions for managing compute shaders, buffers, and render textures.
/// </summary>
/// <remarks>
/// The ComputeHelper class abstracts common operations such as dispatching compute shaders, creating and resizing buffers,
/// and managing 3D textures, making it easier to work with compute shaders in Unity.
/// </remarks>
public static class ComputeHelper
{
    // Default settings for texture creation
    public const FilterMode DefaultFilterMode = FilterMode.Bilinear;
    public const GraphicsFormat DefaultGraphicsFormat = GraphicsFormat.R32G32B32A32_SFloat;

    /// <summary>
    /// Dispatches a compute shader with calculated thread group counts.
    /// </summary>
    /// <param name="cs">The compute shader to dispatch.</param>
    /// <param name="numIterationsX">Number of iterations in the X dimension.</param>
    /// <param name="numIterationsY">Number of iterations in the Y dimension.</param>
    /// <param name="numIterationsZ">Number of iterations in the Z dimension.</param>
    /// <param name="kernelIndex">The index of the kernel to dispatch.</param>
    public static void Dispatch(ComputeShader cs, int numIterationsX, int numIterationsY = 1, int numIterationsZ = 1, int kernelIndex = 0)
    {
        // Input validation
        if (cs == null)
        {
            Debug.LogError("Compute shader cannot be null.");
            return;
        }
        if (numIterationsX <= 0 || numIterationsY <= 0 || numIterationsZ <= 0)
        {
            Debug.LogError("Number of iterations must be greater than zero.");
            return;
        }

        Vector3Int threadGroupSizes = GetThreadGroupSizes(cs, kernelIndex);
        int numGroupsX = Mathf.CeilToInt(numIterationsX / (float)threadGroupSizes.x);
        int numGroupsY = Mathf.CeilToInt(numIterationsY / (float)threadGroupSizes.y);
        int numGroupsZ = Mathf.CeilToInt(numIterationsZ / (float)threadGroupSizes.z);
        cs.Dispatch(kernelIndex, numGroupsX, numGroupsY, numGroupsZ);
    }

    /// <summary>
    /// Gets the size of a struct for buffer stride calculation.
    /// </summary>
    /// <typeparam name="T">The type of the struct.</typeparam>
    /// <returns>The size of the struct in bytes.</returns>
    public static int GetStride<T>()
    {
        return System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));
    }

    /// <summary>
    /// Creates or resizes a structured buffer.
    /// </summary>
    /// <typeparam name="T">The type of data in the buffer.</typeparam>
    /// <param name="buffer">The buffer reference to create or resize.</param>
    /// <param name="count">The number of elements in the buffer.</param>
    public static void CreateStructuredBuffer<T>(ref ComputeBuffer buffer, int count)
    {
        int stride = GetStride<T>();
        bool createNewBuffer = buffer == null || !buffer.IsValid() || buffer.count != count || buffer.stride != stride;
        
        if (createNewBuffer)
        {
            Release(buffer);
            buffer = new ComputeBuffer(count, stride);
        }
    }

    /// <summary>
    /// Creates a structured buffer and initializes it with data.
    /// </summary>
    /// <typeparam name="T">The type of data in the buffer.</typeparam>
    /// <param name="buffer">The buffer reference to create or resize.</param>
    /// <param name="data">The array of data to initialize the buffer with.</param>
    public static void CreateAndInitializeStructuredBuffer<T>(ref ComputeBuffer buffer, T[] data)
    {
        if (data == null || data.Length == 0)
        {
            Debug.LogError("Data array cannot be null or empty.");
            return;
        }
        CreateStructuredBuffer<T>(ref buffer, data.Length);
        buffer.SetData(data);
    }

    /// <summary>
    /// Creates a 3D render texture with specified settings.
    /// </summary>
    /// <param name="texture">The render texture reference to create or resize.</param>
    /// <param name="size">The size of each dimension of the 3D texture.</param>
    /// <param name="name">The name to assign to the texture (optional).</param>
    public static void CreateRenderTexture3D(ref RenderTexture texture, int size, string name = "CubeMarchTexture")
    {
        var format = GraphicsFormat.R32_SFloat;
        bool needNewTexture = texture == null || !texture.IsCreated() || 
                             texture.width != size || texture.height != size || 
                             texture.volumeDepth != size || texture.graphicsFormat != format;

        if (needNewTexture)
        {
            texture?.Release();
            const int numBitsInDepthBuffer = 0;
            texture = new RenderTexture(size, size, numBitsInDepthBuffer)
            {
                graphicsFormat = format,
                volumeDepth = size,
                enableRandomWrite = true,
                dimension = UnityEngine.Rendering.TextureDimension.Tex3D
            };
            texture.Create();
        }

        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = DefaultFilterMode;
        texture.name = name;
    }

    /// <summary>
    /// Gets the thread group sizes for a compute shader kernel.
    /// </summary>
    /// <param name="compute">The compute shader to query.</param>
    /// <param name="kernelIndex">The index of the kernel to query.</param>
    /// <returns>A Vector3Int representing the thread group sizes (X, Y, Z).</returns>
    public static Vector3Int GetThreadGroupSizes(ComputeShader compute, int kernelIndex = 0)
    {
        compute.GetKernelThreadGroupSizes(kernelIndex, out uint x, out uint y, out uint z);
        return new Vector3Int((int)x, (int)y, (int)z);
    }

    /// <summary>
    /// Releases one or more compute buffers.
    /// </summary>
    /// <param name="buffers">An array of buffers to release.</param>
    public static void Release(params ComputeBuffer[] buffers)
    {
        for (int i = 0; i < buffers.Length; i++)
        {
            if (buffers[i] != null)
            {
                buffers[i].Release();
            }
        }
    }

    /// <summary>
    /// Releases one or more render textures.
    /// </summary>
    /// <param name="textures">An array of render textures to release.</param>
    public static void Release(params RenderTexture[] textures)
    {
        for (int i = 0; i < textures.Length; i++)
        {
            if (textures[i] != null)
            {
                textures[i].Release();
            }
        }
    }
}
