namespace Jaket.Content;

using UnityEngine;

using Jaket.Net;
using UnityEngine.UIElements;

/// <summary> All teams. Teams needed for PvP mechanics. </summary>
public enum Team
{
    Yellow, Red, Fraud, Blue, Pink, Purple, Cyan, White
}

/// <summary> Extension class that allows you to get team data. </summary>
public static class TeamExtensions
{
    /// <summary> Returns the team color, used only in the interface. </summary>
    public static Color Color(this Team team) => team switch
    {
        Team.Yellow => new( 1f, .8f, .3f),
        Team.Red    => new( 1f, .2f, .1f),
        Team.Fraud  => new( 0f,  0f,  0f),
        Team.Blue   => new( 0f, .5f,  1f),
        Team.Pink   => new( 1f, .4f, .8f),
        Team.Purple => new(.7f,  0f,  1f),
        Team.Cyan   => new( 0f,  1f,  1f),
        Team.White  => new( 1f,  1f,  1f),
        _ => new(1f, 1f, 1f)
    };

    public static Color LightColor(this Team team) => team switch
    {
        Team.Red    => Team.Yellow.Color(),
        Team.Fraud  => Team.Yellow.Color(),
        Team.Blue   => Team.Yellow.Color(),
        Team.Pink   => team.Color() * 1.2f,
        Team.Purple => team.Color() * 1.5f,
        _ => team.Color()
    };

    public static Color CoinColor(this Team team) => team switch
    {
        Team.Fraud => Team.Yellow.Color(),
        _ => team.Color()
    };

    public static Color UIColor(this Team team) => team switch
    {
        Team.Fraud => new(.3f, .3f, .3f),
        _ => team.Color()
    };

    /// <summary> Whether this team is allied with the player. </summary>
    public static bool Ally(this Team team) => team != Team.White && team == Networking.LocalPlayer.Team || !LobbyController.PvPAllowed;
}
