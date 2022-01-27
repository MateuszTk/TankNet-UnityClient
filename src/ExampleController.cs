using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExampleController : MonoBehaviour
{
    static public Networking networking;
    public GameObject obj;

    GameObject inst;

    void Start()
    {
        networking = gameObject.AddComponent<Networking>();
        networking.uri = "http://localhost:5000/";
        networking.obj = obj;
        networking.Authorize(networking.StartSyncLoop);
    }

    public void AddCube()
    {
        inst = ObjSync.NetInstantiate(obj, networking, new Vector3(0, 2, 0), Quaternion.identity, true);
    }

    public void UploadParts()
    {
        inst.GetComponent<ObjSync>().UploadChildren();
    }
}
