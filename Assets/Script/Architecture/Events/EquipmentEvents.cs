/// <summary>穿戴栏发生变化，UI 与自动存档收到后按事件刷新。</summary>
public readonly struct EquipmentChangedEvent
{
    public EquipmentChangedEvent(EquipmentSlotType slot)
    {
        Slot = slot;
    }

    public EquipmentSlotType Slot { get; }
}
