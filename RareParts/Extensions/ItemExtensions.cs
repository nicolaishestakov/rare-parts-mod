namespace RareParts;

public static class ItemExtensions
{
    extension(BaseItem item)
    {
        public bool IsMap => item.ID.ToLower().Contains("specialmap");
        public bool IsCase => item.ID.ToLower().Contains("specialcase");
        public bool IsRare => PartsInfo.RareParts.Contains(item.ID);
        public bool IsSpecialRepairable => PartsInfo.SpecialRepairableParts.Contains(item.ID);
        public bool IsNonRepairable => PartsInfo.NonRepairableParts.Contains(item.ID);

        public float MinConditionToRepair =>
            item.IsSpecialRepairable ? PartsInfo.SpecialRepairablePartsMinCondition :
            item.IsNonRepairable ? 1.0f : PartsInfo.NormalRepairablePartsMinCondition;
    }

    public static (int MapCount, int CaseCount) GetMapAndCaseCounts(this Il2CppSystem.Collections.Generic.List<BaseItem> items)
    {
        int mapCount = 0;
        int caseCount = 0;
        foreach (var item in items)
        {
            if (item.IsMap)
            {
                mapCount++;
            }
            else if (item.IsCase)
            {
                caseCount++;
            }
        }
        
        return (mapCount, caseCount);
    }
}