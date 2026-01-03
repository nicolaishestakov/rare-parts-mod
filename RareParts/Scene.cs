namespace RareParts;

public class Scene
{
    public Scene(string sceneName)
    {
        UpdateScene(sceneName);
    }
    
    public string LowerName { get; private set; } = "";

    public void UpdateScene(string sceneName)
    {
        LowerName = sceneName.ToLower();
    }
    
    public bool IsGarage => LowerName.Equals("garage") || IsChristmas || IsEaster || IsHalloween;
    public bool IsBarnOrJunkyard => IsBarn || IsJunkyard;
    public bool IsBarn => LowerName.Equals("barn");
    public bool IsJunkyard => LowerName.Equals("junkyard");
    public bool IsEaster => LowerName.Equals("easter");
    public bool IsChristmas => LowerName.Equals("christmas");
    public bool IsHalloween => LowerName.Equals("halloween");
    
};