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

    public bool allow_children_upload = false;
    public int children_uploader_id = 0;

    public List<GameObject> parts = new List<GameObject>();
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
        if (positionSync)
        {
            networking.sync_objects[_entity_id].flo.Add(transform.position.x);
            networking.sync_objects[_entity_id].flo.Add(transform.position.y);
            networking.sync_objects[_entity_id].flo.Add(transform.position.z);
            if (allow_children_upload)
            {
                networking.sync_objects[_entity_id].str.Add("_ObjSync_C");
                networking.sync_objects[_entity_id].flo.Add(children_uploader_id);
                networking.NewEntity(up_id =>
                {
                    children_uploader_id = up_id;
                    networking.sync_objects[_entity_id].flo[3] = up_id;
                    networking.on_change.Add(up_id, Build);
                    entity_id = _entity_id;
                });
            }
            else
            {
                networking.sync_objects[_entity_id].str.Add("_ObjSync");
                entity_id = _entity_id;
            }
        }
    }

    public void Build()
    {
        Debug.Log("build");
        var downloader = networking.sync_objects[children_uploader_id];
        int offset = 0;
        foreach(var name in downloader.str)
        {
            foreach(var part in parts)
            {
                if (part.name == name)
                {
                    var pt = Instantiate(part, transform);
                    pt.transform.localPosition = new Vector3(downloader.flo[offset], downloader.flo[offset + 1], downloader.flo[offset + 2]);
                    offset += 3;
                    break;
                }
            }
        }
    }

    public void UploadChildren()
    {
        if (allow_children_upload && children_uploader_id > 0 && networking.sync_objects[entity_id].str.Contains("_ObjSync_C"))
        {
            var uploader = networking.sync_objects[children_uploader_id];
            foreach(Transform child in transform)
            {
                Debug.Log(child.name);
                bool ok = false;
                foreach (var part in parts)
                {
                    if (child.name.StartsWith(part.name))
                    {
                        ok = true;
                        Vector3 cpos = child.localPosition;
                        uploader.str.Add(part.name);
                        uploader.flo.Add(cpos.x);
                        uploader.flo.Add(cpos.y);
                        uploader.flo.Add(cpos.z);
                        break;
                    }
                }
                if (!ok)
                    Debug.Log("Did not find part matching to:" + child.name);
            }
            networking.changes.Push(children_uploader_id);
        }
    }

    public static GameObject NetInstantiate(GameObject obj, Networking networking, Vector3 position, Quaternion rotation, bool allow_children_upload = false)
    {
        GameObject instance = Instantiate(obj, position, rotation);
        instance.GetComponent<ObjSync>().networking = networking;
        instance.GetComponent<ObjSync>().allow_children_upload = allow_children_upload;
        return instance;
    }

    public void SetNetworking(Networking _networking)
    {
        networking = _networking;

    }
}
