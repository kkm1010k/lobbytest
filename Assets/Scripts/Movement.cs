using Unity.Netcode;    
using UnityEngine;

public class Movement : NetworkBehaviour
{
    private void Update()
    {
        if (!IsOwner)
        {
            return;
        }
        
        if (Input.GetKeyDown(KeyCode.W))
        {
            transform.position += new Vector3(0,0,1);
        }
        else if (Input.GetKeyDown(KeyCode.S))
        {
            transform.position += new Vector3(0,0,-1);
        }
        if (Input.GetKeyDown(KeyCode.A))
        {
            transform.position += new Vector3(-1,0,0);
        }
        else if (Input.GetKeyDown(KeyCode.D))
        {
            transform.position += new Vector3(1,0,0);
        }
    }
}
