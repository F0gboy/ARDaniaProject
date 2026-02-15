using UnityEngine;

[CreateAssetMenu(fileName = "Element", menuName = "Elements/Element Type")]
public class ElementTypeSO : ScriptableObject
{
    [Header("Identity")]
    public ElementID id;
    [Header("Visuals")]
    public GameObject prefab;
}