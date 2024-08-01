namespace Jaket.World;

using UnityEngine;
using UnityEngine.UI;

using Jaket.Net;

/// <summary> Class that manages voting for the skip of a cutscene or an option at 2-S. </summary>
public class Votes
{
    /// <summary> Loads the vote system. </summary>
    public static void Load() => Events.OnLoaded += () =>
    {
        if (LobbyController.Online && Tools.Scene == "Level 2-S") Init2S();
    };

    /// <summary> Replaces Mirage with Virage, patches buttons and other minor stuff. </summary>
    public static void Init2S()
    {
        var fallen = Tools.ObjFind("Canvas/PowerUpVignette/Panel/Aspect Ratio Mask/Fallen");
        for (int i = 0; i < 4; i++)
            fallen.transform.GetChild(i).GetComponent<Image>().sprite = null;

        Tools.ResFind<SpritePoses>(sp => Tools.IsReal(sp) && sp.copyChangeTo.Length > 0, sp => sp.poses = null);
    }

    /// <summary> Changes the name of the character to Virage. </summary>
    public static void Name(Text dialog, ref string name)
    {
        dialog.text = dialog.text.Replace("Mirage", "Virage");

        var tex = dialog.font.material.mainTexture; // aspect ratio of the font texture must always be 1
        if (tex.width != tex.height) dialog.font.RequestCharactersInTexture("I", Mathf.Max(tex.width, tex.height));

        dialog.font.RequestCharactersInTexture("3", 22);
        dialog.font.GetCharacterInfo('3', out var info, 22);

        name = name.Replace("MIRAGE:", $"VIRAG <quad size=18 x={info.uvBottomLeft.x:0.0000} y={info.uvBottomLeft.y:0.0000} width={info.uvTopRight.x - info.uvBottomLeft.x:0.0000} height={info.uvTopRight.y - info.uvBottomLeft.y:0.0000}> :".Replace(',', '.'));
    }
}
