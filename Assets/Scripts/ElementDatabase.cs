using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class ElementRecipe
{
    public ElementID ingredientA;
    public ElementID ingredientB;
    public ElementID result;
}

[CreateAssetMenu(fileName = "ElementDatabase", menuName = "Elements/Database")]
public class ElementDatabase : ScriptableObject
{
    public List<ElementTypeSO> elements = new List<ElementTypeSO>();
    
    public List<ElementRecipe> recipes = new List<ElementRecipe>();
    
    private Dictionary<ElementID, ElementTypeSO> elementLookup;
    private Dictionary<(ElementID, ElementID), ElementID> recipeLookup;
    private bool isInitialized = false;
    
    void OnEnable()
    {
        BuildLookups();
    }
    
    private void BuildLookups()
    {
        // Build element lookup
        elementLookup = new Dictionary<ElementID, ElementTypeSO>();
        foreach (var element in elements)
        {
            if (element != null)
            {
                elementLookup[element.id] = element;
            }
        }
        
        // Build recipe lookup (store both orders for fast bidirectional lookup)
        recipeLookup = new Dictionary<(ElementID, ElementID), ElementID>();
        foreach (var recipe in recipes)
        {
            recipeLookup[(recipe.ingredientA, recipe.ingredientB)] = recipe.result;
            recipeLookup[(recipe.ingredientB, recipe.ingredientA)] = recipe.result;
        }
        
        isInitialized = true;
    }
    
    public ElementTypeSO GetElement(ElementID id)
    {
        if (!isInitialized) BuildLookups();
        
        return elementLookup.ContainsKey(id) ? elementLookup[id] : null;
    }
    
    public ElementTypeSO Combine(ElementID a, ElementID b)
    {
        if (!isInitialized) BuildLookups();
        
        if (recipeLookup.TryGetValue((a, b), out var resultID))
        {
            return GetElement(resultID);
        }
        return null;
    }
    
    // Useful for UI - show what this element can combine with
    public List<(ElementID other, ElementID result)> GetPossibleCombinations(ElementID element)
    {
        if (!isInitialized) BuildLookups();
        
        var results = new List<(ElementID, ElementID)>();
        foreach (var recipe in recipes)
        {
            if (recipe.ingredientA == element)
                results.Add((recipe.ingredientB, recipe.result));
            else if (recipe.ingredientB == element)
                results.Add((recipe.ingredientA, recipe.result));
        }
        return results;
    }
    
    // Validation - call this from a custom editor or menu item
    public void ValidateDatabase()
    {
        var seen = new HashSet<(ElementID, ElementID)>();
        var duplicates = new List<string>();
        
        foreach (var recipe in recipes)
        {
            // Create normalized key (smaller ID first)
            var key = recipe.ingredientA < recipe.ingredientB 
                ? (recipe.ingredientA, recipe.ingredientB)
                : (recipe.ingredientB, recipe.ingredientA);
            
            if (seen.Contains(key))
            {
                duplicates.Add($"{recipe.ingredientA} + {recipe.ingredientB}");
            }
            seen.Add(key);
        }
        
        if (duplicates.Count > 0)
        {
            Debug.LogError($"Found {duplicates.Count} duplicate recipes: {string.Join(", ", duplicates)}");
        }
        else
        {
            Debug.Log("Database validated successfully - no duplicate recipes found.");
        }
    }
}