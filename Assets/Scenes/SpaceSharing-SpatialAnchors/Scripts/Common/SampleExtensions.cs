// Copyright (c) Meta Platforms, Inc. and affiliates.

using JetBrains.Annotations;

using Photon.Realtime;

using Meta.XR.MRUtilityKit;

using System;
using System.Runtime.InteropServices;
using System.Text;

using UnityEngine;

using Hashtable = ExitGames.Client.Photon.Hashtable;


static class SampleExtensions
{

    public static readonly Encoding EncodingForSerialization
        = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);


    public static bool IsInLoadedRoom(in this Vector3 worldPosition, bool testY = false)
    {
        if (!MRUK.Instance)
            return false;

        var room = MRUK.Instance.GetCurrentRoom();
        if (!room)
            return false;

        return room.IsPositionInRoom(worldPosition, testY);
    }

    public static bool IsInLoadedRoom([CanBeNull] this Transform transform, bool testY = false)
    {
        return transform && IsInLoadedRoom(transform.position, testY);
    }


    [NotNull]
    public static byte[] SerializeToByteArray<T>([CanBeNull] T obj) where T : new()
    {
        if (obj is null)
            return Array.Empty<byte>();

        // NOTE: Using JSON as an intermediate protocol is fairly inefficient at runtime compared to a more direct
        // protocol, but it is safe, and the code is brief for the sake of this example.

        var json = JsonUtility.ToJson(obj, prettyPrint: false) ?? "{}";

        return EncodingForSerialization.GetBytes(json);
    }

    public static T DeserializeFromByteArray<T>([NotNull] byte[] bytes) where T : new()
    {
        // NOTE: Using JSON as an intermediate protocol is fairly inefficient at runtime compared to a more direct
        // protocol, but it is safe, and the code is brief for the sake of this example.

        var json = EncodingForSerialization.GetString(bytes);

        return JsonUtility.FromJson<T>(json);
    }


    public static string Serialize(in this Guid guid, string prefix = "")
        => $"{prefix}{guid:N}";


    public static int GetSerializedByteCount(this string str, int lengthHeaderSz = sizeof(ushort))
        => lengthHeaderSz + (string.IsNullOrEmpty(str) ? 0 : EncodingForSerialization.GetByteCount(str));


    public static string Brief(in this Guid guid)
        => $"{guid.ToString("N").Remove(8)}[..]";


    public static string ForLogging(this MRUK.LoadDeviceResult status)
        => $"{status}({(int)status})"; // MRUK overrides OVRPlugin.Result values with other meanings

    public static string ForLogging(this OVRSpatialAnchor.OperationResult status, bool details = true)
        => StatusForLogging(status, details);

    public static string ForLogging(this OVRColocationSession.Result status, bool details = true)
        => StatusForLogging(status, details);

    public static string ForLogging(this OVRAnchor.EraseResult status, bool details = true)
        => StatusForLogging(status, details);

    public static string ForLogging(this OVRAnchor.SaveResult status, bool details = true)
        => StatusForLogging(status, details);

    public static string ForLogging(this OVRAnchor.ShareResult status, bool details = true)
        => StatusForLogging(status, details);

    public static string ForLogging(this OVRAnchor.FetchResult status, bool details = true)
        => StatusForLogging(status, details);

    public static string StatusForLogging<T>(T status, bool details) where T : struct, Enum
        => details ? $"{(OVRPlugin.Result)(object)status}({(int)(object)status}){StatusExtraDetails(status)}"
                   : $"{(OVRPlugin.Result)(object)status}({(int)(object)status})";

    public static string StatusExtraDetails<T>(T status) where T : struct, Enum
    {
        switch ((OVRPlugin.Result)(object)status)
        {
            case OVRPlugin.Result.Success:
                break;

            case OVRPlugin.Result.Failure_SpaceCloudStorageDisabled:
                const string kEnhancedSpatialServicesInfoURL = "https://www.meta.com/help/quest/articles/in-vr-experiences/oculus-features/point-cloud/";
#if UNITY_EDITOR
                if (UnityEditor.SessionState.GetBool(kEnhancedSpatialServicesInfoURL, true))
                {
                    UnityEditor.SessionState.SetBool(kEnhancedSpatialServicesInfoURL, false);
#else
                if (Debug.isDebugBuild)
                {
#endif
                    Debug.Log($"Application.OpenURL(\"{kEnhancedSpatialServicesInfoURL}\")");
                    Application.OpenURL(kEnhancedSpatialServicesInfoURL);
                }
                return "\nEnhanced Spatial Services is disabled on your device. Enable it in OS Settings > Privacy & Safety > Device Permissions";

            case OVRPlugin.Result.Failure_SpaceGroupNotFound:
                return "\n(this is expected if anchors have not been shared to this group UUID yet)";

            case OVRPlugin.Result.Failure_ColocationSessionNetworkFailed:
            case OVRPlugin.Result.Failure_SpaceNetworkTimeout:
            case OVRPlugin.Result.Failure_SpaceNetworkRequestFailed:
                if (Application.internetReachability == NetworkReachability.NotReachable)
                    return "\n(device lacks internet connection)";
                else
                    return "\n(device has internet)";
        }

        return string.Empty;
    }


    /// <summary>
    ///     Determines if an <see cref="OVRSpatialAnchor.OperationResult"/> code might
    ///     imply that invoking the same operation again <em>could</em> succeed,
    ///     typically after a timed delay.
    /// </summary>
    /// <remarks>
    ///     NOTE: This check is suggestive, not exhaustive.
    ///     It is also more educational here in its source code form,
    ///     than it is impactful at runtime.
    /// </remarks>
    public static bool RetryingMightSucceed(this OVRSpatialAnchor.OperationResult opResult)
    {
        switch (opResult)
        {
            // true cases:
            case OVRSpatialAnchor.OperationResult.Success:
                // ^ Succeeding once certainly indicates it might succeed again.
                return true;

            case OVRSpatialAnchor.OperationResult.Failure_SpaceMappingInsufficient:
                // ^ This result means your and your anchor-sharing peer's HMDs don't
                // register as colocated (physically near each other) enough to be
                // able to load the shared anchors.  Assuming you two are definitely
                // in the same physical space, some temporary obstruction could be
                // confusing your HMD(s), such as poor light levels, some kinds of electronic
                // interference, stationary guardians facing away from each other, etc.
                // If whatever the obstruction is clears up, a retry operation could
                // succeed.
                return true;

            case OVRSpatialAnchor.OperationResult.Failure_SpaceLocalizationFailed:
                // ^ Failed localization = your device couldn't figure out how to pose
                // loaded anchors in the real world.  This could be due to variable
                // factors such as light level, obscured sensor lenses, doffing the
                // HMD in the middle of the load request...
                // in summary, transient causes.
                return true;

            case OVRSpatialAnchor.OperationResult.Failure_GroupNotFound:
                // ^ A load operation might fail with "GroupNotFound" but then succeed
                // if retried shortly thereafter if (for example) the group UUID was
                // transmitted to peers *before* any anchors had been completely
                // shared to the group.  (This is a very possible case when using
                // the OVRColocationSession API.)  Simply waiting until at least 1
                // anchor completes the ShareAsync call before you retry the operation
                // often results in successful retries.
                return true;

            // false cases:
            default:
                return false;

            case OVRSpatialAnchor.OperationResult.Failure_SpaceNetworkTimeout:
                // ^ Network timeouts are a commonplace reason to retry ops. Though,
                // given how often they lead to indefinite waiting times on behalf of
                // living, breathing users, it's probably best if your app informs
                // the user(s) about the timeout issue instead of automatically retrying
                // the op.  Perhaps they'd have better luck if they helped the app
                // troubleshoot (e.g. check wifi connectivity) before manually retrying.
                return false;
        }
    }


    //
    // impl. details

    const string k_PlatIDKey = "ocid";

    [StructLayout(LayoutKind.Explicit)]
    struct Reinterpret64
    {
        [FieldOffset(0)]
        public ulong Unsigned;
        [FieldOffset(0)]
        public long Signed;
    }

} // end static class SampleExtensions
