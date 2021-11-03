using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Events;

/*********************************************
 * Anifie NFT management service 
 * Note: This code in C# is primary designed to run on Unity, 
 * but can be used on other .net enviroments
 * Created by: Anifie - 2021
 *********************************************/ 

// OpenSea NFTs array
[Serializable]
public class OpenSeaNFTs
{
    public OpenSeaAsset[] assets;
}

// OpenSea NFT data class
// Compatible with OpenSea API
[Serializable]
public class OpenSeaAsset
{
    public string id;               // NFT id
    public string name;             // NFT name
    public string description;      // NFT description
    public OpenSeaOwner owner;      // NFT owner data
    public string asset_contract;   // NFT contract address
    public string token_id;         // The token ID of the ERC721 asset
    public string external_link;    // External link to the original website for the item
    public string image_url;        // An image for the item
    public string background_color; // The background color to be displayed with the item
    public string image_preview_url; // An image for preview the item
    public string image_original_url; // Original url of the item's image
    public string animation_url;        // Animation url
    public string animation_original_url; // Animation original url
    public string permalink;        
    public string collection;       // Collection name
    public string decimals;         // Token decimals
    public string token_metadata;   // Token metadata
    public string sell_orders;      // Number of sell orders
    public string creator;          // NFT creator
    public string traits;           // A list of traits associated with the item(see traits section)
    public string last_sale;        // When this item was last sold (null if there was no last sale)

    // To interface with Unity Sprite
    private Sprite image;
    public Sprite Image { get { return image;  } set { image = value; } }
}

// Description of the NFT owner
[Serializable]
public class OpenSeaOwner
{
    public string address; // The Ethereum wallet address that uniquely identifies this account.
    public string profile_img_url; // An auto-generated profile picture to use for this wallet address. To get the user's Ethmoji avatar, use the Ethmoji SDK.
    public string user; // An object containing username, a string for the the OpenSea username associated with the account. Will be null if the account owner has not yet set a username on OpenSea.
    public string config; // A string representing public configuration options on the user's account, including affiliate and affiliate_requested for OpenSea affiliates and users waiting to be accepted as affiliates.
}

// NFT order definitions according to OpenSea API
public enum ordersByNFT
{
    none,
    token_id,
    sale_date,
    sale_count,
    visitor_count,
    sale_price
}


// Main OpenSea management class
// Note: This is a Unity monobehaviour class
// if you want to run this on standard .NET enviroment you need to start this class and call the Update each time frame (ex. 30 fps)
public class ManageOpenSeaNFT : MonoBehaviour
{
    public static ManageOpenSeaNFT instance; // Instance of this class to be used by other services
    public static List<OpenSeaAsset> userNFTassets = new List<OpenSeaAsset>(); // List of user NFT assets

    // OpenSea API description
    private string endPoint = "https://api.opensea.io/api"; // Endpoint
    private string version = "/v1";         // API version
    private string getAssets = "/assets";   // Get Assets  
    private string apiKey = ""; // You can skip APi Key for test enviroment, but you need to request a API Key for production;
    // Get Assets Url
    private string GetAssets { get { return endPoint + version + getAssets; } }

    // Awake is called at the system start
    private void Awake()
    {
        // Avoid launch this instance twice
        if (instance != null)
        {
            DestroyImmediate(this.gameObject);
            return;
        }
        // Register this instance
        instance = this;
        // Keep it running trhough scenes (Unity feature)
        DontDestroyOnLoad(this);
    }

    // Start is called before the first frame update
    void Start()
    {
        /* To test the get assets feature, uncomment this code
        GetUserAssets((response) => {
            try
            {
                OpenSeaNFTs nfts = JsonUtility.FromJson<OpenSeaNFTs>(response);
                if ((nfts.assets != null) && (nfts.assets.Length > 0))
                {
                    foreach (OpenSeaAsset asset in nfts.assets)
                        Debug.Log("** Response " + asset.name + " by " + asset.owner.user + " " + asset.owner.address);
                } else
                {
                    Debug.Log("** Response No Assets");
                }
            }
            catch (Exception err)
            {
                Debug.Log("OpenSeaNFTs JSON Error: " + err.Message);
                //testDisplay.text += "Catch error: " + err.Message + "\n";
            }

        });
        */ 
    }

    // Update is called once per frame
    void Update()
    {
        // Update is not used at this service, but can be used for assets pooling 
    }

    // Request all assets in the catalog. 
    // Need to be called multiple times incrementing the _offset by _limit
    public void GetUserAssets(Action<string> callback, int _offset, int _limit)
    {
        GetUserAssets("", ordersByNFT.none, callback, _offset, _limit);
    }

    // Get assets from an owner
    public void GetUserAssets(string _ownerId, Action<string> callback, int _offset = 0, int _limit = 25)
    {
        GetUserAssets(_ownerId, ordersByNFT.sale_count, callback, _offset, _limit);
    }

    // Get Assets
    // _ownerId: Owner address
    // _order: Filter NFTs by order state
    // _callback: Delegate call back with the JSON reponse
    // _offset: Offset for the catalog query
    // _limit: Max. number of assets on this call (Max. 50)
    public void GetUserAssets(string _ownerId, ordersByNFT _order, Action<string> _callback, int _offset, int _limit)
    {
        // Add query parameters
        Dictionary<string, string> keys = new Dictionary<string, string>();
        if (_ownerId != "")
            keys.Add("owner", _ownerId);
        if (_order != ordersByNFT.none)
            keys.Add("order_by", _order.ToString().ToLower());
        keys.Add("order_direction", "desc");
        keys.Add("offset", _offset.ToString());
        keys.Add("limit", _limit.ToString());
        // Call the API
        GetApi(GetAssets, keys, _callback);
    }

    // Call OpenSea API
    private void GetApi(string uri, Dictionary<string, string> keys, Action<string> callback)
    {
        // Start a couroutine to execute the request
        // Note: This is a Unity monobehaviour class
        // if you want to run this on standard .NET enviroment you need
        // to convert this is a Threat start
        StartCoroutine(DoGetApi(uri, keys, callback));
    }

    // Couroutine to call OpenSea API
    IEnumerator DoGetApi(string uri, Dictionary<string, string> keys, Action<string> callback)
    {
        bool success = false;   // Communication status
        int tries = 0;          // Increament each time we got an error reponse 

        // Do until we got a proper reponse, or keep trying for three times
        while (!success && (tries < 3))
        {
            // Unse the base URL
            string getUrl = uri;
            // And add the parameters
            if ((keys != null) && (keys.Count > 0))
            {
                getUrl += "?";
                string append = "";
                foreach (KeyValuePair<string, string> key in keys)
                {
                    getUrl += append + key.Key + "=" + key.Value;
                    append = "&";
                }
            }

            // Start a get request
            UnityWebRequest webRequest = UnityWebRequest.Get(getUrl);

            // Add the API Key if necessary
            if (apiKey != "")
                webRequest.SetRequestHeader("x-api-key", apiKey);

            // Send the request
            yield return webRequest.SendWebRequest();

            // Check the response
            switch (webRequest.result)
            {
                case UnityWebRequest.Result.Success: // We got a response
                    string result = webRequest.downloadHandler.text;
                    try
                    {
                        callback(result); // Use the callback to manage the reponse
                        success = true; // Set status as success
                    }
                    catch (Exception err)
                    {
                        // Catch any error
                        Debug.Log("Err: " + err.Message);
                        //testDisplay.text += "Catch error: " + err.Message + "\n";
                    }
                    break;
                case UnityWebRequest.Result.InProgress: // Waiting the reponse. Usually means a timeout
                    break;
                default: // Default is error
                    break;
            }
            // If we got an error or no response, keep trying
            if (!success)
            {
                tries++; // New try
                yield return new WaitForSeconds(1.0f); // wait one second before next try
            }
        }
        // If we exit after all tries, reply the callbck with an error
        if (!success)
        {
            //testDisplay.text += "Maximum retries at: " + uri + "\n";
            callback("Error");
        }
    }

    // Handle the OpenSea response
    // Use case: ManageOpenSeaNFT.instance.GetUserAssets(address, ManageOpenSeaNFT.instance.StoreNFTs);
    private void StoreNFTs(string response)
    {
        try
        {
            // Parse the JSON response
            OpenSeaNFTs openSeaNFTs = JsonUtility.FromJson<OpenSeaNFTs>(response);
            // If we got a valid reponse
            if ((openSeaNFTs != null) && (openSeaNFTs.assets != null) && (openSeaNFTs.assets.Length > 0))
            {
                // Clear the assets cache
                userNFTassets.Clear();
                // For each assets in the response
                for (int i = 0; i < openSeaNFTs.assets.Length; i++)
                {
                    // Get the presentation image
                    string image_preview_url = openSeaNFTs.assets[i].image_preview_url;
                    // If it is a valid item
                    if ((openSeaNFTs.assets[i].name != "") &&
                        (image_preview_url != ""))
                    {
                        int index = userNFTassets.Count;
                        // Store item at the cache
                        userNFTassets.Add(openSeaNFTs.assets[i]);
                        // Request the image
                        GetWebImage(image_preview_url, index, AddNFTImage);
                    }
                }
            }
            else
            {
                Debug.Log("** Response No Assets");
            }
        }
        catch (Exception err)
        {
            Debug.Log("OpenSeaNFTs JSON Error: " + err.Message);
        }
    }

    // Callback to store the requested image
    public void AddNFTImage(int index, Sprite image)
    {
        // If it is a valid index
        if (index < userNFTassets.Count)
        {
            // Store the image at the cache 
            userNFTassets[index].Image = image;
        }
    }

    // request the NFT presentation image
    public void GetWebImage(string uri, int index, Action<int, Sprite> callback)
    {
        // Start a couroutine to execute the request
        // Note: This is a Unity monobehaviour class
        // if you want to run this on standard .NET enviroment you need
        // to convert this is a Threat start
        StartCoroutine(DoGetWebImage(uri, index, callback));
    }

    // Couroutine to request the image
    IEnumerator DoGetWebImage(string uri, int index, Action<int, Sprite> callback)
    {
        using (UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(uri))
        {
            // Send the request
            yield return webRequest.SendWebRequest();
            // Handle the reponse
            if (webRequest.isNetworkError || webRequest.isHttpError)
            {
                // Return null in case fo error
                Debug.LogError("Network error while getting " + uri);
                callback(index, null);
            }
            else
            {
                // Get the image file and convert to Unity texture.
                Texture2D result = DownloadHandlerTexture.GetContent(webRequest);
                // Convert texture to Sprite for visualization
                Sprite sprite = Sprite.Create(result, new Rect(0, 0, result.width, result.height), new Vector2(0, 0));
                // Send the response via the callback
                callback(index, sprite);
            }
        }
    }
}
