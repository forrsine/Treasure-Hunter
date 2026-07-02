public static class SelectedCharacterState
{
    public static NCharacter CurrentCharacter { get; private set; }

    public static void SetCharacter(NCharacter save)
    {
        CurrentCharacter = save;
    }

    public static void Clear()
    {
        CurrentCharacter = null;
    }
}