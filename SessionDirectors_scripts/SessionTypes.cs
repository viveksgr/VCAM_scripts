// SessionTypes.cs
public enum ContextId { A = 0, B = 1, C = 2 }

public enum ThermodeCondition
{
    NoThermode,
    ThermodeControl,
    ThermodeNoControl
}

[System.Serializable]
public struct ContextAssignment
{
    public ContextId context;
    public ThermodeCondition condition;
}

[System.Serializable]
public struct ContextInstruction
{
    public ContextId id;
    public string title;
    [UnityEngine.TextArea] public string body;
}

[System.Serializable]
public struct BreakInstruction
{
    public string title;
    [UnityEngine.TextArea] public string body;
}

