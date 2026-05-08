using UnityEngine;
using System.Collections.Generic;

public class PlayerInventory : MonoBehaviour
{
    public List<JarType> collectedJars = new List<JarType>();

    public event System.Action<JarType> OnJarCollected;

    public void CollectJar(JarType type)
    {
        if (collectedJars.Contains(type)) return; // no duplicates
        collectedJars.Add(type);
        OnJarCollected?.Invoke(type);
        Debug.Log($"Collected jar: {type}");
    }

    public bool HasJar(JarType type) => collectedJars.Contains(type);

    public bool UseJar(JarType type)
    {
        if (!collectedJars.Contains(type)) return false;
        collectedJars.Remove(type);
        return true;
    }
}