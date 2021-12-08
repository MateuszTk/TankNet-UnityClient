using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System;
using Newtonsoft.Json;

[Serializable]
public class Entity
{
    public List<string> str;
    public List<float> flo;
}

[Serializable]
public class Upload
{
    public int client_id;
    public Dictionary<int, Entity> changes;
}

public class Networking : MonoBehaviour
{
    public string uri = "";
    public int client_id = 0;
    public Dictionary<int, Entity> sync_objects = new Dictionary<int, Entity>();
    public Dictionary<int, Action> on_change = new Dictionary<int, Action>();
    public Stack<int> changes = new Stack<int>();
    public bool ready = false;
    public GameObject obj;

    // authorize client and return its id
    public void Authorize(Action<int> callback)
    {
        if (uri.Length == 0)
            Debug.Log("Server address (uri) is not set");
        else
        {
            StartCoroutine(Get<int>("auth", c_id =>
            {
                if (c_id > 0)
                {
                    client_id = c_id;
                    Debug.Log("Client id: " + client_id);
                    ready = true;
                }
                else
                {
                    client_id = -1;
                }
            }, callback));
        }
    }

    public void NewEntity(Action<int> callback)
    {
        if (ready)
        {
            StartCoroutine(Get<int>("new", e_id =>
            {
                //add new entity
                Entity entity = new Entity();
                entity.flo = new List<float>();
                entity.str = new List<string>();
                sync_objects.Add(e_id, entity);
            }, callback));
        }
    }

    public void StartSyncLoop(int dummy)
    {
        StartCoroutine(SyncLoop());
    }

    IEnumerator SyncLoop()
    {
        while (true)
        {
            //waits for 5 seconds.
            yield return new WaitForSeconds(0.5f);

            Synchronize();
        }
    }

    public void Synchronize()
    {
        if (ready)
            StartCoroutine(Get<Dictionary<int, Entity>>("csync?client=" + client_id, UpdateObjects));
    }

    void UpdateObjects(Dictionary<int, Entity> items)
    {
        List<GameObject> tobuild = new List<GameObject>();
        //fetch changes from server
        foreach(var item in items)
        {
            if (sync_objects.ContainsKey(item.Key))
            {
                sync_objects[item.Key] = item.Value;
                if (on_change.ContainsKey(item.Key))
                {
                    on_change[item.Key]();
                }
            }
            else
            {
                sync_objects.Add(item.Key, item.Value);
                //if received new object of type ObjSync instantiate new corresponding GameObject
                if (item.Value.str != null)
                {
                    if (item.Value.str.Count > 0)
                    {
                        if (item.Value.str[0] == "_ObjSync" || item.Value.str[0] == "_ObjSync_C")
                        {
                            Vector3 position = new Vector3(item.Value.flo[0], item.Value.flo[1], item.Value.flo[2]);
                            var gobject = Instantiate(obj, position, Quaternion.identity);
                            gobject.GetComponent<ObjSync>().SetNetworking(this);
                            gobject.GetComponent<ObjSync>().entity_id = item.Key;
                            gobject.GetComponent<ObjSync>().master = false;
                            if (item.Value.str[0] == "_ObjSync_C")
                            {
                                gobject.GetComponent<ObjSync>().children_uploader_id = (int)item.Value.flo[3];
                                tobuild.Add(gobject);
                            }
                        }
                    }
                }

                if (on_change.ContainsKey(item.Key))
                {
                    on_change[item.Key]();
                }
            }
        }

        foreach(var go in tobuild)
        {
            go.GetComponent<ObjSync>().Build();
        }

        //upload changes to server
        UploadChanges();
    }

    void UploadChanges()
    {
        Upload upload_data = new Upload();
        upload_data.changes = new Dictionary<int, Entity>();
        upload_data.client_id = client_id;
        while (changes.Count > 0)
        {
            var change = changes.Pop();
            if(!upload_data.changes.ContainsKey(change))
                upload_data.changes.Add(change, sync_objects[change]);
        }
        string json = JsonConvert.SerializeObject(upload_data);
        if (json.Length > 4)
            Debug.Log(json);
        StartCoroutine(Post(json));
    }

    IEnumerator Post(string json)
    {
        using (UnityWebRequest webRequest = UnityWebRequest.Post(uri + "ssync", json))
        {
            webRequest.SetRequestHeader("Content-Type", "application/json");

            webRequest.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));

            // Request and wait
            yield return webRequest.SendWebRequest();

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.Log(webRequest.error);
            }
        }
    }

    IEnumerator Get<T>(string _uri, params Action<T>[] callbacks)
    {
        using (UnityWebRequest webRequest = UnityWebRequest.Get(uri + _uri))
        {
            // Request and wait
            yield return webRequest.SendWebRequest();

            string response = GetResult(webRequest);

            //execute all callbacks and pass deserialized object
            if (webRequest.result != UnityWebRequest.Result.ConnectionError && webRequest.result != UnityWebRequest.Result.ProtocolError && webRequest.result != UnityWebRequest.Result.DataProcessingError)
            {
                T parsed = JsonConvert.DeserializeObject<T>(response);
                foreach (var callback in callbacks)
                    callback(parsed);
            }
            else
            {
                ready = false;
            }
        }
    }

    string GetResult(UnityWebRequest webRequest)
    {
        switch (webRequest.result)
        {
            case UnityWebRequest.Result.ConnectionError:
            case UnityWebRequest.Result.DataProcessingError:
                Debug.LogError("Error: " + webRequest.error);
                break;
            case UnityWebRequest.Result.ProtocolError:
                Debug.LogError("HTTP Error: " + webRequest.error);
                break;
            case UnityWebRequest.Result.Success:
                string text = webRequest.downloadHandler.text;
                Debug.Log("Received: " + text);
                return text;
        }

        return "";
    }
}
