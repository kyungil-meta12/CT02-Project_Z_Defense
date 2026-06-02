using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TurretPartsProgressionApplier : MonoBehaviour
{
    [SerializeField] private TurretPartsProgressionSO partsProgressionProfile;
    [SerializeField] private Transform partsRoot;
    [SerializeField] private bool applyOnStart;
    [SerializeField] private int startLevel = 1;

    private readonly List<Transform> partTransforms = new List<Transform>(16);
    private readonly List<string> activePartNames = new List<string>(8);

    private void Reset()
    {
        RefreshPartsRoot();
    }

    private void Start()
    {
        if (applyOnStart)
        {
            Apply(startLevel);
        }
    }

    public void SetProfile(TurretPartsProgressionSO partsProgressionProfile_)
    {
        partsProgressionProfile = partsProgressionProfile_;
    }

    public void Apply(int level)
    {
        level = Mathf.Max(1, level);
        CachePartTransforms();

        if (partsProgressionProfile == null)
        {
            SetAllPartsActive(false);
            return;
        }

        partsProgressionProfile.GetActivePartNames(level, activePartNames);

        for (int i = 0; i < partTransforms.Count; i++)
        {
            Transform partTransform = partTransforms[i];
            if (partTransform == null)
            {
                continue;
            }

            bool shouldActive = activePartNames.Contains(partTransform.name);
            partTransform.gameObject.SetActive(shouldActive);
        }
    }

    private void RefreshPartsRoot()
    {
        if (partsRoot == null)
        {
            Transform foundRoot = transform.Find("UpgradeParts");
            if (foundRoot == null)
            {
                foundRoot = transform.Find("PartsRoot");
            }

            partsRoot = foundRoot;
        }
    }

    private void CachePartTransforms()
    {
        RefreshPartsRoot();
        partTransforms.Clear();

        if (partsRoot == null)
        {
            Debug.LogWarning("[TurretPartsProgressionApplier] Parts root is missing. Create/assign UpgradeParts or PartsRoot.", this);
            return;
        }

        for (int i = 0; i < partsRoot.childCount; i++)
        {
            Transform child = partsRoot.GetChild(i);
            if (child != null)
            {
                partTransforms.Add(child);
            }
        }
    }

    private void SetAllPartsActive(bool isActive)
    {
        for (int i = 0; i < partTransforms.Count; i++)
        {
            Transform partTransform = partTransforms[i];
            if (partTransform != null)
            {
                partTransform.gameObject.SetActive(isActive);
            }
        }
    }
}
