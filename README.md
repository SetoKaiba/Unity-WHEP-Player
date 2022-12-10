# Unity WebRTC player

This repository contains simple and frankly basic implementation of WebRTC player for Unity (project is based on VRChat World template - but the project itself is generic to any unity project)

This project uses `Unity.WebRTC` package (as of now 3.0.0-pre.1 but specific version should not matter)

This is partial implementation of WHEP specification (<https://www.ietf.org/id/draft-murillo-whep-01.html>) 

WHEPClient.cs implementation is based on <https://github.com/cloudflare/templates/blob/main/stream/webrtc/src/WHEPClient.ts> licensed under MIT license (copyright notice where appropriate)