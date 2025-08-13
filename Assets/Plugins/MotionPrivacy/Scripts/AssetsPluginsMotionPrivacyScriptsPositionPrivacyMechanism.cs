using UnityEngine;

public abstract class PositionPrivacyMechanism : ScriptableObject
{
    public abstract Vector3 Apply(Vector3 input);
}