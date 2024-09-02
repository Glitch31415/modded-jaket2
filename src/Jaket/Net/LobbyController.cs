namespace Jaket.Net;

using Steamworks;
using Steamworks.Data;
using System;
using System.Linq;
using UnityEngine;

using Jaket.Assets;
using Jaket.IO;
using Jaket.UI.Dialogs;

/// <summary> Lobby controller with several useful methods and properties. </summary>
public class LobbyController
{
    static Chat chat => Chat.Instance;
    /// <summary> The current lobby the player is connected to. Null if the player is not connected to any lobby. </summary>
    public static Lobby? Lobby;
    public static bool Online => Lobby != null;
    public static bool Offline => Lobby == null;

    /// <summary> Id of the last lobby owner, needed to track the exit of the host and for other minor things. </summary>
    public static SteamId LastOwner;
    /// <summary> Whether the player owns the lobby. </summary>
    public static bool IsOwner;

    /// <summary> Whether a lobby is creating right now. </summary>
    public static bool CreatingLobby;
    /// <summary> Whether a list of public lobbies is being fetched right now. </summary>
    public static bool FetchingLobbies;

    /// <summary> Whether PvP is allowed in this lobby. </summary>
    public static bool PvPAllowed => Lobby?.GetData("pvp") == "True";
    /// <summary> Whether cheats are allowed in this lobby. </summary>
    // P-1 and P-2 boss doors like to get stuck after I die - so cheats are enabled there
    public static bool CheatsAllowed => true;
    /// <summary> Whether mods are allowed in this lobby. </summary>
    public static bool ModsAllowed => true;
    /// <summary> Whether bosses must be healed after death in this lobby. </summary>
    public static bool HealBosses => Lobby?.GetData("heal-bosses") == "True";
    /// <summary> Number of percentages that will be added to the boss's health for each player. </summary>
    public static float PPP;

    /// <summary> Scales health to increase difficulty. </summary>
    public static void ScaleHealth(ref float health) => health *= 1f + Math.Min(Lobby?.MemberCount - 1 ?? 1, 1) * PPP;
    /// <summary> Whether the given lobby is created via Multikill. </summary>
    public static bool IsMultikillLobby(Lobby lobby) => lobby.Data.Any(pair => pair.Key == "mk_lobby");
    public static int MaxPlayerCount = 999;
    /// <summary> Creates the necessary listeners for proper work. </summary>
    public static void Load()
    {
        // get the owner id when entering the lobby
        SteamMatchmaking.OnLobbyEntered += lobby =>
        {
            if (lobby.Owner.Id != 0L) LastOwner = lobby.Owner.Id;

            // if (IsMultikillLobby(lobby))
            // {
            //     LeaveLobby();
            //     Bundle.Hud("lobby.mk");
            // }
        };
        // and leave the lobby if the owner has left it
        SteamMatchmaking.OnLobbyMemberLeave += (lobby, member) =>
        {
            if (member.Id == LastOwner) LeaveLobby();
        };

        // put the level name in the lobby data so that it can be seen in the public lobbies list
        Events.OnLoaded += () => Lobby?.SetData("level", MapMap(Tools.Scene));
        // if the player exits to the main menu, then this is equivalent to leaving the lobby
        Events.OnMainMenuLoaded += () => LeaveLobby(false);
    }

    /// <summary> Is there a user with the given id among the members of the lobby. </summary>
    public static bool Contains(uint id) => Lobby?.Members.Any(member => member.Id.AccountId == id) ?? false;

    /// <summary> Returns the member at the given index or null. </summary>
    public static Friend? At(int index) => Lobby?.Members.ElementAt(Math.Min(Math.Max(index, 0), Lobby.Value.MemberCount));

    /// <summary> Returns the index of the local player in the lits of members. </summary>
    public static int IndexOfLocal() => Lobby?.Members.ToList().FindIndex(member => member.IsMe) ?? 0;

    #region control

    /// <summary> Asynchronously creates a new lobby with default settings and connects to it. </summary>
    public static void CreateLobby()
    {
        if (Lobby != null || CreatingLobby) return;
        Log.Debug("Creating a lobby...");

        CreatingLobby = true;
        SteamMatchmaking.CreateLobbyAsync(MaxPlayerCount).ContinueWith(task =>
        {
            CreatingLobby = false; IsOwner = true;
            Lobby = task.Result;

            Lobby?.SetJoinable(true);
            Lobby?.SetPrivate();
            Lobby?.SetData("jaket", "true");
            Lobby?.SetData("name", $"{SteamClient.Name}'s Lobby");
            Lobby?.SetData("level", MapMap(Tools.Scene));
            Lobby?.SetData("pvp", "True");
            Lobby?.SetData("cheats", "False");
            Lobby?.SetData("mods", "True");
            Lobby?.SetData("heal-bosses", "True");
        });
    }

    /// <summary> Leaves the lobby. If the player is the owner, then all other players will be thrown into the main menu. </summary>
    public static void LeaveLobby(bool loadMainMenu = true)
    {
        Log.Debug("Leaving the lobby...");

        if (Online) // free up resources allocated for packets that have not been sent
        {
            Networking.Server.Close();
            Networking.Client.Close();
            Pointers.Free();

            Lobby?.Leave();
            Lobby = null;
        }

        // load the main menu if the client has left the lobby
        if (!IsOwner && loadMainMenu) Tools.Load("Main Menu");

        Networking.Clear();
        Events.OnLobbyAction.Fire();
    }

    /// <summary> Opens Steam overlay with a selection of a friend to invite to the lobby. </summary>
    public static void InviteFriend() => SteamFriends.OpenGameInviteOverlay(Lobby.Value.Id);

    /// <summary> Asynchronously connects the player to the given lobby. </summary>
    public static void JoinLobby(Lobby lobby)
    {
        if (Lobby?.Id == lobby.Id) { Bundle.Hud("lobby.join-yourself"); return; }
        Log.Debug("Joining a lobby...");

        // leave the previous lobby before join the new, but don't load the main menu
        if (Online) LeaveLobby(false);

        lobby.Join().ContinueWith(task =>
        {
            if (task.Result == RoomEnter.Success)
            {
                IsOwner = false;
                Lobby = lobby;
            }
            else Log.Warning($"Couldn't join a lobby. Result is {task.Result}");
        });
    }

    #endregion
    #region codes

    /// <summary> Copies the lobby code to the clipboard. </summary>
    public static void CopyCode()
    {
        GUIUtility.systemCopyBuffer = Lobby?.Id.ToString();
        if (Online) Bundle.Hud("lobby.copied");
    }

    /// <summary> Joins by the lobby code from the clipboard. </summary>
    public static void JoinByCode()
    {
        if (ulong.TryParse(GUIUtility.systemCopyBuffer, out var code)) JoinLobby(new(code));
        else Bundle.Hud("lobby.failed");
    }

    #endregion
    #region browser

    /// <summary> Asynchronously fetches a list of public lobbies. </summary>
    public static void FetchLobbies(Action<Lobby[]> done)
    {
        FetchingLobbies = true;
        SteamMatchmaking.LobbyList.RequestAsync().ContinueWith(task =>
        {
            FetchingLobbies = false;
            done(task.Result.Where(l => l.Data.Any(p => p.Key == "jaket" || p.Key == "mk_lobby")).ToArray());
        });
    }

    /// <summary> Maps the map name so that it is more understandable to an average player. </summary>
    public static string MapMap(string map) => map switch
    {
        "Tutorial"       => "Tutorial",
        "uk_construct"   => "Sandbox",
        "Endless"        => "Cyber Grind",
        "CreditsMuseum2" => "Museum",
        "Intermission1"  => "Intermission",
        "Intermission2"  => "Intermission",
        "Level 4-S"      => "Myth",

        // custom level normalization
        // Ultrabus
        "UltrabusLmao" => "Ultrabus",

        // levels in the Purgatorio campaign
        // Rubicon/Carcass
        "remphase.hydraxous.rubicon.first"  => "Rubicon-1",
        "remphase.hydraxous.rubicon.second" => "Rubicon-2",

        // Envy
        "RaifuLostFieldsenvyb"       => "Envy-1",
        "raifuenvypalaceforbundle"   => "Envy-2",
        "RaifuCastleManiacTheReal"   => "Envy-3",
        "RaifuEveryStarInTheSkyReal" => "Envy-4",

        // Indulgence
        "mag.indulgence.thedeathofparadigm" => "Indulgence-1",

        // Paradiso
        "frizou.paradiso.moonFirst" => "Paridiso 1-1",

        // Layers Of Grief
        "QoDaX.BargainingFirst" => "Bargaining-1",
        "QoDaX.AcceptanceFirst" => "Acceptance-1",

        // Where The Streets Have No Name
        "pkpseudo-nonamestreets" => "WTSHNN",

        // Prelude Xtreme
        "SSSoap:PX-0-1" => "PreludeXtreme-1",
        "SSSoap:PX-0-2" => "PreludeXtreme-2",

        // Violence Encore ::: Eyes Of Death
        "brushtromein-7-1-1"     => "V. Encore 7-1-1",
        "brushtromein-7-3-1-new" => "V. Encore 7-3-1",

        // MegaFraud
        "megacheb.tasb" => "MegaFraud",

        // Fraudulence
        "Spelunky.FRAUDULENCE_FIRST"  => "Fraudulence-1",
        "Spelunky.FRAUDULENCE_SECOND" => "Fraudulence-2",

        // Fraud ::: Higher Than The Black Sky
        "82.Fraud.HigherTTBS" => "FraudHTTBS-2",

        // Finale
        "fruitc.finale"   => "Finale-1",
        "fruic.finale2"   => "Finale-2",
        "fruitc.finale2b" => "Finale-2B",

        // The Cheb Museum
        "chebm.essentials" => "Cheb Museum",

        // Minecraft
        "ceo_of_gaming.overworld.1"      => "Minecraft O-1",
        "ceo_of_gaming.overworld.2"      => "Minecraft O-2",
        "ceo_of_gaming.minecraft.nether" => "Minecraft N-1",

        // Cult Of Dopefish
        "willem1321.cultofdopefish" => "CultOfDopefish",

        // The Cyber Grind?
        "Rude.Jam.cool.level" => "TheCyberGrind?",

        // Bloody Tears
        "riko.uk.bloodytears" => "Bloody Tears",

        // The Weight Of The World
        "TheWeightOfTheWorldWrath.Gerigrape9" => "Pandemonium",

        // uk_flatgrass2
        "willem1321-ukflatgrass2" => "uk_flatgrass2",

        // V3's Showdown
        "t.trinity.v3" => "V3's Showdown",

        // // Epitaph //
        "willem1321-epitaph" => "Epitaph",

        // SkillTests
        "willem1321.theskilltest" => "SkillTest 1",
        "willem1321-skilltest2"   => "SkillTest 2",
        "willem1321-premonitions" => "SkillTest 2.5",

        _ => map.StartsWith("Level ") ? map.Substring("Level ".Length) : map
    };

    #endregion
}
