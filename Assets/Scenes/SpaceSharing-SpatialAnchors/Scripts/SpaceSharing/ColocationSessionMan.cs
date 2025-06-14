// Copyright (c) Meta Platforms, Inc. and affiliates.

using JetBrains.Annotations;

using System;
using System.Runtime.InteropServices;

using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;

using Result = OVRPlugin.Result;


/// <summary>
///   Manages the use of <see cref="OVRColocationSession"/> in place of something more complex (like PUN) to communicate
///   UUID data between clients in the current colocation.
/// </summary>
/// <remarks>
///   Simple and quiet by design, since this usage is supposed to be supplementary rather than focal.
/// </remarks>
public static class ColocationSessionMan
{
    //
    // Public interface

    public static async OVRTask<OVRResult<DiscoData, Result>> Advertise(Guid groupId, Guid roomId, Pose floorPose)
    {
        var customData = SerializeBytes(new[] { groupId, roomId }, floorPose);
        var startAd = await OVRColocationSession.StartAdvertisementAsync(customData);

        var startResult = (Result)startAd.Status;

        Sampleton.Log(
            $"{nameof(OVRColocationSession.StartAdvertisementAsync)}: {startResult}({(int)startResult})",
            error: !startAd.Success
        );

        // ignore startAd.Value, which *is* a group UUID, but not the one we will use.

        return OVRResult<DiscoData, Result>.From(new DiscoData(groupId, roomId, floorPose), startResult);
    }

    public static async OVRTask<OVRResult<DiscoData, Result>> Discover()
    {
        if (s_DiscoTask != default && !s_DiscoTask.IsCompleted)
            return await s_DiscoTask;

        s_DiscoTask = default;
        s_DiscoTaskId = default;

        OVRColocationSession.ColocationSessionDiscovered -= OnSessionDiscovery;
        OVRColocationSession.ColocationSessionDiscovered += OnSessionDiscovery;

        var startDisco = await OVRColocationSession.StartDiscoveryAsync();

        var startResult = (Result)startDisco.Status;

        Sampleton.Log(
            $"{nameof(OVRColocationSession.StartDiscoveryAsync)}: {startResult}({(int)startResult})",
            error: !startDisco.Success
        );

        if (!startDisco.Success)
        {
            OVRColocationSession.ColocationSessionDiscovered -= OnSessionDiscovery;
            return OVRResult<DiscoData, Result>.From(default, startResult);
        }

        s_DiscoTaskId = Guid.NewGuid();
        s_DiscoTask = OVRTask.Create<OVRResult<DiscoData, Result>>(s_DiscoTaskId);

        return await s_DiscoTask;
    }

    public readonly struct DiscoData
    {
        public readonly bool IsValid;
        public readonly Guid GroupId;
        public readonly Guid RoomId;
        public readonly Pose FloorPose;

        public DiscoData(Guid groupId, Guid roomId, Pose floor)
        {
            IsValid = groupId != Guid.Empty;
            GroupId = groupId;
            RoomId = roomId;
            FloorPose = floor;
        }
    }


    //
    // internal impl.

    static OVRTask<OVRResult<DiscoData, Result>> s_DiscoTask;
    static Guid s_DiscoTaskId;

    [RuntimeInitializeOnLoadMethod]
    static void RegisterLifetimeListeners()
    {
        SceneManager.sceneLoaded += resetState;

        static void resetState(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive)
                return;

            OVRColocationSession.ColocationSessionDiscovered -= OnSessionDiscovery;

            _ = OVRColocationSession.StopAdvertisementAsync();
            _ = OVRColocationSession.StopDiscoveryAsync();

            if (s_DiscoTask != default && !s_DiscoTask.IsCompleted)
            {
                OVRTask.SetResult(s_DiscoTaskId, OVRResult<DiscoData, Result>.FromFailure(Result.Failure));
            }

            s_DiscoTask = default;
            s_DiscoTaskId = default;
        }
    }

    static void OnSessionDiscovery(OVRColocationSession.Data data)
    {
        // we can be strict here, so stop after the first discovery
        OVRColocationSession.ColocationSessionDiscovered -= OnSessionDiscovery;

        _ = OVRColocationSession.StopDiscoveryAsync();

        if (s_DiscoTask == default || s_DiscoTask.IsCompleted)
            return;

        Sampleton.Log($"{nameof(OnSessionDiscovery)}: {data.AdvertisementUuid} + {data.Metadata?.Length ?? 0} bytes");

        Sampleton.Log($"+ (we will discard this AdvertisementUuid)");
        _ = data.AdvertisementUuid;

        if (data.Metadata is null || data.Metadata.Length <= k_HeaderSize)
        {
            Sampleton.Error($"- Invalid Metadata block! Aborting.");
            return;
        }

        if (data.Metadata[0] != k_ProtocolUuidsWithPose)
        {
            Sampleton.Error($"- Unknown protocol format in data! Aborting. ({data.Metadata[0]:x2})");
            return;
        }

        int offset = DeserializeUuids(data.Metadata, out var uuids);
        offset = DeserializePose(data.Metadata, offset, out var floorPose);

        var discoData = new DiscoData(uuids[0], uuids[1], floorPose);
        var finalResult = discoData.IsValid ? Result.Success : Result.Failure;

        OVRTask.SetResult(s_DiscoTaskId, OVRResult<DiscoData, Result>.From(discoData, finalResult));

        s_DiscoTask = default;
        s_DiscoTaskId = default;
    }

    //
    // wikiwiki custom protocol implementation

    const byte k_ProtocolUuidsOnly = 1;
    const byte k_ProtocolUuidsWithPose = 3;
    const int k_GuidSize = 16;
    const int k_HeaderSize = k_GuidSize; // only 2/16 bytes are used, but can't use the remainder anyway so why not use it as padding
    const int k_MaxDataSize = OVRColocationSession.Data.MaxMetadataSize;

    static readonly byte[] s_RawData = new byte[k_MaxDataSize]; // would be resized by OVRPlugin anyway if not the max size

    [CanBeNull]
    static byte[] SerializeBytes([NotNull] Guid[] uuids, Pose pose)
    {
        // (assumes anything deserializing this will be the same (little) endianness as the sender)

        int idx = 0;

        // write protocol code
        s_RawData[idx] = k_ProtocolUuidsWithPose;
        idx += sizeof(byte);

        // write data length
        int dataSz = uuids.Length;
        if (k_HeaderSize + dataSz * k_GuidSize + sizeof(float) * 7 > s_RawData.Length)
        {
            Sampleton.Error($"{nameof(ColocationSessionMan)}.{nameof(SerializeBytes)}: {dataSz} is too many RoomIds to send in the custom data block!");
            return null;
        }

        Assert.IsTrue(dataSz <= byte.MaxValue, "dataSz <= byte.MaxValue");

        s_RawData[idx] = (byte)dataSz;

        idx = k_HeaderSize;

        // write uuids
        Span<byte> toSpan;
        for (int i = 0; i < dataSz; ++i)
        {
            toSpan = new Span<byte>(s_RawData, idx, k_GuidSize);
            idx += k_GuidSize;
            _ = uuids[i].TryWriteBytes(toSpan); // can't fail as long as k_GuidSize is correct
        }

        // write pose
        toSpan = new Span<byte>(s_RawData, idx, sizeof(float) * 7);
        MemoryMarshal.Write(toSpan, ref pose);

        return s_RawData;
    }

    static int DeserializeUuids([NotNull] byte[] bytes, [NotNull] out Guid[] uuids)
    {
        int idx = sizeof(byte);

        var dataSz = bytes[idx];
        idx = k_HeaderSize;
        uuids = new Guid[dataSz];

        for (int i = 0; i < dataSz; ++i)
        {
            var fromSpan = new ReadOnlySpan<byte>(bytes, idx, k_GuidSize);
            idx += k_GuidSize;
            uuids[i] = new Guid(fromSpan);
        }

        return idx;
    }

    static int DeserializePose([NotNull] byte[] bytes, int idx, [NotNull] out Pose pose)
    {
        var fromSpan = new ReadOnlySpan<byte>(bytes, idx, sizeof(float) * 7);
        if (MemoryMarshal.TryRead(fromSpan, out pose))
            idx += fromSpan.Length;
        return idx;
    }

}
