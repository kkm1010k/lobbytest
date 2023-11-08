using System.Collections.Generic;
using Unity.Netcode;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

public class LobbyPlayer : MonoBehaviour
{
    [SerializeField] private Renderer _isReadyRenderer;

    private void OnEnable()
    {
        TestNetwork.callBacks.PlayerDataChanged += OnPlayerDataChanged;
    }

    private void OnDisable()
    {
        TestNetwork.callBacks.PlayerDataChanged -= OnPlayerDataChanged;
    }

    private void OnPlayerDataChanged(Dictionary<int, Dictionary<string, ChangedOrRemovedLobbyValue<PlayerDataObject>>> dictionary)
    {
        // foreach (var (i,j) in dictionary)
        // {
        //     foreach (var (n,m) in j)
        //     {
        //         Debug.Log(m.Value.Value);
        //     }
        // }
        Invoke( nameof(SetData),1);
    }
    
    public void SetData()
    {
        Debug.Log(gameObject.name + " set data");
        var Id = GetComponent<NetworkObject>().OwnerClientId;
        foreach (var plr in TestNetwork.joinedLobby.Players)
        {
            if (plr.Data == null) return;
            Debug.Log("data " + plr.Data[TestNetwork.PLAYER_ID].Value);
            Debug.Log("Id " + Id);
            if (plr.Data[TestNetwork.PLAYER_ID].Value == Id.ToString())
            {
                if (plr.Data[TestNetwork.PLAYER_READY].Value == true.ToString())
                {
                    print("bruh");
                    if (_isReadyRenderer != null)
                    {
                        Debug.Log("bruh2");
                        _isReadyRenderer.material.color = Color.green;
                    }
                }
                else if (plr.Data[TestNetwork.PLAYER_READY].Value == false.ToString())
                {
                    print("why");
                    if (_isReadyRenderer != null)
                    {
                        Debug.Log("why2");
                        _isReadyRenderer.material.color = Color.red;
                    }
                }
                else
                {
                    Debug.Log("wtf????");
                }
            }
        } 
    }
}
