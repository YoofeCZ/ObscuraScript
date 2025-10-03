using Obscurus.Console;
using UnityEngine;

public class PlayerConsoleCommands : MonoBehaviour, IConsoleProvider
{
    CharacterController cc;
    bool noclip;

    void Awake(){ cc = GetComponent<CharacterController>(); }

    [ConsoleCommand("noclip", "Toggle kolizí hráče.")]
    public string Noclip()
    {
        if (!cc) return "CharacterController not found.";
        noclip = !noclip;
        cc.detectCollisions = !noclip;
        cc.enableOverlapRecovery = !noclip;
        return $"noclip = {noclip}";
    }
}