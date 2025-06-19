# Colocated ShowRoom

## Setup process
- Follow the instructions of [Space Sharing Setup Document](./SPACE-SHARING-SETUP.md) => copy from Oculus Sample from which I based myself in

## 🔗 Quick Links
- 🎥 [Demo Video](https://drive.google.com/file/d/1YJzEOWSeFUrpRtX5gbhErMKfq9ch31TW/view?usp=sharing)
- 📑 [APK](https://drive.google.com/file/d/1LH3GKnODNk8PvZKj5FVFuGwVl81UqAiA/view?usp=sharing)
- 💾 [GitHub Repo](https://github.com/tiagomms/XRPrototyping-04-MultiplayerShowroom)

## 🧠 Overview

This is my fourth and final one-week solo prototype for the XR Bootcamp XR Prototyping course (May–July 2025). I had less time for this project for personal reasons, and had about 2 and half days to try to do something here.

**Colocated ShowRoom** prototype aimed to provide a new form of student-level sharing in XR by allowing viewing of 3D projects from students. I teach teenagers Game Development at TUMO and would love to showcase their works away from computers to each other and parents and other students. This seemed a good way to start learning about multiplayer in XR.

> “In the future, presenting your work in classrooms may happen through shared MR spaces.”

Inspired by MR museum-style exhibitions, this project explores MR as a shared educational tool. The long-term goal: let students walk through their 3D creations together.

## 🛠 Tech Stack

- Unity 6000.0.44f1
- Meta XR All-in-One SDK v77 (MRUK, Spatial Anchors)
- Multiplayer (Room Sharing, Anchor Sync)

## 🧰 Project Setup

See the [Space Sharing Setup Document](./SPACE-SHARING-SETUP.md) for base instructions (adapted from Meta's Oculus Sample).

Additional setup notes:
- Used Unity version: `6000.0.44f1`

## 🎯 Key Learnings

- Multiplayer setup and debugging under time constraints
- Meta multiplayer testing requires fast hardware
- MRUK object placement tested in room-scale
- Quest Link unreliability once a bugs occur (OpenXR conflict)
- Building UI flow from Meta’s samples

## ✅ What Worked

- Testing multiplayer visibility with different Quest versions
- Initial UI layout exploration
- Clean project structure despite time constraints

## ⚠️ Challenges and What I'd Do Differently

- Multiplayer required more time (2–3 days not enough)
- Would have focused on single user experience (like the teacher or the users), with some smart object placement first, Hand UI and basic interactions (rotate, zoom, move)
- Reinstall Meta Quest Link and delete its entire history earlier to avoid debug delays
- Better fallback plan if multiplayer fails

## 🌱 Next Steps

- Fix OpenXR Oculus Link issues
- Fix multiplayer issues with Room Sharing and debug from a working sample to my own
- Begin including spatial anchors
- Add minimal interactions (move/zoom/rotate)
- Use MRUK and implement smart placement logic away from objects using Scene Understanding

- Find ways to extract unity projects 3d level design as a single file
  - either as a voxel look-a-like or compression (needs a lot of research)
- Backend to upload and stream these contents

## 📚 Inspiration and References

- [MR Museum Showcases](https://www.youtube.com/watch?v=oFDx8tai8bM)

## 🪪 License

This is a student prototype developed for educational purposes as part of XR Bootcamp. No production guarantees are provided.

MIT License