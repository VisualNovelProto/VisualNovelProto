using System;
using System.IO;
using System.Text;
using UnityEngine;

#if STEAMWORKS_NET
using Steamworks;
#endif

/// <summary>
/// Central point for talking to Steamworks. Handles initialization, achievements
/// and Steam Cloud file synchronization.
/// </summary>
public sealed class SteamIntegrationManager : MonoBehaviour
{
    public static SteamIntegrationManager Instance { get; private set; }
    public static bool HasInstance => Instance != null;

    [Header("Initialization")]
    [SerializeField] bool autoInitialize = true;
    [SerializeField] bool runCallbacksInUpdate = true;
    [SerializeField] bool verboseLogging = false;

    [Header("Steam Cloud")] 
    [Tooltip("If enabled, manual and auto saves are mirrored to Steam Cloud.")]
    [SerializeField] bool enableCloudSync = true;
    [Tooltip("When loading a save we attempt to pull a fresher copy from Steam Cloud before reading the local file.")]
    [SerializeField] bool syncCloudBeforeLoad = true;

    bool _initialized;

    public bool IsInitialized => _initialized;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (autoInitialize)
            InitializeSteam();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Shutdown();
            Instance = null;
        }
    }

    void Update()
    {
#if STEAMWORKS_NET
        if (runCallbacksInUpdate && _initialized)
            SteamAPI.RunCallbacks();
#endif
    }

    void OnApplicationQuit()
    {
        Shutdown();
    }

    public bool InitializeSteam()
    {
#if STEAMWORKS_NET
        if (_initialized)
            return true;

        try
        {
            _initialized = SteamAPI.Init();
            if (_initialized)
            {
                if (verboseLogging)
                {
                    var name = SteamFriends.GetPersonaName();
                    Debug.Log($"[SteamIntegration] Steam API initialized as '{name}'.");
                }
            }
            else
            {
                Debug.LogError("[SteamIntegration] SteamAPI.Init() returned false.");
            }
        }
        catch (DllNotFoundException ex)
        {
            Debug.LogError($"[SteamIntegration] Steamworks native binaries missing: {ex.Message}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SteamIntegration] Failed to initialize Steam: {ex.Message}");
        }

        return _initialized;
#else
        if (verboseLogging)
            Debug.LogWarning("[SteamIntegration] STEAMWORKS_NET define is not enabled. Steam integration skipped.");
        return false;
#endif
    }

    public void Shutdown()
    {
#if STEAMWORKS_NET
        if (_initialized)
        {
            SteamAPI.Shutdown();
            _initialized = false;
            if (verboseLogging)
                Debug.Log("[SteamIntegration] Steam API shutdown.");
        }
#endif
    }

    bool EnsureInitialized()
    {
#if STEAMWORKS_NET
        if (_initialized)
            return true;

        if (autoInitialize)
            return InitializeSteam();
#endif
        return false;
    }

    public static bool EnsureInitializedGlobal()
    {
        return HasInstance && Instance.EnsureInitialized();
    }

    public bool UnlockAchievement(string achievementId)
    {
#if STEAMWORKS_NET
        if (string.IsNullOrEmpty(achievementId))
            return false;
        if (!EnsureInitialized())
            return false;

        bool changed = SteamUserStats.SetAchievement(achievementId);
        SteamUserStats.StoreStats();

        if (verboseLogging)
            Debug.Log($"[SteamIntegration] Unlock achievement '{achievementId}' (changed={changed}).");

        return changed;
#else
        return false;
#endif
    }

    public static bool UnlockAchievementGlobal(string achievementId)
    {
        return HasInstance && Instance.UnlockAchievement(achievementId);
    }

    public bool ClearAchievement(string achievementId)
    {
#if STEAMWORKS_NET
        if (string.IsNullOrEmpty(achievementId))
            return false;
        if (!EnsureInitialized())
            return false;

        bool changed = SteamUserStats.ClearAchievement(achievementId);
        SteamUserStats.StoreStats();

        if (verboseLogging)
            Debug.Log($"[SteamIntegration] Clear achievement '{achievementId}' (changed={changed}).");

        return changed;
#else
        return false;
#endif
    }

    public static bool ClearAchievementGlobal(string achievementId)
    {
        return HasInstance && Instance.ClearAchievement(achievementId);
    }

    public bool UploadSaveToCloud(string localPath, string contents)
    {
        if (!enableCloudSync)
            return false;

#if STEAMWORKS_NET
        if (string.IsNullOrEmpty(localPath))
            return false;
        if (!EnsureInitialized())
            return false;

        string remoteFile = ToRemoteFileName(localPath);
        byte[] payload = Encoding.UTF8.GetBytes(contents ?? string.Empty);

        bool ok = SteamRemoteStorage.FileWrite(remoteFile, payload, payload.Length);
        if (!ok)
        {
            Debug.LogWarning($"[SteamIntegration] Failed to upload '{remoteFile}' to Steam Cloud.");
            return false;
        }

        if (verboseLogging)
            Debug.Log($"[SteamIntegration] Uploaded '{remoteFile}' ({payload.Length} bytes) to Steam Cloud.");

        return true;
#else
        return false;
#endif
    }

    public static bool TryUploadSaveToCloud(string localPath, string contents)
    {
        return HasInstance && Instance.UploadSaveToCloud(localPath, contents);
    }

    public bool TrySyncCloudSaveToLocal(string localPath)
    {
        if (!enableCloudSync || !syncCloudBeforeLoad)
            return false;

#if STEAMWORKS_NET
        if (string.IsNullOrEmpty(localPath))
            return false;
        if (!EnsureInitialized())
            return false;

        string remoteFile = ToRemoteFileName(localPath);
        if (!SteamRemoteStorage.FileExists(remoteFile))
            return false;

        DateTime remoteTimeUtc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        try
        {
            long remoteSeconds = SteamRemoteStorage.GetFileTimestamp(remoteFile);
            if (remoteSeconds > 0)
                remoteTimeUtc = DateTimeOffset.FromUnixTimeSeconds(remoteSeconds).UtcDateTime;
        }
        catch (Exception ex)
        {
            if (verboseLogging)
                Debug.LogWarning($"[SteamIntegration] Could not read timestamp for '{remoteFile}': {ex.Message}");
        }

        if (File.Exists(localPath))
        {
            var localTimeUtc = File.GetLastWriteTimeUtc(localPath);
            if (localTimeUtc >= remoteTimeUtc)
                return false; // Local file is newer or same age.
        }

        int size = SteamRemoteStorage.GetFileSize(remoteFile);
        if (size <= 0)
            return false;

        byte[] buffer = new byte[size];
        int bytesRead = SteamRemoteStorage.FileRead(remoteFile, buffer, size);
        if (bytesRead <= 0)
            return false;

        if (bytesRead != size)
            Array.Resize(ref buffer, bytesRead);

        try
        {
            var directory = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllBytes(localPath, buffer);
            if (verboseLogging)
                Debug.Log($"[SteamIntegration] Downloaded Steam Cloud file '{remoteFile}' to '{localPath}'.");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SteamIntegration] Failed to write cloud save to '{localPath}': {ex.Message}");
            return false;
        }
#else
        return false;
#endif
    }

    public static bool TrySyncCloudSaveToLocalGlobal(string localPath)
    {
        return HasInstance && Instance.TrySyncCloudSaveToLocal(localPath);
    }

    string ToRemoteFileName(string localPath)
    {
        string name = Path.GetFileName(localPath);
        return string.IsNullOrEmpty(name) ? localPath : name;
    }
}
