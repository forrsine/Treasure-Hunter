using UnityEngine;

public interface IGameplayInput
{
    float XInput { get; }
    float YInput { get; }
    Vector3 MouseInput { get; }
    bool LeftMouseDown { get; }
}
