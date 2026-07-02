using System;
using System.Collections.Generic;

[Serializable]
public class CharacterDefineTable
{
    public List<CharacterDefine> characters;
}

[Serializable]
public class CharacterDefine
{
    public int classId;
    public string classKey;
    public string name;
    public string description;
    public string previewPrefabPath;
    public string gamePrefabPath;
    public int initLevel;
    public float hp;
    public float mp;
    public float attack;
    public float defense;
    public float moveSpeed;
}