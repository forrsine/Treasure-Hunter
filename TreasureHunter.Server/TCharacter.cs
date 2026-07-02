namespace GameServer;

public sealed class TCharacter
{
    public long ID { get; set; }
    public long UserId { get; set; }
    public int SlotIndex { get; set; }
    public string Name { get; set; } = "";
    public int Class { get; set; }
    public int Level { get; set; }
    public int Exp { get; set; }
    public int TID { get; set; }
    public int MapID { get; set; } = 1;
    public long Gold { get; set; }
}
