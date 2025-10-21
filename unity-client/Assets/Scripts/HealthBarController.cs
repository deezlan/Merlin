using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

/// <summary>
/// Controls both players' health bars and synchronizes damage across the network.
/// </summary>
public class HealthBarController : MonoBehaviourPunCallbacks, IOnEventCallback
{
    [Header("Player HealthBars")]
    [SerializeField] private HealthBar playerAHealthBar;
    [SerializeField] private HealthBar playerBHealthBar;

    private const byte DamageEventCode = 5; // custom Photon event ID

    private void Awake()
    {
        if (playerAHealthBar == null || playerBHealthBar == null)
        {
            Debug.LogError("Assign both PlayerA and PlayerB health bars in the Inspector!");
        }
    }

    private void OnEnable() => PhotonNetwork.AddCallbackTarget(this);
    private void OnDisable() => PhotonNetwork.RemoveCallbackTarget(this);

    public float PlayerAHealth => playerAHealthBar.GetHealth();
    public float PlayerBHealth => playerBHealthBar.GetHealth();


    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != DamageEventCode) return;

        object[] data = (object[])photonEvent.CustomData;
        string targetPlayer = (string)data[0];
        float damage = (float)data[1];

        // Don't re-broadcast network event
        ApplyDamage(targetPlayer, damage, false);
    }

    /// <summary>
    /// Apply damage to target player and optionally broadcast to all clients.
    /// </summary>
    public void ApplyDamage(string targetPlayer, float damage, bool broadcast = true)
    {
        // Apply local damage
        if (targetPlayer == "PlayerA")
        {
            playerAHealthBar.TakeDamage(damage);
        }
        else if (targetPlayer == "PlayerB")
        {
            playerBHealthBar.TakeDamage(damage);
        }
        else
        {
            Debug.LogWarning($"Unknown player target: {targetPlayer}");
        }

        // Broadcast to everyone else if local action
        if (broadcast)
        {
            object[] content = new object[] { targetPlayer, damage };
            RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.All };
            SendOptions sendOptions = new SendOptions { Reliability = true };
            PhotonNetwork.RaiseEvent(DamageEventCode, content, options, sendOptions);
        }
    }

    /// <summary>
    /// Helper: deals damage to the *opponent* of the local player.
    /// </summary>
    public void DealDamageToOpponent(float damage)
    {
        string myName = PhotonNetwork.NickName;
        string target = (myName == "PlayerA") ? "PlayerB" : "PlayerA";
        ApplyDamage(target, damage, true);
    }
}
