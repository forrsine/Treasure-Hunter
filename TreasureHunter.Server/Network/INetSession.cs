namespace Network;

public interface INetSession
{
    byte[]? GetResponse();
    void Disconnected();
}
