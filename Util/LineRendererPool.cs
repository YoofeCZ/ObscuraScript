using System.Collections.Generic;
using UnityEngine;

// Pooling pro link efekty (LineRenderer)
public class LineRendererPool : MonoBehaviour
{
    [Header("Prefab link efektu (LineRenderer)")]
    public LineRenderer prefab;

    private readonly Queue<LineRenderer> pool = new Queue<LineRenderer>();


    public LineRenderer Get()
    {
        if (pool.Count > 0)
            return pool.Dequeue();
        return Instantiate(prefab, transform);
    }

    public void Return(LineRenderer lr)
    {
        lr.gameObject.SetActive(false);
        pool.Enqueue(lr);
    }
}