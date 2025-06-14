// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using Meta.XR.MRUtilityKit;

using UnityEngine;
using UnityEngine.Events;

public class AlignPlayer : MonoBehaviour
{
    public static AlignPlayer Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
        }
    }

    // NOTE: setting spatial anchor alignments
    #region SpatialAnchors-Sample

    [Header("Spatial Anchors alignment setup")]
    [SerializeField] Transform player;
    [SerializeField] Transform playerHands;

    SharedAnchor m_CurrentAlignmentAnchor;
    Coroutine m_AlignCoroutine;
    
    public void SetAlignmentAnchor(SharedAnchor anchor)
    {
        if (m_AlignCoroutine != null)
        {
            StopCoroutine(m_AlignCoroutine);
            m_AlignCoroutine = null;
        }

        Sampleton.Log($"{nameof(AlignPlayer)}: setting {anchor} as the alignment anchor...");

        if (m_CurrentAlignmentAnchor)
        {
            Sampleton.Log($"{nameof(AlignPlayer)}: unset {m_CurrentAlignmentAnchor} as the alignment anchor.");
            m_CurrentAlignmentAnchor.IsSelectedForAlign = false;
        }

        m_CurrentAlignmentAnchor = null;

        if (player)
        {
            player.SetPositionAndRotation(default, Quaternion.identity);
        }

        if (!anchor || !player)
            return;

        m_AlignCoroutine = StartCoroutine(RealignRoutine(anchor));
    }

    IEnumerator RealignRoutine(SharedAnchor anchor)
    {
        yield return null;

        var anchorTransform = anchor.transform;

        player.position = anchorTransform.InverseTransformPoint(Vector3.zero);
        player.eulerAngles = new Vector3(0, -anchorTransform.eulerAngles.y, 0);

        if (playerHands)
        {
            playerHands.SetLocalPositionAndRotation(
                -player.position,
                Quaternion.Inverse(player.rotation)
            );
        }

        m_CurrentAlignmentAnchor = anchor;
        anchor.IsSelectedForAlign = true;

        Sampleton.Log($"{nameof(AlignPlayer)}: finished alignment -> {anchor}");
        m_AlignCoroutine = null;
    }
    #endregion
    
    // NOTE: code from Shared Room sample
    #region SpaceSharing-Sample
    [Header("Room sharing alignment setup")]
    [SerializeField]
    public UnityEvent<bool> m_OnAlign = new();

    public void AlignFloorAnchorToOrigin()
    {
        var mruk = MRUK.Instance;
        if (!mruk)
        {
            Sampleton.Error("- MRUK NOT FOUND");
            m_OnAlign.Invoke(false);
            return;
        }
        var currentRoom = mruk.GetCurrentRoom();
        var rooms = mruk.Rooms;
        if (rooms.Count == 0 || !currentRoom)
        {
            Sampleton.Error("- MRUK ROOMS NOT FOUND");
            m_OnAlign.Invoke(false);
            return;
        }
        if (!currentRoom.FloorAnchor)
        {
            Sampleton.Error("- FLOOR ANCHOR NOT FOUND");
            m_OnAlign.Invoke(false);
            return;
        }

        var floorAnchor = currentRoom.FloorAnchor.transform;

        // We're adjusting all anchored Room Transforms such that the current Room's "Floor" anchor aligns with /
        // becomes Unity's world origin (0,0,0).

        // Calculate the offset for that adjustment:

        var floorOffset = new Pose(
            position: floorAnchor.position,
        //  Floor anchors are oriented Z-up/Y-forward ~ we should flip it:
            rotation: Quaternion.LookRotation(forward: floorAnchor.up, upwards: Vector3.up)
        );

        // Aside:
        // ( We use Vector3.up instead of floorAnchor.forward:  Scene anchors only orient themselves yaw-wise relative
        //   to their Room, so to avoid introducing any tilt noise, we can "fudge it" and assume as if the current floor
        //   anchor's up vector is and has always been the Y(+)-axis. This assumption would only ever be minutely off,
        //   and unlike the alternative (floorAnchor.forward) would not accumulate error with successive alignments. )

        // We now have the offset that would transform (move) an object at (0,0,0) to match the floorAnchor's pose,
        // but what we want is the offset that does the OPPOSITE.

        // This is calculated by "inverting" the Pose:
        floorOffset.rotation = Quaternion.Inverse(floorOffset.rotation);
        floorOffset.position = floorOffset.rotation * -floorOffset.position;

        // "Apply" this inverse offset to each Room (including the current):
        foreach (var room in rooms)
        {
            // AKA put each room into the child space of the (inverted) floor pose
            room.transform.SetPositionAndRotation(
                floorOffset.position + floorOffset.rotation * room.transform.position,
                floorOffset.rotation * room.transform.rotation
            );
            // ( This is akin to affine Matrix4x4 multiplication, which is what powers parent-child relationships in
            //   Unity's hierarchy.  However, due to Unity's "lazy-recalculation" model for Transform matrices,
            //   matrix products cannot be set directly.    (It is probably for the best!)
            //   This thing we often do instead (above) is sometimes called a "Pose product" / "Pose multiplication",
            //   but terms vary widely. )
        }

        m_OnAlign.Invoke(true);
    }
    #endregion

}
