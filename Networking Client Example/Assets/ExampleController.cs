using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExampleController : MonoBehaviour
{
    static public Networking networking;
    public GameObject obj;

    void Start()
    {
        networking = gameObject.AddComponent<Networking>();
        networking.uri = "https://localhost:5001/";
        networking.obj = obj;
        networking.Authorize(networking.StartSyncLoop);
    }

    public void AddCube()
    {
        ObjSync.NetInstantiate(obj, networking, new Vector3(0, 2, 0), Quaternion.identity);
    }
}
