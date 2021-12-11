using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjSync : MonoBehaviour
{
    Networking networking;

    public int entity_id = 0;
    public bool master = true;
    public bool clientPhysics = false;
    public bool transformSync = false;
    public float threshold = 0.05f;

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
        //set callback on children change
        if (!master && children_uploader_id > 0)
        {
            networking.on_change.Add(children_uploader_id, Build);
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
                if (transformSync)
                {
                    if (master && Vector3.Distance(transform.position, prev_pos) > threshold)
                    {
                        networking.sync_objects[entity_id].flo[0] = transform.position.x;
                        networking.sync_objects[entity_id].flo[1] = transform.position.y;
                        networking.sync_objects[entity_id].flo[2] = transform.position.z;

                        networking.sync_objects[entity_id].flo[3] = transform.rotation.x;
                        networking.sync_objects[entity_id].flo[4] = transform.rotation.y;
                        networking.sync_objects[entity_id].flo[5] = transform.rotation.z;
                        networking.sync_objects[entity_id].flo[6] = transform.rotation.w;

                        networking.changes.Push(entity_id);
                        prev_pos = transform.position;
                    }
                    else
                    {
                        Vector3 netpos = new Vector3(networking.sync_objects[entity_id].flo[0],
                           networking.sync_objects[entity_id].flo[1],
                           networking.sync_objects[entity_id].flo[2]);

                        if (Vector3.Distance(transform.position, netpos) > threshold)
                        {
                            transform.position = netpos;
                            transform.rotation = new Quaternion(networking.sync_objects[entity_id].flo[3],
                               networking.sync_objects[entity_id].flo[4],
                               networking.sync_objects[entity_id].flo[5],
                               networking.sync_objects[entity_id].flo[6]); ;
                            prev_pos = netpos;
                        }
                    }
                }
            }
        }
    }

    //master only
    void OnCreate(int _entity_id)
    {
        if (transformSync)
        {
            var flo = networking.sync_objects[_entity_id].flo;
            flo.Add(transform.position.x);
            flo.Add(transform.position.y);
            flo.Add(transform.position.z);
            flo.Add(transform.rotation.x);
            flo.Add(transform.rotation.y);
            flo.Add(transform.rotation.z);
            flo.Add(transform.rotation.w);

            if (allow_children_upload)
            {
                networking.sync_objects[_entity_id].str.Add("_ObjSync_C");
                flo.Add(children_uploader_id);
                networking.NewEntity(up_id =>
                {
                    children_uploader_id = up_id;
                    networking.sync_objects[_entity_id].flo[7] = up_id;
                    networking.changes.Push(_entity_id);
                    networking.changes.Push(up_id);
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

    //non master only
    public void Build()
    {
        if (children_uploader_id > 0) {
            var downloader = networking.sync_objects[children_uploader_id];
            if (downloader.str != null) {
                int offset = 0;
                foreach (var name in downloader.str)
                {
                    foreach (var part in parts)
                    {
                        if (part.name == name)
                        {
                            var pt = Instantiate(part, transform);
                            pt.transform.localPosition = new Vector3(downloader.flo[offset], downloader.flo[offset + 1], downloader.flo[offset + 2]);
                            pt.transform.localRotation = new Quaternion(downloader.flo[offset + 3], downloader.flo[offset + 4], downloader.flo[offset + 5], downloader.flo[offset + 6]);
                            offset += 3 + 4;
                            break;
                        }
                    }
                }
            }
        }
    }

    //non master only
    public void Wait4Upid()
    {
        int uid = (int)networking.sync_objects[entity_id].flo[7];
        if (uid > 0 && networking.sync_objects.ContainsKey(uid))
        {
            //don't call me again
            networking.on_change.Remove(entity_id);

            children_uploader_id = uid;
            Build();
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
                        Quaternion crot = child.localRotation;
                        uploader.str.Add(part.name);
                        uploader.flo.Add(cpos.x);
                        uploader.flo.Add(cpos.y);
                        uploader.flo.Add(cpos.z);

                        uploader.flo.Add(crot.x);
                        uploader.flo.Add(crot.y);
                        uploader.flo.Add(crot.z);
                        uploader.flo.Add(crot.w);
                        break;
                    }
                }
                if (!ok)
                    Debug.Log("Did not find part matching to:" + child.name);
            }
            networking.changes.Push(children_uploader_id);
        }
    }

    public static GameObject NetInstantiate(GameObject obj, Networking networking, Vector3 position, Quaternion rotation, bool _allow_children_upload = false)
    {
        GameObject instance = Instantiate(obj, position, rotation);
        instance.GetComponent<ObjSync>().networking = networking;
        instance.GetComponent<ObjSync>().allow_children_upload = _allow_children_upload;
        return instance;
    }

    public void SetNetworking(Networking _networking)
    {
        networking = _networking;

    }
}
