using System.Collections.Generic;

namespace CBS.Models
{
    public class CBSRecipeContainer
    {
        public Dictionary<string, CBSItemRecipe> Recipes;

        public bool HasRecipe(string itemID)
        {
            if (Recipes == null)
                return false;
            return Recipes.ContainsKey(itemID);
        }

        public CBSItemRecipe GetRecipe(string itemID)
        {
            if (Recipes == null)
                return null;
            try
            {
                return Recipes[itemID];
            }
            catch
            {
                return null;
            }
        }

        public void AddOrUpdateRecipe(string itemID, CBSItemRecipe recipe)
        {
            if (Recipes == null)
                Recipes = new Dictionary<string, CBSItemRecipe>();
            Recipes[itemID] = recipe;
        }

        public void RemoveRecipe(string itemID)
        {
            if (Recipes == null)
                return;
            if (Recipes.ContainsKey(itemID))
            {
                Recipes.Remove(itemID);
            }
        }

        public void Duplicate(string fromItemID, string toItemID)
        {
            if (Recipes == null)
                return;
            if (Recipes.TryGetValue(fromItemID, out var recipeData))
            {
                Recipes[toItemID] = recipeData;
            }
        }
    }
}
