using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjSync : MonoBehaviour
{
    Networking networking;

    public int entity_id = 0;
    public bool master = true;
    public bool clientPhysics = false;
    public bool positionSync = false;
    //public bool rotationSync = false;
    //public bool scaleSync = false;

    Vector3 prev_pos = Vector3.zero;
    bool requested = false;

    void Start()
    {
        if (!clientPhysics && !master)
        {
            if (gameObject.TryGetComponent<Rigidbody>(out Rigidbody rb))
            {
                rb.isKinematic = true;
            }
            else if (gameObject.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb2))
            {
                rb2.isKinematic = true;
            }
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (networking.ready)
        {
            if (entity_id == 0)
            {
                if (!requested)
                {
                    networking.NewEntity(OnCreate);
                    requested = true;
                }
            }
            else
            {
                if (positionSync)
                {
                    Vector3 netpos = new Vector3(networking.sync_objects[entity_id].flo[0],
                           networking.sync_objects[entity_id].flo[1],
                           networking.sync_objects[entity_id].flo[2]);

                    if (master && Vector3.Distance(transform.position, prev_pos) > 0.1f)
                    {
                        networking.sync_objects[entity_id].flo[0] = transform.position.x;
                        networking.sync_objects[entity_id].flo[1] = transform.position.y;
                        networking.sync_objects[entity_id].flo[2] = transform.position.z;
                        networking.changes.Push(entity_id);
                        prev_pos = transform.position;
                    }
                    else if (Vector3.Distance(transform.position, netpos) > 0.1f)
                    {
                        transform.position = netpos;
                        prev_pos = netpos;
                    }
                }
            }
        }
    }

    void OnCreate(int _entity_id)
    {
        entity_id = _entity_id;
        if (positionSync)
        {
            networking.sync_objects[entity_id].flo.Add(transform.position.x);
            networking.sync_objects[entity_id].flo.Add(transform.position.y);
            networking.sync_objects[entity_id].flo.Add(transform.position.z);
            networking.sync_objects[entity_id].str.Add("ObjSync");
        }
    }

    public static void NetInstantiate(GameObject obj, Networking networking, Vector3 position, Quaternion rotation)
    {
        GameObject instance = Instantiate(obj, position, rotation);
        instance.GetComponent<ObjSync>().networking = networking;
    }
}
