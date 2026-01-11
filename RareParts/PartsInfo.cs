using System.Collections.Generic;

namespace RareParts;

public static class PartsInfo
{
    /// <summary>
    /// Parts that are repairable from higher condition only, in vanilla game they are not repairable at all.
    /// </summary>
    public static HashSet<string> SpecialRepairableParts { get; } = [];
    /// <summary>
    /// Minimal condition to repair a part from the list of special repairable parts.
    /// </summary>
    public static float SpecialRepairablePartsMinCondition { get; set; } = 0.85f;
    
    /// <summary>
    /// Minimal condition to repair a normal repairable part (set by game, not configurable).
    /// </summary>
    public const float NormalRepairablePartsMinCondition = 0.15f;
    
    /// <summary>
    /// Part that can not be repaired.
    /// </summary>
    public static HashSet<string> NonRepairableParts { get; } = [];
        
    /// <summary>
    /// Parts that are banned from shops and can be found only in junkyards, barns, or ripped off cars.
    /// </summary>
    public static HashSet<string> RareParts { get; } = [];
}